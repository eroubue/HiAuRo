using HiAuRo.ACR;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 检测指定 DataId 的敌人在当前对象表中是否可选中
/// </summary>
[TriggerDisplay("单位可选中", "检测指定DataId的单位是否可选中")]
[TriggerTypeName("HiAuRo.Execution.Triggers.Cond.TriggerCond_单位可选中, HiAuRo")]

public sealed class TriggerCond_单位可选中 : ITriggerCond
{
    private readonly uint _dataId;

    /// <param name="dataId">单位 DataId</param>
    public TriggerCond_单位可选中(uint dataId)
    {
        _dataId = dataId;
    }

    /// <summary>检测指定 DataId 的单位是否可选中</summary>
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
