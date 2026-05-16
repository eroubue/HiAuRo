using OmenTools.OmenService;
using static HiAuRo.Data;
using HiAuRo.Execution;
using HiAuRo.Execution.Events;

namespace HiAuRo.Runtime;

/// <summary>
/// 战斗上下文状态机
/// </summary>
public static class CombatContext
{
    public enum State
    {
        /// <summary>未初始化 / 未登录</summary>
        Idle,
        /// <summary>战斗中</summary>
        InCombat,
        /// <summary>脱战中</summary>
        OutOfCombat,
        /// <summary>切图中</summary>
        Zoning
    }

    public static State CurrentState { get; private set; } = State.Idle;

    public static bool IsInCombat => CurrentState == State.InCombat;

    /// <summary>状态变更事件</summary>
    public static event Action<State, State>? StateChanged;

    private static bool _wasInCombat;
    private static bool _wasBetweenAreas = true;
    private static bool _initialized;

    public static void Check()
    {
        if (!GameState.IsLoggedIn)
        {
            TransitionTo(State.Idle);
            _initialized = false;
            return;
        }

        if (!_initialized)
        {
            _wasInCombat = Combat.InCombat;
            _wasBetweenAreas = Combat.IsBetweenAreas;
            _initialized = true;
        }

        var nowInCombat = Combat.InCombat;
        var nowBetweenAreas = Combat.IsBetweenAreas;

        if (nowBetweenAreas)
        {
            if (!_wasBetweenAreas)
                TransitionTo(State.Zoning);
            _wasBetweenAreas = true;
        }
        else if (nowInCombat && !_wasInCombat)
        {
            TransitionTo(State.InCombat);
        }
        else if (!nowInCombat && _wasInCombat)
        {
            TransitionTo(State.OutOfCombat);
        }

        _wasInCombat = nowInCombat;
        _wasBetweenAreas = nowBetweenAreas;
    }

    public static void Reset()
    {
        if (CurrentState != State.Idle)
            TransitionTo(State.Idle);
        _initialized = false;
    }

    private static void TransitionTo(State newState)
    {
        if (CurrentState == newState) return;

        var oldState = CurrentState;
        CurrentState = newState;

        StateChanged?.Invoke(oldState, newState);

        // 通知执行轴：战斗状态变化事件
        if (newState == State.InCombat)
            ExecutionAxis.Instance.UseCondParams(
                new CombatStateParams { IsEntering = true });
        else if (newState == State.OutOfCombat)
            ExecutionAxis.Instance.UseCondParams(
                new CombatStateParams { IsEntering = false });
    }
}
