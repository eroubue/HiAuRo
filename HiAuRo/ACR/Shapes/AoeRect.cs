using System.Numerics;

namespace HiAuRo.ACR.Shapes;

/// <summary>
/// 矩形 AOE —— 长沿 X 轴，绕中心旋转角度
/// </summary>
public sealed class AoeRect : IAoeZone
{
    readonly Vector3 _center;
    readonly float _halfW;
    readonly float _halfD;
    readonly float _cosR;
    readonly float _sinR;

    public AoeRect(Vector3 center, float widthX, float depthZ, float rotationDeg)
    {
        _center = center;
        _halfW = widthX / 2;
        _halfD = depthZ / 2;
        var rad = rotationDeg * MathF.PI / 180f;
        _cosR = MathF.Cos(rad);
        _sinR = MathF.Sin(rad);
    }

    public bool Contains(Vector3 point)
    {
        var dx = point.X - _center.X;
        var dz = point.Z - _center.Z;
        // 反向旋转到本地坐标系
        var localX = dx * _cosR + dz * _sinR;
        var localZ = -dx * _sinR + dz * _cosR;
        return MathF.Abs(localX) <= _halfW && MathF.Abs(localZ) <= _halfD;
    }
}
