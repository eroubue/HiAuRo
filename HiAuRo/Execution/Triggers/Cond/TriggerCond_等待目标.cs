using HiAuRo.ACR;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 等待指定 DataId 的单位在对象表中出现（可选中 + 未死亡）
/// </summary>
[TriggerDisplay("等待目标", "等待指定DataId的目标出现")]
[TriggerTypeName("TriggerCondWaitTarget")]
public sealed class TriggerCond_等待目标 : ITriggerCond
{
    private readonly uint _dataId;

    /// <param name="dataId">目标 DataId</param>
    public TriggerCond_等待目标(uint dataId)
    {
        _dataId = dataId;
    }

    /// <summary>检测指定 DataId 的单位是否已出现</summary>
    public bool Handle(ITriggerCondParams? condParams = null)
    {
        foreach (var obj in Objects.Enemies)
        {
            if (obj is IBattleNPC npc && npc.DataID == _dataId && npc.IsTargetable && npc.IsDead != true)
                return true;
        }
        return false;
    }
}
