using System.Numerics;
using HiAuRo.ACR;

namespace HiAuRo.ACR.Shapes;

/// <summary>
/// 环形扇形 AOE —— 扇形减去内圈，尖端被切掉
/// </summary>
public sealed class AoeRingFan : IAoeZone
{
    readonly Vector3 _center;
    readonly float _innerSq;
    readonly float _outerSq;
    readonly float _facingRad;
    readonly float _halfArcRad;

    public AoeRingFan(Vector3 center, float innerRadius, float outerRadius, float facingDeg, float arcDeg)
    {
        _center = center;
        _innerSq = innerRadius * innerRadius;
        _outerSq = outerRadius * outerRadius;
        _facingRad = facingDeg * MathF.PI / 180f;
        _halfArcRad = arcDeg / 2 * MathF.PI / 180f;
    }

    public bool Contains(Vector3 point)
    {
        var dx = point.X - _center.X;
        var dz = point.Z - _center.Z;
        var distSq = dx * dx + dz * dz;
        if (distSq < _innerSq || distSq > _outerSq)
            return false;

        var angle = MathF.Atan2(dx, -dz);
        var diff = MathHelper.NormalizeAngle(angle - _facingRad);
        return MathF.Abs(diff) <= _halfArcRad;
    }
}
