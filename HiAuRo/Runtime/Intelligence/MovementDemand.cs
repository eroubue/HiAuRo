using System.Numerics;

namespace HiAuRo.Runtime.Intelligence;

/// <summary>
/// 移动/TP 需求，由脚本或 IPC 产生
/// </summary>
public sealed class MovementDemand
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public string FactNodeId { get; init; } = "";
    public DemandType Type { get; init; }
    public Vector3? TargetPos { get; init; }
    public float? TargetHeading { get; init; }
    public string TargetRole { get; init; } = "All";
    public int AddedOrder { get; set; }
    public string Source { get; init; } = "";
}
