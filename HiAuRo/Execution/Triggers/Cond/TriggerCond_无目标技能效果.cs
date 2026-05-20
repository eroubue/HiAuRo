using HiAuRo.ACR;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 检测在回溯时间窗口内是否出现无目标的技能效果（如 Boss 地面 AoE 反馈效果）
/// </summary>
[TriggerDisplay("无目标技能效果", "检测在指定时间窗口内是否出现无目标技能效果（地面 AoE 等）")]
[TriggerTypeName("TriggerCondReceviceNoTargetAbilityEffect")]
public sealed class TriggerCond_无目标技能效果 : ITriggerCond
{
    private readonly uint _actionId;
    private readonly float _checkTimeSec;

    /// <param name="actionId">技能 ID</param>
    /// <param name="checkTimeSec">回溯时间窗口（秒）</param>
    public TriggerCond_无目标技能效果(uint actionId, float checkTimeSec = 3f)
    {
        _actionId = actionId;
        _checkTimeSec = checkTimeSec;
    }

    /// <summary>检测是否出现无目标技能效果</summary>
    public bool Handle(ITriggerCondParams? condParams = null)
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var cutoffMs = nowMs - (long)(_checkTimeSec * 1000);

        return BattleData.GetRecentActionEffects().Any(e =>
            e.TimestampMs >= cutoffMs &&
            e.ActionId == _actionId &&
            e.TargetId == 0xE0000000);
    }
}
