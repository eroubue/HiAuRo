using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HiAuRo.UI;

/// <summary>
/// C# ↔ JS 消息路由（JSON 序列化 / 分发）
/// </summary>
public sealed class WebUiBridge : IDisposable
{
    private readonly List<WebSocket> _clients = [];
    private readonly object _lock = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly Dictionary<string, Action<JsonElement?>> _handlers = [];

    /// <summary>注册消息处理器</summary>
    public void On(string type, Action<JsonElement?> handler)
    {
        _handlers[type] = handler;
    }

    /// <summary>推送消息到所有已连接客户端</summary>
    public async Task SendAsync(object message)
    {
        var json = JsonSerializer.Serialize(message, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(bytes);

        List<WebSocket> sendTargets;
        lock (_lock)
        {
            _clients.RemoveAll(c => c.State != WebSocketState.Open);
            sendTargets = [.._clients];
        }

        foreach (var client in sendTargets)
        {
            try
            {
                await client.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Warning($"[WebUiBridge] SendAsync 失败: {ex.Message}");
                lock (_lock) _clients.Remove(client);
            }
        }
    }

    /// <summary>处理单个 WebSocket 连接</summary>
    public async Task HandleConnection(WebSocket ws, CancellationToken ct)
    {
        lock (_lock) _clients.Add(ws);

        // 连接时推送初始状态
        PushInitialStatus(ws);

        var buffer = new byte[8192];
        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var type = doc.RootElement.GetProperty("type").GetString();
                    if (type != null && _handlers.TryGetValue(type, out var handler))
                    {
                        var data = doc.RootElement.TryGetProperty("data", out var dataElement) ? dataElement : (JsonElement?)null;
                        handler(data);
                    }
                }
                catch (Exception ex)
                {
                    DService.Instance().Log.Warning($"[WebUiBridge] 消息解析失败: {ex.Message}");
                }
            }
        }
        catch (WebSocketException) { }
        finally
        {
            lock (_lock) _clients.Remove(ws);
            if (ws.State != WebSocketState.Closed)
            {
                try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); }
                catch { }
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var client in _clients)
            {
                try { client.Abort(); } catch { }
            }
            _clients.Clear();
        }
    }

    private async void PushInitialStatus(WebSocket ws)
    {
        try
        {
            var hotkeys = ACR.HotkeyHelper.GetAll().Select(r => new
            {
                id = r.Id,
                label = r.Label,
                iconId = r.IconId,
                iconUrl = IconServer.GetIconUrl(r.IconId),
                available = r.Check() >= 0,
                binding = ACR.HotkeyHelper.GetBinding(r.Id)
            }).ToList();

            var qts = ACR.QTHelper.GetAll().Select(q => new
            {
                id = q.Id,
                label = q.Label,
                value = q.Value,
                tooltip = q.Tooltip,
                color = q.Color,
                binding = q.HotkeyBinding
            }).ToList();

            var json = JsonSerializer.Serialize(new
            {
                type = "status",
                data = new
                {
                    job = HiAuRo.Runtime.ACRLifecycle.CurrentAcrName,
                    enabled = HiAuRo.Runtime.RuntimeCore.IsRunning,
                    paused = HiAuRo.ACR.MainControlHelper.IsPaused,
                    inCombat = HiAuRo.Runtime.CombatContext.IsInCombat,
                    currentSpell = "Idle",
                    gcdRemaining = 0,
                    gcdDuration = 2500,
                    hotkeys,
                    qts
                }
            }, _jsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch { }
    }
}
