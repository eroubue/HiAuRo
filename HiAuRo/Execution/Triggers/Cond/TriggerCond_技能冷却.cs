using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 触发条件参数 —— 技能冷却
/// </summary>
public sealed class TriggerCondParams_技能冷却 : ITriggerCondParams
{
    /// <summary>技能 ID</summary>
    public uint SpellId;
    /// <summary>冷却剩余毫秒阈值（小于等于此值时触发）</summary>
    public int RemainingMs;
}

/// <summary>
/// 检测指定技能的冷却剩余是否在阈值内
/// </summary>
[TriggerDisplay("技能冷却", "检测技能冷却剩余时间")]
[TriggerTypeName("TriggerCondCheckSpellCd")]
public sealed class TriggerCond_技能冷却 : ITriggerCond
{
    private readonly uint _spellId;
    private readonly int _remainingMs;

    /// <param name="spellId">技能 ID</param>
    /// <param name="remainingMs">冷却剩余毫秒阈值</param>
    public TriggerCond_技能冷却(uint spellId, int remainingMs = 500)
    {
        _spellId = spellId;
        _remainingMs = remainingMs;
    }

    /// <summary>检测技能冷却剩余是否在阈值内</summary>
    public bool Handle(ITriggerCondParams? condParams = null)
    {
        var cd = SpellHelper.GetCooldownRemaining(_spellId);
        return cd >= 0 && cd <= _remainingMs;
    }
}
