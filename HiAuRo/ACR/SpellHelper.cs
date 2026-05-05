using OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds;
using OmenTools.OmenService;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace HiAuRo.ACR;

/// <summary>
/// 技能辅助 —— 冷却 / 距离 / 资源 综合判断
/// </summary>
public static class SpellHelper
{
    /// <summary>综合判断技能是否可用</summary>
    public static bool CanUseSpell(uint id)
    {
        return UseActionManager.Instance().IsActionOffCooldown(ActionType.Action, id);
    }

    /// <summary>技能剩余冷却毫秒（0 表示已冷却好）</summary>
    public static unsafe float GetSpellCooldown(uint id)
    {
        var am = ActionManager.Instance();
        if (am == null) return 0;

        var recastGroup = am->GetRecastGroup((int)ActionType.Action, id);
        var detail = am->GetRecastGroupDetail(recastGroup);
        if (detail == null || !detail->IsActive) return 0;
        return Math.Max(0, detail->Total - detail->Elapsed) * 1000f;
    }

    /// <summary>目标是否在技能射程内</summary>
    public static bool IsInRange(uint id, IGameObject? target)
    {
        if (target == null) return false;
        var distance = Data.Me.DistanceToObject2D(target);

        // 从 Lumina 查询技能实际射程
        var actionRow = DService.Instance().Data.GetExcelSheet<Lumina.Excel.Sheets.Action>()?.GetRow(id);
        var range = actionRow?.Range ?? 25f;
        return distance <= range;
    }

    /// <summary>充能技能当前层数</summary>
    public static unsafe int GetCharges(uint id)
    {
        var am = ActionManager.Instance();
        if (am == null) return 0;
        return (int)am->GetCurrentCharges(id);
    }

    /// <summary>从 Lumina Action 表中获取技能行数据</summary>
    public static unsafe Lumina.Excel.Sheets.Action? GetActionRow(uint id)
        => DService.Instance().Data.GetExcelSheet<Lumina.Excel.Sheets.Action>()?.GetRow(id);
}
