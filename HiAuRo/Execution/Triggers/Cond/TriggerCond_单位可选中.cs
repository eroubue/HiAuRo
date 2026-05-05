using HiAuRo.ACR;
using HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 触发条件参数 —— 单位可选中
/// </summary>
public sealed class TriggerCondParams_单位可选中 : ITriggerCondParams
{
    /// <summary>等待变为可选中的单位 DataId</summary>
    public uint DataId;
}

/// <summary>
/// 检测指定 DataId 的敌人在当前对象表中是否可选中
/// </summary>
public sealed class TriggerCond_单位可选中 : ITriggerCond
{
    private readonly uint _dataId;

    public TriggerCond_单位可选中(uint dataId)
    {
        _dataId = dataId;
    }

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        foreach (var obj in Objects.All)
        {
            if (obj is IBattleNPC npc && npc.DataID == _dataId && npc.IsTargetable)
                return true;
        }
        return false;
    }
}
