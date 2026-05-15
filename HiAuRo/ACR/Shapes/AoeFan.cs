using System.Numerics;
using HiAuRo.ACR;

namespace HiAuRo.ACR.Shapes;

/// <summary>
/// 扇形 AOE —— 从中心朝向某方向，张开指定角度
/// </summary>
public sealed class AoeFan : IAoeZone
{
    readonly Vector3 _center;
    readonly float _radiusSq;
    readonly float _facingRad;
    readonly float _halfArcRad;

    public AoeFan(Vector3 center, float radius, float facingDeg, float arcDeg)
    {
        _center = center;
        _radiusSq = radius * radius;
        _facingRad = facingDeg * MathF.PI / 180f;
        _halfArcRad = arcDeg / 2 * MathF.PI / 180f;
    }

    public bool Contains(Vector3 point)
    {
        var dx = point.X - _center.X;
        var dz = point.Z - _center.Z;
        if (dx * dx + dz * dz > _radiusSq)
            return false;

        var angle = MathF.Atan2(dz, dx);
        var diff = MathHelper.NormalizeAngle(angle - _facingRad);
        return MathF.Abs(diff) <= _halfArcRad;
    }
}
