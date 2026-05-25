using HiAuRo.ACR;
using HiAuRo.Execution.Events;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

[TriggerDisplay("收到技能效果", "检测是否收到指定技能效果")]
[TriggerTypeName("TriggerCondReceviceAbilityEffect")]
public sealed class TriggerCond_收到技能效果 : ITriggerCond
{
    public uint SpellId { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        if (condParams is ActionEffectParams ae)
            return ae.ActionID == SpellId;

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var cutoffMs = nowMs - 3000;
        return BattleData.GetRecentActionEffects().Any(e =>
            e.TimestampMs >= cutoffMs && e.ActionId == SpellId);
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("SpellId", (int)SpellId);
    }
}
