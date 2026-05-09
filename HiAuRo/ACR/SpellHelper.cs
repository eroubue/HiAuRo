using OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds;
using OmenTools.OmenService;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace HiAuRo.ACR;

/// <summary>
/// 技能辅助 —— 冷却 / 充能 / 距离 / 资源 综合判断
/// </summary>
public static class SpellHelper
{
    /// <summary>技能可用：冷却转好了吗</summary>
    public static bool CanUseSpell(uint id)
    {
        return UseActionManager.Instance().IsActionOffCooldown(ActionType.Action, id);
    }

    /// <summary>综合就绪检查（CD + MP + 目标 + 解锁等），直接读游戏原生 GetActionStatus</summary>
    public static unsafe bool IsActionReady(uint actionId, ulong targetId = 0xE000_0000)
        => ActionManager.Instance()->GetActionStatus(ActionType.Action, actionId, targetId, true, true, null) <= 1;

    /// <summary>技能剩余冷却毫秒（0 表示已冷却好）</summary>
    public static unsafe float GetCooldownRemaining(uint spellId)
    {
        var recastGroup = ActionManager.Instance()->GetRecastGroup((int)ActionType.Action, spellId);
        var detail = ActionManager.Instance()->GetRecastGroupDetail(recastGroup);
        if (detail == null || !detail->IsActive) return 0;
        return Math.Max(0, detail->Total - detail->Elapsed) * 1000f;
    }

    /// <summary>充能技能最大层数</summary>
    public static unsafe int GetMaxCharges(uint spellId)
        => ActionManager.GetMaxCharges(spellId, 0);

    /// <summary>充能技能当前层数</summary>
    public static unsafe int GetCharges(uint spellId)
        => (int)ActionManager.Instance()->GetCurrentCharges(spellId);

    /// <summary>充能技能距下次充能的毫秒数</summary>
    public static unsafe float GetChargeCooldown(uint spellId)
    {
        var currentCharges = ActionManager.Instance()->GetCurrentCharges(spellId);
        var maxCharges = ActionManager.GetMaxCharges(spellId, 0);
        if (currentCharges >= maxCharges) return 0;
        return GetCooldownRemaining(spellId);
    }

    /// <summary>目标是否在技能射程内</summary>
    public static bool IsInRange(uint id, IGameObject? target)
    {
        if (target == null) return false;
        var distance = Data.Me.DistanceToObject2D(target);
        var range = GetActionRow(id)?.Range ?? 25f;
        return distance <= range;
    }

    /// <summary>从 Lumina Action 表中获取技能行数据</summary>
    public static unsafe Lumina.Excel.Sheets.Action? GetActionRow(uint id)
        => DService.Instance().Data.GetExcelSheet<Lumina.Excel.Sheets.Action>()?.GetRow(id);
}
