using HiAuRo.ACR;
using HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 触发条件参数 —— Actor 死亡
/// </summary>
public sealed class TriggerCondParams_Actor死亡 : ITriggerCondParams
{
    /// <summary>死亡的 Actor DataId</summary>
    public uint DataId;
}

[TriggerDisplay("Actor死亡", "检测指定DataId的Actor死亡")]
[TriggerTypeName("TriggerCondActorDeath")]

/// <summary>
/// 检测指定 DataId 的敌人是否已死亡（从对象表中消失或标记为死）
/// </summary>
public sealed class TriggerCond_Actor死亡 : ITriggerCond
{
    private readonly uint _dataId;

    public TriggerCond_Actor死亡(uint dataId)
    {
        _dataId = dataId;
    }

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        // 检查所有对象中是否存在此 DataId 且存活
        foreach (var obj in Objects.All)
        {
            if (obj is IBattleNPC npc && npc.DataID == _dataId)
            {
                // 存在且未死亡 → 不满足
                if (npc.IsDead != true)
                    return false;
            }
        }
        // 不存在或已死亡 → 满足
        return true;
    }
}
