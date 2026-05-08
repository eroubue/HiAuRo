using OmenTools.OmenService;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace HiAuRo.ACR;

/// <summary>
/// 冷却辅助 —— 技能冷却 / 充能冷却判断
/// </summary>
public static class CooldownHelper
{
    /// <summary>技能是否在冷却中</summary>
    public static bool IsOnCooldown(uint spellId)
    {
        return !UseActionManager.Instance().IsActionOffCooldown(ActionType.Action, spellId);
    }

    /// <summary>技能剩余冷却毫秒数（冷却好返回 0）</summary>
    public static unsafe float GetCooldownRemaining(uint spellId)
    {
        var am = ActionManager.Instance();
        if (am == null) return 0;

        var recastGroup = am->GetRecastGroup((int)ActionType.Action, spellId);
        var detail = am->GetRecastGroupDetail(recastGroup);
        if (detail == null || !detail->IsActive) return 0;
        return Math.Max(0, detail->Total - detail->Elapsed) * 1000f;
    }

    /// <summary>充能技能最大层数</summary>
    public static unsafe int GetMaxCharges(uint spellId)
    {
        var am = ActionManager.Instance();
        if (am == null) return 1;
        return ActionManager.GetMaxCharges(spellId, 0);
    }

    /// <summary>充能技能当前层数</summary>
    public static unsafe int GetCharges(uint spellId)
    {
        var am = ActionManager.Instance();
        if (am == null) return 0;
        return (int)am->GetCurrentCharges(spellId);
    }

    /// <summary>充能技能距下次充能的毫秒数</summary>
    public static unsafe float GetChargeCooldown(uint spellId)
    {
        var am = ActionManager.Instance();
        if (am == null) return 0;

        var currentCharges = am->GetCurrentCharges(spellId);
        var maxCharges = ActionManager.GetMaxCharges(spellId, 0);
        if (currentCharges >= maxCharges) return 0;

        return GetCooldownRemaining(spellId);
    }
}
