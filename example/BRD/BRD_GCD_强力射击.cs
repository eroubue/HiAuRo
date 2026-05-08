using HiAuRo.ACR;
using static HiAuRo.ACR.SpellsDefine;

namespace HiAuRo.Jobs.BRD;

/// <summary>
/// 强力射击 — GCD 槽位解析器
/// </summary>
public sealed class BRD_GCD_强力射击 : ISlotResolver
{
    public int Check()
    {
        if (!SpellHelper.CanUseSpell(强力射击)) return -1;
        if (Data.Combat.IsCasting) return -1;
        return 97;
    }

    public void Build(Slot slot)
    {
        slot.Add(new Spell
        {
            Id = 强力射击,
            Name = "强力射击",
            TargetType = SpellTargetType.Target,
            Type = SpellType.RealGcd
        });
    }
}
