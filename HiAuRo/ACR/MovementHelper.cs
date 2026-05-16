using System.Numerics;
using HiAuRo.Runtime.Intelligence;

namespace HiAuRo.ACR;

/// <summary>
/// 移动/TP 快捷操作 —— ACR 在事件回调中直接调用
/// </summary>
public static class MovementHelper
{
    /// <summary>寻路移动到目标位置（依赖 VNavmesh）</summary>
    public static void MoveTo(Vector3 target, string demandId = "manual")
    {
        DemandBuffer.Add(new MovementDemand
        {
            Id = demandId,
            Type = DemandType.MoveTo,
            TargetPos = target,
        });
    }

    /// <summary>瞬移到目标位置（依赖外部 TP 插件）</summary>
    public static void TeleportTo(Vector3 target, string demandId = "manual")
    {
        DemandBuffer.Add(new MovementDemand
        {
            Id = demandId,
            Type = DemandType.TP,
            TargetPos = target,
        });
    }

    /// <summary>停住不动</summary>
    public static void Hold(string demandId = "manual")
    {
        DemandBuffer.Add(new MovementDemand
        {
            Id = demandId,
            Type = DemandType.Hold,
        });
    }
}
