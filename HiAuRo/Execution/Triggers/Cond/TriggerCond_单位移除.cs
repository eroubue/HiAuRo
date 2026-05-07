using HiAuRo.ACR;
using HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 触发条件参数 —— 单位移除
/// </summary>
public sealed class TriggerCondParams_单位移除 : ITriggerCondParams
{
    /// <summary>被移除的单位 DataId</summary>
    public uint DataId;
}

/// <summary>
/// 检测指定 DataId 的敌人是否已从对象表中移除（消失或死亡）
/// </summary>
[TriggerDisplay("单位移除", "检测指定DataId的单位是否移除")]
[TriggerTypeName("HiAuRo.Execution.Triggers.Cond.TriggerCond_单位移除, HiAuRo")]

public sealed class TriggerCond_单位移除 : ITriggerCond
{
    private readonly uint _dataId;

    public TriggerCond_单位移除(uint dataId)
    {
        _dataId = dataId;
    }

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        foreach (var obj in Objects.All)
        {
            if (obj is IBattleNPC npc && npc.DataID == _dataId)
                return false; // 还在
        }
        return true; // 已移除
    }
}
