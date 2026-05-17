using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Action;

/// <summary>
/// 将技能以高优先级插入执行队列（跳过 ACR 正常优先级的 slot 调度）
/// </summary>
[TriggerDisplay("高优Slot", "高优先级技能槽执行")]
[TriggerTypeName("TriggerActionHighPrioritySlot")]
public sealed class TriggerAction_高优Slot : ITriggerAction
{
    private readonly uint _spellId;
    private readonly SpellTargetType _targetType;

    /// <param name="spellId">技能 ID</param>
    /// <param name="targetType">目标类型</param>
    public TriggerAction_高优Slot(uint spellId, SpellTargetType targetType = SpellTargetType.Target)
    {
        _spellId = spellId;
        _targetType = targetType;
    }

    /// <summary>设置高优技能强制释放</summary>
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
