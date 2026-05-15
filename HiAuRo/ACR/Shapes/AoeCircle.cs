using System.Numerics;

namespace HiAuRo.ACR.Shapes;

/// <summary>
/// 圆形 AOE —— 中心点+半径
/// </summary>
public sealed class AoeCircle : IAoeZone
{
    readonly Vector3 _center;
    readonly float _radiusSq;

    public AoeCircle(Vector3 center, float radius)
    {
        _center = center;
        _radiusSq = radius * radius;
    }

    public bool Contains(Vector3 point)
    {
        var dx = point.X - _center.X;
        var dz = point.Z - _center.Z;
        return dx * dx + dz * dz <= _radiusSq;
    }
}
