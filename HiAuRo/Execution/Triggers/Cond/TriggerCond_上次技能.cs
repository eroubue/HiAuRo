using HiAuRo.ACR;
using HiAuRo.Runtime;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 检测最近一次成功执行的技能
/// </summary>
[TriggerDisplay("上次技能", "检测上次使用的技能是否为指定技能")]
[TriggerTypeName("TriggerCondCheckLastSpell")]
public sealed class TriggerCond_上次技能 : ITriggerCond
{
    private readonly uint _spellId;

    /// <param name="spellId">技能 ID</param>
    public TriggerCond_上次技能(uint spellId)
    {
        _spellId = spellId;
    }

    /// <summary>检测上次使用的技能是否为指定技能</summary>
    public bool Handle(ITriggerCondParams? condParams = null)
    {
        return EventSystem.LastCompletedActionId == _spellId;
    }
}
