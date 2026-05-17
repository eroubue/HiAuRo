using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Action;

/// <summary>
/// 将指定技能加入法术队列，在下一个 GCD 窗口插入执行
/// </summary>
[TriggerDisplay("技能队列", "将技能加入执行队列")]
[TriggerTypeName("TriggerActionSpellQueue")]
public sealed class TriggerAction_技能队列 : ITriggerAction
{
    private readonly uint _spellId;
    private readonly SpellTargetType _targetType;

    /// <param name="spellId">技能 ID</param>
    /// <param name="targetType">目标类型</param>
    public TriggerAction_技能队列(uint spellId, SpellTargetType targetType = SpellTargetType.Target)
    {
        _spellId = spellId;
        _targetType = targetType;
    }

    /// <summary>将技能加入执行队列</summary>
    public bool Handle()
    {
        var spell = new Spell
        {
            Id = _spellId,
            Name = $"队列_{_spellId}",
            TargetType = _targetType,
            Type = SpellType.Ability
        };

        var slot = new Slot();
        slot.Add(spell);
        ExecutionAxis.Instance.CurrentOutput.ForceSpell = spell;
        return true;
    }
}
