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
