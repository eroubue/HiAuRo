using HiAuRo.ACR;
using HiAuRo.Execution.Events;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 检测是否收到特定的技能效果（任意目标收到指定 ActionId 的效果）
/// 事件驱动：匹配 ActionEffectParams；轮询：查询 BattleData 近期行动效果历史
/// </summary>
[TriggerDisplay("收到技能效果", "检测是否收到指定技能效果")]
[TriggerTypeName("TriggerCondReceviceAbilityEffect")]
public sealed class TriggerCond_收到技能效果 : ITriggerCond
{
    private readonly uint _spellId;

    /// <param name="spellId">技能 ID</param>
    public TriggerCond_收到技能效果(uint spellId)
    {
        _spellId = spellId;
    }

    /// <summary>检测是否收到特定技能效果</summary>
    public bool Handle(ITriggerCondParams? condParams = null)
    {
        if (condParams is ActionEffectParams ae)
            return ae.ActionID == _spellId;

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var cutoffMs = nowMs - 3000;
        return BattleData.GetRecentActionEffects().Any(e =>
            e.TimestampMs >= cutoffMs && e.ActionId == _spellId);
    }
}
