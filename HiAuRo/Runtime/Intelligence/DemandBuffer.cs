using System.Collections.Concurrent;

namespace HiAuRo.Runtime.Intelligence;

/// <summary>
/// 线程安全的移动需求缓冲区——脚本和 IPC 接收方写入，智能层读取
/// </summary>
public static class DemandBuffer
{
    private static readonly ConcurrentQueue<MovementDemand> _pending = new();
    private static readonly object _rebuildLock = new();
    private static int _orderCounter;

    /// <summary>添加需求（自动分配 AddedOrder）</summary>
    public static void Add(MovementDemand demand)
    {
        demand.AddedOrder = Interlocked.Increment(ref _orderCounter);
        _pending.Enqueue(demand);
    }

    /// <summary>按 factNodeId 分组取出所有积压需求</summary>
    public static ILookup<string, MovementDemand> GetGrouped()
    {
        return _pending.ToArray().ToLookup(d => d.FactNodeId);
    }

    /// <summary>移除已释放的需求</summary>
    public static void Remove(IEnumerable<string> demandIds)
    {
        lock (_rebuildLock)
        {
            var ids = new HashSet<string>(demandIds);
            var remaining = _pending.Where(d => !ids.Contains(d.Id)).ToArray();
            while (_pending.TryDequeue(out _)) { }
            foreach (var d in remaining)
                _pending.Enqueue(d);
        }
    }

    /// <summary>清空</summary>
    public static void Clear()
    {
        while (_pending.TryDequeue(out _)) { }
    }
}
