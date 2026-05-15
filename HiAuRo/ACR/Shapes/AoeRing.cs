using System.Numerics;

namespace HiAuRo.ACR.Shapes;

/// <summary>
/// 环形 AOE —— 内圈到外圈之间的环形区域
/// </summary>
public sealed class AoeRing : IAoeZone
{
    readonly Vector3 _center;
    readonly float _innerSq;
    readonly float _outerSq;

    public AoeRing(Vector3 center, float innerRadius, float outerRadius)
    {
        _center = center;
        _innerSq = innerRadius * innerRadius;
        _outerSq = outerRadius * outerRadius;
    }

    public bool Contains(Vector3 point)
    {
        var dx = point.X - _center.X;
        var dz = point.Z - _center.Z;
        var distSq = dx * dx + dz * dz;
        return distSq >= _innerSq && distSq <= _outerSq;
    }
}
