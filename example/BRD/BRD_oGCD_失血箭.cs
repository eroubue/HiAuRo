using HiAuRo.ACR;
using static HiAuRo.ACR.SpellsDefine;

namespace HiAuRo.Jobs.BRD;

/// <summary>
/// 失血箭 — oGCD 槽位解析器
/// </summary>
public sealed class BRD_oGCD_失血箭 : ISlotResolver
{
    public int Check()
    {
        if (CooldownHelper.IsOnCooldown(失血箭)) return -1;
        return 110;
    }

    public void Build(Slot slot)
    {
        slot.Add(new Spell
        {
            Id = 失血箭,
            Name = "失血箭",
            TargetType = SpellTargetType.Target,
            Type = SpellType.Ability
        });
    }
}
