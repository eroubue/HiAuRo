using System.Numerics;

namespace HiAuRo.Data;

/// <summary>
/// 战斗事件历史缓冲区 —— 为 TriggerCond 提供近期事件查询
/// 通过 OmenTools GamePacketManager 订阅游戏事件包填充数据
/// </summary>
public static class BattleData
{
    public record TetherEvent(uint TetherId, uint SourceId, uint TargetId, long TimestampMs);
    public record ActionEffectEvent(uint ActionId, uint CasterId, ulong TargetId, long TimestampMs);
    public record MapEffectEvent(uint EffectId, Vector3 Position, long TimestampMs);

    public static readonly List<TetherEvent> RecentTethers = [];
    public static readonly List<ActionEffectEvent> RecentActionEffects = [];
    public static readonly List<MapEffectEvent> RecentMapEffects = [];

    private const long MaxAgeMs = 30_000;

    /// <summary>清除超过 MaxAgeMs 的过期事件</summary>
    public static void PruneExpired(long nowMs)
    {
        var cutoff = nowMs - MaxAgeMs;
        RecentTethers.RemoveAll(e => e.TimestampMs < cutoff);
        RecentActionEffects.RemoveAll(e => e.TimestampMs < cutoff);
        RecentMapEffects.RemoveAll(e => e.TimestampMs < cutoff);
    }

    /// <summary>注册连线事件</summary>
    public static void OnTetherAdded(uint tetherId, uint sourceId, uint targetId, long nowMs)
    {
        PruneExpired(nowMs);
        RecentTethers.Add(new TetherEvent(tetherId, sourceId, targetId, nowMs));
    }

    /// <summary>注册行动效果事件</summary>
    public static void OnActionEffect(uint actionId, uint casterId, ulong targetId, long nowMs)
    {
        PruneExpired(nowMs);
        RecentActionEffects.Add(new ActionEffectEvent(actionId, casterId, targetId, nowMs));
    }

    /// <summary>注册地图特效事件</summary>
    public static void OnMapEffect(uint effectId, Vector3 position, long nowMs)
    {
        PruneExpired(nowMs);
        RecentMapEffects.Add(new MapEffectEvent(effectId, position, nowMs));
    }
}
