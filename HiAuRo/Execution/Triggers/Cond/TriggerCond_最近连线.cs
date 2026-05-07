using HiAuRo.ACR;
using HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

[TriggerDisplay("最近连线", "检测在指定时间窗口内自身是否被连线过")]
[TriggerTypeName("TriggerCondCheckRecentlyTether")]

/// <summary>
/// 检测在回溯时间窗口内自身是否被指定连线过
/// </summary>
public sealed class TriggerCond_最近连线 : ITriggerCond
{
    private readonly uint _tetherId;
    private readonly float _checkTimeSec;

    public TriggerCond_最近连线(uint tetherId, float checkTimeSec = 3f)
    {
        _tetherId = tetherId;
        _checkTimeSec = checkTimeSec;
    }

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        var selfId = Me.Object?.EntityId ?? 0;
        if (selfId == 0) return false;

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var cutoffMs = nowMs - (long)(_checkTimeSec * 1000);

        return BattleData.RecentTethers.Any(e =>
            e.TimestampMs >= cutoffMs &&
            e.TetherId == _tetherId &&
            e.TargetId == selfId);
    }
}
