using HiAuRo.ACR;
using HiAuRo.Runtime;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 触发条件参数 —— 技能后
/// </summary>
public sealed class TriggerCondParams_技能后 : ITriggerCondParams
{
    /// <summary>刚使用过的技能 ID</summary>
    public uint SpellId;
}

[TriggerDisplay("技能后", "检测指定技能使用后")]
[TriggerTypeName("TriggerCondAfterSpell")]

/// <summary>
/// 检测自己是否刚用过指定技能（最近一次 Completed 技能）
/// </summary>
public sealed class TriggerCond_技能后 : ITriggerCond
{
    private readonly uint _spellId;

    public TriggerCond_技能后(uint spellId)
    {
        _spellId = spellId;
    }

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        return EventSystem.LastCompletedActionId == _spellId;
    }
}
