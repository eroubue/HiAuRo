using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 触发条件参数 —— 地图特效（暂存，等待 MapEffect 读取基础设施）
/// </summary>
public sealed class TriggerCondParams_地图特效 : ITriggerCondParams
{
    /// <summary>地图特效 ID</summary>
    public uint EffectId;
}

/// <summary>
/// 检测是否出现指定的地图特效（暂存，等待 MapEffect Hook 基础设施）
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
        // 需要 MapEffect Hook，暂未实现
        return false;
    }
}
