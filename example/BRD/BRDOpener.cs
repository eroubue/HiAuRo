using HiAuRo.ACR;
using static HiAuRo.ACR.SpellsDefine;

namespace HiAuRo.Jobs.BRD;

/// <summary>
/// 诗人最简起手示例
/// </summary>
public sealed class BRDOpener : IOpener
{
    public uint Level => 1;

    public List<Action<Slot>> Sequence { get; } =
    [
        slot => slot.Add(new Spell { Id = 猛者强击, Name = "猛者强击", TargetType = SpellTargetType.Self, Type = SpellType.Ability }),
        slot =>
        {
            slot.Add(new Spell { Id = 强力射击, Name = "强力射击", TargetType = SpellTargetType.Target, Type = SpellType.RealGcd });
            slot.Add2NdWindowAbility(new Spell { Id = 失血箭, Name = "失血箭", TargetType = SpellTargetType.Target, Type = SpellType.Ability });
        },
    ];

    public int StartCheck() => 0;
    public int StopCheck(int index) => 0;
    public void InitCountDown(Runtime.CountDownHandler handler) { }
}
