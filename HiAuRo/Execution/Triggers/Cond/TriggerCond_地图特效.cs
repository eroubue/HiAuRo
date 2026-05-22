using HiAuRo.ACR;
using HiAuRo.Execution.Events;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 触发条件参数 —— 地图特效
/// </summary>
public sealed class TriggerCondParams_地图特效 : ITriggerCondParams
{
    /// <summary>地图特效 ID</summary>
    public uint EffectId;
}

/// <summary>
/// 检测是否出现指定的地图特效
/// 事件驱动：匹配 MapEffectParams.PositionIndex；轮询：查询 BattleData 近期地图特效历史
/// </summary>
[TriggerDisplay("地图特效", "检测地图特效触发")]
[TriggerTypeName("TriggerCondMapEffect")]
public sealed class TriggerCond_地图特效 : ITriggerCond
{
    private readonly uint _effectId;

    /// <param name="effectId">地图特效 ID</param>
    public TriggerCond_地图特效(uint effectId)
    {
        _effectId = effectId;
    }

    /// <summary>检测是否出现指定的地图特效</summary>
    public bool Handle(ITriggerCondParams? condParams = null)
    {
        if (condParams is MapEffectParams me)
            return me.PositionIndex == _effectId;

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var cutoffMs = nowMs - 3000;
        return BattleData.GetRecentMapEffects().Any(e =>
            e.TimestampMs >= cutoffMs && e.EffectId == _effectId);
    }
}
