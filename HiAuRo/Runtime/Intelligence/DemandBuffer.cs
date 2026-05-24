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

    /// <summary>按 factNodeId 分组取出所有积压需求（空队列快速返回）</summary>
    public static ILookup<string, MovementDemand> GetGrouped()
    {
        if (_pending.IsEmpty)
            return Array.Empty<MovementDemand>().ToLookup(d => d.FactNodeId);
        return _pending.ToArray().ToLookup(d => d.FactNodeId);
    }

    /// <summary>移除已释放的需求（省去 Where().ToArray() 中间分配）</summary>
    public static void Remove(IEnumerable<string> demandIds)
    {
        lock (_rebuildLock)
        {
            var ids = new HashSet<string>(demandIds);
            var snapshot = _pending.ToArray();
            while (_pending.TryDequeue(out _)) { }
            foreach (var d in snapshot)
                if (!ids.Contains(d.Id))
                    _pending.Enqueue(d);
        }
    }

    /// <summary>清空</summary>
    public static void Clear()
    {
        while (_pending.TryDequeue(out _)) { }
    }
}
