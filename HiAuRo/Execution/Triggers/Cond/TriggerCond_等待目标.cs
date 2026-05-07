using HiAuRo.ACR;
using HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 触发条件参数 —— 等待目标
/// </summary>
public sealed class TriggerCondParams_等待目标 : ITriggerCondParams
{
    /// <summary>等待出现的单位 DataId</summary>
    public uint DataId;
}

[TriggerDisplay("等待目标", "等待指定DataId的目标出现")]
[TriggerTypeName("TriggerCondWaitTarget")]

/// <summary>
/// 等待指定 DataId 的单位在对象表中出现（可选中 + 未死亡）
/// </summary>
public sealed class TriggerCond_等待目标 : ITriggerCond
{
    private readonly uint _dataId;

    public TriggerCond_等待目标(uint dataId)
    {
        _dataId = dataId;
    }

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
