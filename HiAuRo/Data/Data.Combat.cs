using OmenTools.OmenService;
using Dalamud.Game.ClientState.Conditions;

namespace HiAuRo.Data;

/// <summary>
/// 战斗状态数据 —— 转发 GameState.* + Condition.*
/// </summary>
public static class Combat
{
    public static bool IsLoggedIn => GameState.IsLoggedIn;

    public static bool IsInInstanceArea => GameState.IsInInstanceArea;

    public static bool IsInPVPArea => GameState.IsInPVPArea;

    public static uint TerritoryType => GameState.TerritoryType;

    public static uint Map => GameState.Map;

    public static float DeltaTime => GameState.DeltaTime;

    public static long ServerTimeUnix => GameState.ServerTimeUnix;

    public static bool InCombat =>
        DService.Instance().Condition is { } cond && cond[ConditionFlag.InCombat];

    public static bool IsCasting =>
        DService.Instance().Condition is { } cond && cond[ConditionFlag.Casting];

    public static bool IsBoundByDuty =>
        DService.Instance().Condition is { } cond && cond[ConditionFlag.BoundByDuty];

    public static bool IsOnMount =>
        DService.Instance().Condition is { } cond && cond[ConditionFlag.Mounted];

    public static bool IsBetweenAreas =>
        DService.Instance().Condition is { } cond && cond[ConditionFlag.BetweenAreas];
}
