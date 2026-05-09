using OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds;
using OmenTools.OmenService;
using FFXIVClientStructs.FFXIV.Client.Game;
using HiAuRo.Execution;
using HiAuRo.Execution.Events;

namespace HiAuRo.Runtime;

/// <summary>
/// 底层游戏事件监听分发
/// </summary>
public static class EventSystem
{
    private static readonly List<Action<uint, ulong>> _onActionUsedHandlers = [];
    private static readonly List<Action<uint>> _onActionCompletedHandlers = [];
    private static readonly List<Action<IGameObject?>> _onTargetChangedHandlers = [];

    private static IGameObject? _lastTarget;
    private static bool _initialized;

    /// <summary>最近一次成功执行的技能 ID（供触发条件使用）</summary>
    public static uint LastCompletedActionId { get; private set; }

    /// <summary>上一次成功执行的技能 ID（连击系统用）</summary>
    public static uint LastComboSpellId { get; private set; }

    /// <summary>去重：自记录 ID 集合，防止 OnPostUseAction 同 ID 二次覆盖</summary>
    private static readonly HashSet<uint> _selfRecordedIds = [];

    /// <summary>SlotExecutor 自行调用的记录入口（绕过 OmenTools Hook）</summary>
    public static void OnUseActionSuccess(uint actionId, HiAuRo.ACR.SpellType spellType)
    {
        _selfRecordedIds.Add(actionId);
        if (_selfRecordedIds.Count > 8)
            _selfRecordedIds.Remove(_selfRecordedIds.Min());

        LastCompletedActionId = actionId;
        HiAuRo.ACR.SpellHistoryHelper.RecordSpell(actionId);
        if (spellType is HiAuRo.ACR.SpellType.RealGcd or HiAuRo.ACR.SpellType.GeneralGcd)
        {
            HiAuRo.ACR.SpellHistoryHelper.RecordGcd();
        }
        DService.Instance().Log.Debug($"[EventSystem] 自行记录: id={actionId} LastCompleted={LastCompletedActionId}");
    }

    public static void Init()
    {
        if (_initialized) return;

        UseActionManager.Instance().RegPreUseAction(OnPreUseAction);
        UseActionManager.Instance().RegPostUseAction(OnPostUseAction);

        _initialized = true;
    }

    public static void Shutdown()
    {
        UseActionManager.Instance().Unreg(OnPreUseAction);
        UseActionManager.Instance().Unreg(OnPostUseAction);

        _onActionUsedHandlers.Clear();
        _onActionCompletedHandlers.Clear();
        _onTargetChangedHandlers.Clear();

        _initialized = false;
    }

    public static void CheckTargetChanged()
    {
        var currentTarget = TargetManager.Target;
        if (_lastTarget?.EntityID != currentTarget?.EntityID)
        {
            _lastTarget = currentTarget;
            foreach (var handler in _onTargetChangedHandlers)
                handler(currentTarget);
        }
    }

    #region 注册 / 注销

    public static void RegisterOnActionUsed(Action<uint, ulong> handler) =>
        _onActionUsedHandlers.Add(handler);

    public static void UnregisterOnActionUsed(Action<uint, ulong> handler) =>
        _onActionUsedHandlers.Remove(handler);

    public static void RegisterOnActionCompleted(Action<uint> handler) =>
        _onActionCompletedHandlers.Add(handler);

    public static void UnregisterOnActionCompleted(Action<uint> handler) =>
        _onActionCompletedHandlers.Remove(handler);

    public static void RegisterOnTargetChanged(Action<IGameObject?> handler) =>
        _onTargetChangedHandlers.Add(handler);

    public static void UnregisterOnTargetChanged(Action<IGameObject?> handler) =>
        _onTargetChangedHandlers.Remove(handler);

    #endregion

    #region Hook 回调

    private static void OnPreUseAction(
        ref bool isPrevented, ref ActionType actionType, ref uint actionId, ref ulong targetId,
        ref uint extraParam, ref ActionManager.UseActionMode queueState, ref uint comboRouteId)
    {
    }

    private static void OnPostUseAction(
        bool result, ActionType actionType, uint actionId, ulong targetId,
        uint extraParam, ActionManager.UseActionMode queueState, uint comboRouteId)
    {
        DService.Instance().Log.Debug($"[EventSystem] OnPostUseAction: result={result} actionId={actionId} targetId={targetId:X} comboRouteId={comboRouteId}");

        if (result)
        {
            // 去重：已通过 OnUseActionSuccess 自行记录过的 ID 不再更新 Combo/History
            if (_selfRecordedIds.Remove(actionId))
            {
                DService.Instance().Log.Debug($"[EventSystem] OnPostUseAction: id={actionId} 已自行记录, 跳过状态更新");
            }
            else
            {
                LastComboSpellId = LastCompletedActionId;
                LastCompletedActionId = actionId;
                HiAuRo.ACR.SpellHistoryHelper.RecordSpell(actionId);
            }

            DService.Instance().Log.Information($"[EventSystem] 技能成功: id={actionId} type={actionType} target={targetId:X} LastCombo={LastComboSpellId} LastCompleted={LastCompletedActionId}");
            foreach (var handler in _onActionCompletedHandlers)
            {
                try { handler(actionId); }
                catch (Exception ex) { DService.Instance().Log.Error($"[EventSystem] OnActionCompleted handler 异常: {ex}"); }
            }

            // 通知执行轴：技能执行成功事件
            try
            {
                ExecutionAxis.Instance.UseCondParams(
                    new AfterSpellParams { SpellID = actionId });
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Error($"[EventSystem] UseCondParams 异常: {ex}");
            }
        }

        foreach (var handler in _onActionUsedHandlers)
        {
            try { handler(actionId, targetId); }
            catch (Exception ex) { DService.Instance().Log.Error($"[EventSystem] OnActionUsed handler 异常: {ex}"); }
        }
    }

    #endregion
}
