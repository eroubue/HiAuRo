using System.Linq;
using System.Text.Json;
using Browsingway;
using HiAuRo.Infrastructure;
using OmenTools.Dalamud.Helpers;

namespace HiAuRo.UI;

/// <summary>
/// Browsingway IPC 服务 —— 通过 Dalamud IPC 控制 Browsingway 的 overlay 窗口
/// 同时暴露 Provider 端点供 Browsingway 反向查询 HiAuRo 的 WebUI 设置
/// </summary>
internal sealed class BrowsingwayIpc : IDisposable
{
    private readonly int _port;
    private readonly Func<UIMode> _getUIMode;
    private OverlayWindowSetting[] _overlayConfigs = [];
    public bool IsReady { get; private set; }
    private readonly CancellationTokenSource _cts = new();

    public BrowsingwayIpc(int port, Func<UIMode> getUIMode)
    {
        _port = port;
        _getUIMode = getUIMode;
        RegisterProviders();
    }

    /// <summary>注册 HiAuRo → Browsingway 的 IPC Provider 端点</summary>
    private void RegisterProviders()
    {
        var pi = DService.Instance().PI;
        try
        {
            pi.GetIpcProvider<int>("HiAuRo.GetWebUiPort").RegisterFunc(() => _port);
            pi.GetIpcProvider<string>("HiAuRo.GetOverlaysJson").RegisterFunc(() =>
                JsonSerializer.Serialize(_overlayConfigs,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            pi.GetIpcProvider<bool>("HiAuRo.IsWebUIMode").RegisterFunc(() => _getUIMode() == UIMode.WebUI);
            DService.Instance().Log.Information("[BrowsingwayIpc] IPC Provider 端点已注册");
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[BrowsingwayIpc] IPC Provider 注册失败: {ex.Message}");
        }
    }

    /// <summary>注销 IPC Provider 端点</summary>
    private void UnregisterProviders()
    {
        var pi = DService.Instance().PI;
        try { pi.GetIpcProvider<int>("HiAuRo.GetWebUiPort").UnregisterFunc(); } catch { }
        try { pi.GetIpcProvider<string>("HiAuRo.GetOverlaysJson").UnregisterFunc(); } catch { }
        try { pi.GetIpcProvider<bool>("HiAuRo.IsWebUIMode").UnregisterFunc(); } catch { }
    }

    /// <summary>异步等待 Browsingway 就绪，未安装则自动安装</summary>
    public async Task InitAsync(OverlayWindowSetting[] overlays)
    {
        try
        {
            _overlayConfigs = overlays;
            await WaitForReadyAsync();

            foreach (var ol in overlays)
            {
                CreateOrUpdateOverlay(ol);
            }

            IsReady = true;
            DService.Instance().Log.Information($"[BrowsingwayIpc] 已就绪, 注册 {overlays.Length} 个 overlay");
        }
        catch (OperationCanceledException)
        {
            DService.Instance().Log.Information("[BrowsingwayIpc] 初始化已取消");
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[BrowsingwayIpc] 初始化失败: {ex.Message}");
        }
    }

    private async Task WaitForReadyAsync()
    {
        var pi = DService.Instance().PI;

        // 延迟 15s 一次性检测是否需要安装
        _ = Task.Run(async () =>
        {
            await Task.Delay(15000, _cts.Token);
            if (_cts.IsCancellationRequested) return;
            if (!IsBrowsingwayInstalled())
            {
                DService.Instance().Chat.Print("[HiAuRo] 未检测到 Browsingway，正在自动安装...");
                var installed = await TryInstallBrowsingwayAsync();
                if (installed)
                    DService.Instance().Chat.Print("[HiAuRo] Browsingway 安装成功，等待加载...");
                else
                    DService.Instance().Chat.Print("[HiAuRo] Browsingway 自动安装失败，请手动添加库:\nhttps://raw.githubusercontent.com/denghaoxuan991876906/Browsingway/main/pluginmaster.json\n然后搜索安装 Browsingway");
            }
        }, _cts.Token);

        while (!_cts.IsCancellationRequested)
        {
            try
            {
                if (pi.GetIpcSubscriber<bool>("Browsingway.IsReady").InvokeFunc())
                {
                    DService.Instance().Log.Information("[BrowsingwayIpc] Browsingway 已就绪");
                    return;
                }
            }
            catch { }

            await Task.Delay(1000, _cts.Token);
        }
    }

    /// <summary>检查 Browsingway 是否已安装</summary>
    private static bool IsBrowsingwayInstalled()
    {
        try
        {
            return DService.Instance().PI.InstalledPlugins.Any(x => x.InternalName == "Browsingway");
        }
        catch { return false; }
    }

    /// <summary>自动添加库并安装 Browsingway</summary>
    private static async Task<bool> TryInstallBrowsingwayAsync()
    {
        try
        {
            const string repoUrl = "https://raw.githubusercontent.com/denghaoxuan991876906/Browsingway/main/pluginmaster.json";
            const string internalName = "Browsingway";

            // 标记主线程（反射调用 Dalamud 内部 API 需要）
            DalamudReflector.MarkCurrentThreadAsMainThread();

            var result = await DalamudReflector.AddPlugin(repoUrl, internalName);
            if (result)
            {
                DService.Instance().Log.Information("[BrowsingwayIpc] 安装成功");
            }
            else
            {
                DService.Instance().Log.Warning("[BrowsingwayIpc] 安装失败");
            }
            return result;
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[BrowsingwayIpc] 安装异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>创建或更新 overlay（替换 URL 中的端口占位符）</summary>
    public void CreateOrUpdateOverlay(OverlayWindowSetting ol)
    {
        try
        {
            var url = ol.Url.Replace("localhost:5678", $"localhost:{_port}");
            DService.Instance().Framework.RunOnFrameworkThread(() =>
            {
                DService.Instance().PI
                    .GetIpcSubscriber<CreateOrUpdateArgs, object>("Browsingway.Overlay.CreateOrUpdate")
                    .InvokeAction(new CreateOrUpdateArgs
                    {
                        Name = ol.Name,
                        Url = url,
                        Width = ol.Width,
                        Height = ol.Height,
                        Zoom = ol.Zoom,
                        Locked = ol.Locked
                    });
            });
            if (ol.Visible)
                SetOverlayVisible(ol.Name, true);
            else
                SetOverlayVisible(ol.Name, false);
            DService.Instance().Log.Debug($"[BrowsingwayIpc] CreateOrUpdate: {ol.Name} {url} {ol.Width}x{ol.Height}");
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[BrowsingwayIpc] CreateOrUpdate 失败 ({ol.Name}): {ex.Message}");
        }
    }

    /// <summary>调整 overlay 尺寸（保留其他属性从配置读取）</summary>
    public void ResizeOverlay(string name, int width, int height)
    {
        var cfg = _overlayConfigs.FirstOrDefault(o => o.Name == name);
        if (cfg == null) return;
        try
        {
            var url = cfg.Url.Replace("localhost:5678", $"localhost:{_port}");
            DService.Instance().Framework.RunOnFrameworkThread(() =>
            {
                DService.Instance().PI
                    .GetIpcSubscriber<CreateOrUpdateArgs, object>("Browsingway.Overlay.CreateOrUpdate")
                    .InvokeAction(new CreateOrUpdateArgs
                    {
                        Name = name,
                        Url = url,
                        Width = width,
                        Height = height,
                        Zoom = cfg.Zoom,
                        Locked = cfg.Locked
                    });
            });
            DService.Instance().Log.Debug($"[BrowsingwayIpc] Resize: {name} {width}x{height}");
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[BrowsingwayIpc] Resize 失败 ({name}): {ex.Message}");
        }
    }

    /// <summary>显示/隐藏 overlay</summary>
    public void SetOverlayVisible(string name, bool visible)
    {
        try
        {
            DService.Instance().Framework.RunOnFrameworkThread(() =>
            {
                DService.Instance().PI
                    .GetIpcSubscriber<SetVisibilityArgs, object>("Browsingway.Overlay.SetVisibility")
                    .InvokeAction(new SetVisibilityArgs
                    {
                        Name = name,
                        Visible = visible
                    });
            });
            DService.Instance().Log.Debug($"[BrowsingwayIpc] SetVisibility: {name} = {visible}");
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[BrowsingwayIpc] SetVisibility 失败 ({name}): {ex.Message}");
        }
    }

    /// <summary>按配置显示/隐藏所有 overlay</summary>
    public void ShowConfigured(OverlayWindowSetting[] overlays)
    {
        foreach (var ol in overlays)
            SetOverlayVisible(ol.Name, ol.Visible);
    }

    /// <summary>禁用所有 overlay（销毁 CEF 实例，释放资源）</summary>
    public void DisableAll(OverlayWindowSetting[] overlays)
    {
        foreach (var ol in overlays)
            SetOverlayDisabled(ol.Name, true);
    }

    /// <summary>启用所有 overlay（重新创建 CEF 实例）</summary>
    public void EnableAll(OverlayWindowSetting[] overlays)
    {
        foreach (var ol in overlays)
            SetOverlayDisabled(ol.Name, false);
    }

    /// <summary>禁用/启用 overlay</summary>
    private void SetOverlayDisabled(string name, bool disabled)
    {
        try
        {
            DService.Instance().Framework.RunOnFrameworkThread(() =>
            {
                DService.Instance().PI
                    .GetIpcSubscriber<SetDisabledArgs, object>("Browsingway.Overlay.SetDisabled")
                    .InvokeAction(new SetDisabledArgs { Name = name, Disabled = disabled });
            });
            DService.Instance().Log.Debug($"[BrowsingwayIpc] SetDisabled: {name} = {disabled}");
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[BrowsingwayIpc] SetDisabled 失败 ({name}): {ex.Message}");
        }
    }

    /// <summary>隐藏所有 overlay</summary>
    public void HideAll(OverlayWindowSetting[] overlays)
    {
        foreach (var ol in overlays)
            SetOverlayVisible(ol.Name, false);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        IsReady = false;
        UnregisterProviders();
    }
}
