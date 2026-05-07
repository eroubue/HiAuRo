using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Action;

[TriggerDisplay("高优Slot", "高优先级技能槽执行")]
[TriggerTypeName("TriggerActionHighPrioritySlot")]
/// <summary>
/// 将技能以高优先级插入执行队列（跳过 ACR 正常优先级的 slot 调度）
/// </summary>
public sealed class TriggerAction_高优Slot : ITriggerAction
{
    private readonly uint _spellId;
    private readonly SpellTargetType _targetType;

    public TriggerAction_高优Slot(uint spellId, SpellTargetType targetType = SpellTargetType.Target)
    {
        _spellId = spellId;
        _targetType = targetType;
    }

    public bool Handle()
    {
        ExecutionAxis.Instance.SetForceSpell(new Spell
        {
            Id = _spellId,
            Name = $"高优_{_spellId}",
            TargetType = _targetType,
            Type = SpellType.Ability
        });
        return true;
    }
}
