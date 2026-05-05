using HiAuRo.ACR;
using HiAuRo.Runtime;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 触发条件参数 —— 上次技能
/// </summary>
public sealed class TriggerCondParams_上次技能 : ITriggerCondParams
{
    /// <summary>最近一次使用的技能 ID</summary>
    public uint SpellId;
}

/// <summary>
/// 检测最近一次成功执行的技能
/// </summary>
public sealed class TriggerCond_上次技能 : ITriggerCond
{
    private readonly uint _spellId;

    public TriggerCond_上次技能(uint spellId)
    {
        _spellId = spellId;
    }

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        return EventSystem.LastCompletedActionId == _spellId;
    }
}
