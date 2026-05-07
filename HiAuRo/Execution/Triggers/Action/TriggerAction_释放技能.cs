using HiAuRo.ACR;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace HiAuRo.Execution.Triggers.Action;

[TriggerDisplay("释放技能", "强制释放指定技能")]
[TriggerTypeName("TriggerActionCastSpell")]
/// <summary>
/// 强制释放指定技能 —— 将技能高优先级插入执行队列
/// </summary>
public sealed class TriggerAction_释放技能 : ITriggerAction
{
    private readonly uint _spellId;
    private readonly SpellTargetType _targetType;

    public TriggerAction_释放技能(uint spellId, SpellTargetType targetType = SpellTargetType.Target)
    {
        _spellId = spellId;
        _targetType = targetType;
    }

    public bool Handle()
    {
        // 构建高优先 Spell 并标记为执行轴输出
        ExecutionAxis.Instance.SetForceSpell(new Spell
        {
            Id = _spellId,
            Name = $"执行轴_{_spellId}",
            TargetType = _targetType,
            Type = SpellType.Ability
        });

        return true;
    }
}
