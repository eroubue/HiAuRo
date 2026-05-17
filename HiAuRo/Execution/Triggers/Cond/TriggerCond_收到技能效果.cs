using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 触发条件参数 —— 收到技能效果（暂存，等待 ActionEffect Hook 基础设施）
/// </summary>
public sealed class TriggerCondParams_收到技能效果 : ITriggerCondParams
{
    /// <summary>收到的技能效果 ID</summary>
    public uint SpellId;
}

/// <summary>
/// 检测是否收到特定的技能效果（暂存，等待 ActionEffect Hook 基础设施）
/// </summary>
[TriggerDisplay("收到技能效果", "检测是否收到指定技能效果")]
[TriggerTypeName("TriggerCondReceviceAbilityEffect")]
public sealed class TriggerCond_收到技能效果 : ITriggerCond
{
    private readonly uint _spellId;

    /// <param name="spellId">技能 ID</param>
    public TriggerCond_收到技能效果(uint spellId)
    {
        _spellId = spellId;
    }

    /// <summary>检测是否收到特定技能效果</summary>
    public bool Handle(ITriggerCondParams? condParams = null)
    {
        // 需要 ActionEffect Hook，暂未实现
        return false;
    }
}
