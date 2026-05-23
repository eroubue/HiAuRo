using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 检测战斗经过时间是否达到阈值
/// </summary>
[TriggerDisplay("经过时间", "检测战斗开始后经过的时间")]
[TriggerTypeName("TriggerCondAfterBattleStart")]
public sealed class TriggerCond_经过时间 : ITriggerCond
{
    private readonly int _timeMs;

    /// <param name="timeMs">经过时间（毫秒）阈值</param>
    public TriggerCond_经过时间(int timeMs)
    {
        _timeMs = timeMs;
    }

    /// <summary>检测战斗经过时间是否达到阈值</summary>
    public bool Handle(ITriggerCondParams? condParams = null)
    {
        return ExecutionAxis.Instance.BattleTimeMs >= _timeMs;
    }
}
