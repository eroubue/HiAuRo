using System.Runtime.CompilerServices;

namespace HiAuRo.Runtime;

/// <summary>
/// 轻量协程调度器 —— 支持回调 + async/await
/// 每帧由 RuntimeCore 驱动
/// </summary>
public sealed class Coroutine
{
    /// <summary>协程调度器单例</summary>
    public static Coroutine Instance { get; } = new();

    private readonly List<CoroutineWait> _waiting = [];

    private Coroutine() { }

    /// <summary>每帧由 RuntimeCore 调用，推进所有等待中的协程</summary>
    public void Update()
    {
        for (var i = _waiting.Count - 1; i >= 0; i--)
        {
            var task = _waiting[i];
            if (task.IsCompleted)
            {
                try
                {
                    task.Callback?.Invoke();
                }
                catch (Exception ex)
                {
                    DService.Instance().Log.Error($"[Coroutine] 协程回调异常: {ex}");
                }
                _waiting.RemoveAt(i);
            }
        }
    }

    /// <summary>等待指定毫秒后执行回调</summary>
    public void WaitAsync(long ms, Action? callback = null)
    {
        if (ms <= 0)
        {
            callback?.Invoke();
            return;
        }
        _waiting.Add(new CoroutineWait { DueTime = Environment.TickCount64 + ms, Callback = callback });
    }

    /// <summary>返回一个 Task，在指定毫秒后完成（支持 async/await）</summary>
    public Task DelayAsync(double ms)
    {
        if (ms <= 0) return Task.CompletedTask;
        var tcs = new TaskCompletionSource<bool>();
        _waiting.Add(new CoroutineWait { DueTime = Environment.TickCount64 + (long)ms, Callback = () => tcs.TrySetResult(true) });
        return tcs.Task;
    }

    /// <summary>每帧检查条件，满足时完成 Task</summary>
    public Task WaitUntilAsync(Func<bool> condition)
    {
        var tcs = new TaskCompletionSource<bool>();
        _waiting.Add(new CoroutineWait { Condition = condition, Callback = () => tcs.TrySetResult(true) });
        return tcs.Task;
    }

    /// <summary>重置所有等待任务</summary>
    public void Clear()
    {
        foreach (var w in _waiting)
            w.Cancelled = true;
        _waiting.Clear();
    }

    /// <summary>协程等待状态</summary>
    public sealed class CoroutineWait
    {
        internal long DueTime;
        internal Func<bool>? Condition;
        internal Action? Callback;
        internal bool Cancelled;

        /// <summary>是否已完成</summary>
        public bool IsCompleted => !Cancelled && (Condition != null ? Condition() : Environment.TickCount64 >= DueTime);
    }
}
