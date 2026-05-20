using HiAuRo.ACR;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 检测在回溯时间窗口内自身是否被指定连线过
/// </summary>
[TriggerDisplay("最近连线", "检测在指定时间窗口内自身是否被连线过")]
[TriggerTypeName("TriggerCondCheckRecentlyTether")]
public sealed class TriggerCond_最近连线 : ITriggerCond
{
    private readonly uint _tetherId;
    private readonly float _checkTimeSec;

    /// <param name="tetherId">连线 ID</param>
    /// <param name="checkTimeSec">回溯时间窗口（秒）</param>
    public TriggerCond_最近连线(uint tetherId, float checkTimeSec = 3f)
    {
        _tetherId = tetherId;
        _checkTimeSec = checkTimeSec;
    }

    /// <summary>检测自身是否在时间窗口内被指定连线过</summary>
    public bool Handle(ITriggerCondParams? condParams = null)
    {
        var selfId = Me.Object?.EntityID ?? 0;
        if (selfId == 0) return false;

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var cutoffMs = nowMs - (long)(_checkTimeSec * 1000);

        return BattleData.GetRecentTethers().Any(e =>
            e.TimestampMs >= cutoffMs &&
            e.TetherId == _tetherId &&
            e.TargetId == selfId);
    }
}
