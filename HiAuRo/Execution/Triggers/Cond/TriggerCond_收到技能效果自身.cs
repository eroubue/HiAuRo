using HiAuRo.ACR;
using HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

[TriggerDisplay("收到技能效果自身", "检测在指定时间窗口内自身是否收到过指定技能效果")]
[TriggerTypeName("TriggerCondCheckAbilityEffect")]

/// <summary>
/// 检测自身在回溯时间窗口内是否收到过指定技能效果（基于历史记录而非事件触发）
/// </summary>
public sealed class TriggerCond_收到技能效果自身 : ITriggerCond
{
    private readonly uint _actionId;
    private readonly float _checkTimeSec;

    public TriggerCond_收到技能效果自身(uint actionId, float checkTimeSec = 3f)
    {
        _actionId = actionId;
        _checkTimeSec = checkTimeSec;
    }

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        var selfId = Me.Object?.EntityID ?? 0;
        if (selfId == 0) return false;

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var cutoffMs = nowMs - (long)(_checkTimeSec * 1000);

        return BattleData.RecentActionEffects.Any(e =>
            e.TimestampMs >= cutoffMs &&
            e.ActionId == _actionId &&
            e.TargetId == selfId);
    }
}
