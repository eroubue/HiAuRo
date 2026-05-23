using HiAuRo.ACR;
using HiAuRo.Runtime;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 检测自己是否刚用过指定技能（最近一次 Completed 技能）
/// </summary>
[TriggerDisplay("技能后", "检测指定技能使用后")]
[TriggerTypeName("TriggerCondAfterSpell")]
public sealed class TriggerCond_技能后 : ITriggerCond
{
    private readonly uint _spellId;

    /// <param name="spellId">技能 ID</param>
    public TriggerCond_技能后(uint spellId)
    {
        _spellId = spellId;
    }

    /// <summary>检测是否刚用过指定技能</summary>
    public bool Handle(ITriggerCondParams? condParams = null)
    {
        return EventSystem.LastCompletedActionId == _spellId;
    }
}
