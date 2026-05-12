using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using HiAuRo.ACR;

namespace HiAuRo.Infrastructure;

public record LogEntry(DateTime Timestamp, string Type, string Content);

public sealed class LogManager : IDisposable
{
    public static LogManager Instance { get; } = new();

    private const int MaxEntries = 5000;
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private readonly object _writeLock = new();
    private static readonly ConcurrentDictionary<Type, FieldInfo[]> _fieldCache = new();
    private int _count;
    private StreamWriter? _writer;

    public void Init(string configDir)
    {
        // 先释放旧的 writer，防止泄露
        if (_writer != null)
        {
            try { _writer.Flush(); _writer.Dispose(); } catch { }
            _writer = null;
        }

        var logPath = Path.Combine(configDir, "hiauro_events.log");
        try
        {
            _writer = new StreamWriter(logPath, append: true)
            {
                AutoFlush = true
            };
            DService.Instance().Log.Information($"[LogManager] 日志文件: {logPath}");
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[LogManager] 无法创建日志文件: {ex.Message}");
        }
    }

    public void Log(ITriggerCondParams? p)
    {
        if (p == null) return;

        var type = p.GetType().Name;
        var content = SerializeFields(p);
        var entry = new LogEntry(DateTime.Now, type, content);

        // 内存缓冲（环形），用 Interlocked 避免 O(n) Count
        if (Interlocked.Increment(ref _count) > MaxEntries)
        {
            if (_entries.TryDequeue(out _))
                Interlocked.Decrement(ref _count);
        }
        _entries.Enqueue(entry);

        // 写文件，writer 为 null 时跳过锁
        var writer = _writer;
        if (writer == null) return;

        try
        {
            lock (_writeLock)
            {
                writer.WriteLine($"{entry.Timestamp:HH:mm:ss.fff} | {entry.Type} | {entry.Content}");
            }
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[LogManager] 写入失败: {ex.Message}");
        }
    }

    public IReadOnlyList<LogEntry> GetEntries()
    {
        return _entries.ToArray();
    }

    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
    }

    public void Dispose()
    {
        _writer?.Flush();
        _writer?.Dispose();
        _writer = null;
    }

    /// <summary>
    /// 反射读取所有 public 字段，格式化为 KV 串
    /// </summary>
    private static string SerializeFields(ITriggerCondParams p)
    {
        var fields = _fieldCache.GetOrAdd(p.GetType(),
            t => t.GetFields(BindingFlags.Public | BindingFlags.Instance));

        var parts = new List<string>(fields.Length);
        foreach (var f in fields)
        {
            var val = f.GetValue(p);
            var str = val switch
            {
                null => "null",
                string s => $"\"{s}\"",
                float fv => fv.ToString("F2"),
                _ => val.ToString()
            };
            parts.Add($"{f.Name}={str}");
        }
        return parts.Count > 0 ? string.Join(", ", parts) : "(empty)";
    }
}
