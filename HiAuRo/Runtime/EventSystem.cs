using OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds;
using OmenTools.OmenService;
using FFXIVClientStructs.FFXIV.Client.Game;

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

    public static void Init()
    {
        if (_initialized) return;

        UseActionManager.Instance().RegPreUseAction(OnPreUseAction);
        UseActionManager.Instance().RegPostUseAction(OnPostUseAction);

        _initialized = true;
    }

    public static void Shutdown()
    {
        _initialized = false;

        UseActionManager.Instance().Unreg(OnPreUseAction);
        UseActionManager.Instance().Unreg(OnPostUseAction);

        _onActionUsedHandlers.Clear();
        _onActionCompletedHandlers.Clear();
        _onTargetChangedHandlers.Clear();
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
        if (result)
        {
            LastComboSpellId = LastCompletedActionId;
            LastCompletedActionId = actionId;
            HiAuRo.ACR.SpellHistoryHelper.RecordSpell(actionId);
            foreach (var handler in _onActionCompletedHandlers)
            {
                try { handler(actionId); }
                catch (Exception ex) { DService.Instance().Log.Error($"[EventSystem] OnActionCompleted handler 异常: {ex}"); }
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
