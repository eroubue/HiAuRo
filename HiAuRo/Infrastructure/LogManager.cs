using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using HiAuRo.ACR;

namespace HiAuRo.Infrastructure;

/// <summary>日志条目记录</summary>
/// <param name="Timestamp">时间戳</param>
/// <param name="Type">类型</param>
/// <param name="Content">内容</param>
public record LogEntry(DateTime Timestamp, string Type, string Content);

/// <summary>日志管理器 —— 内存环形缓冲 + 文件持久化</summary>
public sealed class LogManager : IDisposable
{
    /// <summary>日志管理器单例</summary>
    public static LogManager Instance { get; } = new();

    private const int MaxEntries = 5000;
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private readonly object _writeLock = new();
    private static readonly ConcurrentDictionary<Type, FieldInfo[]> _fieldCache = new();
    private long _sequence;
    private uint _lastTerritoryId;
    private StreamWriter? _writer;

    /// <summary>ID 字段名后缀（用于识别需要解析实体名的字段）</summary>
    private static readonly HashSet<string> EntityIdFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "SourceID", "TargetID", "TargetOID", "EntityId", "SourceOID", "OwnerId"
    };

    /// <summary>不需要 hex 显示的浮点/字符串字段</summary>
    private static readonly HashSet<string> NonHexFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "CastTime", "PosX", "PosY", "PosZ", "Message", "SourceName", "YellMsg", "Name"
    };

    /// <summary>初始化日志管理器</summary>
    public void Init(string configDir)
    {
        // 先释放旧的 writer，防止泄露
        if (_writer != null)
        {
            try { _writer.Flush(); _writer.Dispose(); } catch { }
            _writer = null;
        }

        _lastTerritoryId = 0;
        _sequence = 0;

        var logPath = Path.Combine(configDir, "hiauro_events.log");
        try
        {
            _writer = new StreamWriter(logPath, append: true)
            {
                AutoFlush = true
            };
            // 写入 session 分隔符
            _writer.WriteLine($"# === Session Start: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            DService.Instance().Log.Information($"[LogManager] 日志文件: {logPath}");
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[LogManager] 无法创建日志文件: {ex.Message}");
        }
    }

    /// <summary>记录触发条件事件日志</summary>
    public void Log(ITriggerCondParams? p)
    {
        if (p == null) return;

        var seq = Interlocked.Increment(ref _sequence);
        var type = p.GetType().Name;
        var content = SerializeFields(p);
        var zoneCtx = GetZoneContext();
        var entry = new LogEntry(DateTime.Now, type, content);

        // 内存缓冲（环形）
        _entries.Enqueue(entry);
        while (_entries.Count > MaxEntries)
            _entries.TryDequeue(out _);

        // 写文件，writer 为 null 时跳过锁
        var writer = _writer;
        if (writer == null) return;

        try
        {
            lock (_writeLock)
            {
                var ts = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                writer.WriteLine(string.IsNullOrEmpty(zoneCtx)
                    ? $"{ts} | [{seq:D4}] | {entry.Type} | {entry.Content}"
                    : $"{ts} | [{seq:D4}] | {entry.Type} | {entry.Content} | {zoneCtx}");
            }
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[LogManager] 写入失败: {ex.Message}");
        }
    }

    /// <summary>获取所有日志条目</summary>
    public IReadOnlyList<LogEntry> GetEntries()
    {
        return _entries.ToArray();
    }

    /// <summary>清空日志</summary>
    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
        _sequence = 0;
    }

    /// <summary>释放日志文件资源</summary>
    public void Dispose()
    {
        try
        {
            _writer?.WriteLine($"# === Session End: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            _writer?.Flush();
            _writer?.Dispose();
        }
        catch { }
        _writer = null;
    }

    /// <summary>
    /// 反射读取所有 public 字段，格式化为 KV 串（含实体名解析 + hex + 技能/状态名）
    /// </summary>
    private static string SerializeFields(ITriggerCondParams p)
    {
        var fields = _fieldCache.GetOrAdd(p.GetType(),
            t => t.GetFields(BindingFlags.Public | BindingFlags.Instance));

        var parts = new List<string>(fields.Length);
        foreach (var f in fields)
        {
            var val = f.GetValue(p);
            var str = FormatField(f.Name, val);
            parts.Add($"{f.Name}={str}");
        }
        return parts.Count > 0 ? string.Join(", ", parts) : "(empty)";
    }

    /// <summary>格式化单个字段值，附加实体名/hex/技能名</summary>
    private static string FormatField(string fieldName, object? val)
    {
        var core = val switch
        {
            null => "null",
            string s => $"\"{s}\"",
            float fv => fv.ToString("F2"),
            bool b => b ? "true" : "false",
            _ => val.ToString() ?? "null"
        };

        // 非 ID 字段直接返回
        if (val is not (uint or ushort or int or byte or long or ulong) && val is not string)
            return core;

        // 跳过不需要 hex 显示的字段
        if (val is string || NonHexFields.Contains(fieldName))
            return core;

        // hex 显示
        var hexSuffix = val switch
        {
            uint uv => $" (0x{uv:X})",
            ushort usv => $" (0x{usv:X})",
            int iv => iv >= 0 ? $" (0x{iv:X})" : "",
            byte bv => $" (0x{bv:X2})",
            long lv => lv >= 0 ? $" (0x{lv:X})" : "",
            ulong ulv => $" (0x{ulv:X})",
            _ => ""
        };

        // 实体名解析
        if (EntityIdFields.Contains(fieldName) && val is uint entityId && entityId != 0 && entityId != 0xE0000000)
        {
            try
            {
                var obj = DService.Instance().ObjectTable?.SearchByID(entityId);
                if (obj != null)
                {
                    var name = obj.Name.ToString();
                    return $"{core}{hexSuffix} \"{name}\"";
                }
            }
            catch { }
        }

        // 技能名解析（ActionID 字段，支持 uint/ushort/int）
        if (fieldName.Equals("ActionID", StringComparison.OrdinalIgnoreCase) && val is not null)
        {
            try
            {
                var actionId = System.Convert.ToUInt32(val);
                if (actionId > 0)
                {
                    var sheet = DService.Instance().Data.GetExcelSheet<Lumina.Excel.Sheets.Action>();
                    var row = sheet?.GetRow(actionId);
                    if (row.HasValue && !string.IsNullOrEmpty(row.Value.Name.ToString()))
                        return $"{core}{hexSuffix} \"{row.Value.Name}\"";
                }
            }
            catch { }
        }

        // 状态名解析（StatusID 字段）
        if ((fieldName.Equals("StatusID", StringComparison.OrdinalIgnoreCase) ||
             fieldName.Equals("IconID", StringComparison.OrdinalIgnoreCase))
            && val is uint statusId && statusId > 0)
        {
            try
            {
                var sheet = DService.Instance().Data.GetExcelSheet<Lumina.Excel.Sheets.Status>();
                var row = sheet?.GetRow(statusId);
                if (row.HasValue && !string.IsNullOrEmpty(row.Value.Name.ToString()))
                    return $"{core}{hexSuffix} \"{row.Value.Name}\"";
            }
            catch { }
        }

        // TetherID 暂不需要额外解析
        if (fieldName.Equals("TetherID", StringComparison.OrdinalIgnoreCase) && val is ushort tetherId && tetherId > 0)
            return $"{core}{hexSuffix}";

        return string.IsNullOrEmpty(hexSuffix) ? core : $"{core}{hexSuffix}";
    }

    /// <summary>获取当前副本上下文，进入新区时返回 Territory 信息</summary>
    private string GetZoneContext()
    {
        try
        {
            var terri = DService.Instance().ClientState?.TerritoryType ?? 0;
            if (terri == _lastTerritoryId || terri == 0)
                return "";

            _lastTerritoryId = terri;
            try
            {
                var sheet = DService.Instance().Data.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>();
                var row = sheet?.GetRow(terri);
                if (row.HasValue && !string.IsNullOrEmpty(row.Value.PlaceName.Value.Name.ToString()))
                    return $"Territory={terri} (0x{terri:X}) \"{row.Value.PlaceName.Value.Name}\"";
            }
            catch { }
            return $"Territory={terri} (0x{terri:X})";
        }
        catch
        {
            return "";
        }
    }
}
