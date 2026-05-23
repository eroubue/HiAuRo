using HiAuRo.ACR;
using HiAuRo.Execution.Events;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 检测是否有指定连线生效
/// 事件驱动：匹配 TetherCreateParams；轮询：查询 BattleData 活跃连线列表
/// </summary>
[TriggerDisplay("连线", "检测是否存在指定连线")]
[TriggerTypeName("HiAuRo.Execution.Triggers.Cond.TriggerCond_连线, HiAuRo")]

public sealed class TriggerCond_连线 : ITriggerCond
{
    private readonly uint _tetherId;

    /// <param name="tetherId">连线 ID</param>
    public TriggerCond_连线(uint tetherId)
    {
        _tetherId = tetherId;
    }

    /// <summary>检测是否有指定连线生效</summary>
    public bool Handle(ITriggerCondParams? condParams = null)
    {
        if (condParams is TetherCreateParams create)
            return create.TetherID == _tetherId;

        if (condParams is TetherRemoveParams)
            return false;

        return BattleData.GetRecentTethers().Any(e => e.TetherId == _tetherId);
    }
}
