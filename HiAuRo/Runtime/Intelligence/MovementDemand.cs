using System.Numerics;

namespace HiAuRo.Runtime.Intelligence;

/// <summary>移动/传送/站定 需求（本地数据，角色路由由外部分发插件负责）</summary>
public sealed class MovementDemand
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string FactNodeId { get; set; } = "";
    public DemandType Type { get; set; } = DemandType.MoveTo;
    public Vector3? TargetPos { get; set; }
    public float? TargetHeading { get; set; }
    public float? Duration { get; set; }       // Hold 持续秒
    public int AddedOrder { get; set; }
    public string Source { get; set; } = "";
}
