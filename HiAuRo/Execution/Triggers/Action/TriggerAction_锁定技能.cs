using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Action;

/// <summary>
/// 锁定/解锁指定技能的使用（Phase 6+ 暂存 force spell 优先级的扩展点）
/// 当前实现：锁定=设置 ExecutionAxis 的 ForceSpell 为 Idle 空转技能
/// </summary>
public sealed class TriggerAction_锁定技能 : ITriggerAction
{
    private readonly uint _spellId;
    private readonly bool _locked;

    /// <param name="spellId">要锁定的技能 ID</param>
    /// <param name="locked">true=锁定（强制不释放）, false=解锁</param>
    public TriggerAction_锁定技能(uint spellId, bool locked)
    {
        _spellId = spellId;
        _locked = locked;
    }

    public bool Handle()
    {
        // 锁定技能在 ACR 层面的实现：将 force spell 设为 Idle 表示本帧不可用
        // 解锁：清除 force spell 让正常循环恢复
        if (_locked)
        {
            ExecutionAxis.Instance.SetForceSpell(null!);
        }
        else
        {
            ExecutionAxis.Instance.CurrentOutput.ForceSpell = null;
        }
        return true;
    }
}
