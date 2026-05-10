using OmenTools.OmenService;
using Dalamud.Game.ClientState.Conditions;

namespace HiAuRo.Data;

/// <summary>
/// 战斗状态数据 —— 转发 GameState.* + Condition.*
/// </summary>
public static class Combat
{
    /// <summary>当前 GCD 窗口已使用的能力技数量（框架内部设置，ACR 只读）</summary>
    public static int AbilityCountInGcd { get; internal set; }

    /// <summary>当前 GCD 窗口能力技上限（ACR 可读写，框架仅在生命周期事件时重置为 PluginConfig 默认值）</summary>
    public static int MaxAbilityTimesInGcd { get; set; } = 2;

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
