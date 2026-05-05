using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 触发条件参数 —— 经过时间
/// </summary>
public sealed class TriggerCondParams_经过时间 : ITriggerCondParams
{
    /// <summary>距离战斗开始的时间（毫秒）</summary>
    public int TimeMs;
}

/// <summary>
/// 检测战斗经过时间是否达到阈值
/// </summary>
public sealed class TriggerCond_经过时间 : ITriggerCond
{
    private readonly int _timeMs;

    public TriggerCond_经过时间(int timeMs)
    {
        _timeMs = timeMs;
    }

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        return ExecutionAxis.Instance.BattleTimeMs >= _timeMs;
    }
}
