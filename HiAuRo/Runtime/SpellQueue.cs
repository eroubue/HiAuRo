using HiAuRo.ACR;

namespace HiAuRo.Runtime;

/// <summary>
/// HiAuRo 内部 Slot 调度队列（非 FFXIV 客户端队列）
/// </summary>
public sealed class SpellQueue
{
    private readonly Queue<Slot> _queue = new();

    public void Enqueue(Slot slot) => _queue.Enqueue(slot);

    public bool HasPending() => _queue.Count > 0;

    public int QueueSize => _queue.Count;

    public Slot? GetNext() => _queue.Count > 0 ? _queue.Dequeue() : null;

    public void Clear() => _queue.Clear();
}
