using System.Numerics;

namespace HiAuRo.ACR.Shapes;

/// <summary>
/// 矩形场地 —— 轴对齐，中心点定位
/// </summary>
public sealed class RectField : IField
{
    readonly Vector3 _center;
    readonly float _halfX;
    readonly float _halfZ;
    readonly Dictionary<float, List<Vector3>> _gridCache = new();

    public RectField(Vector3 center, float widthX, float depthZ)
    {
        _center = center;
        _halfX = widthX / 2;
        _halfZ = depthZ / 2;
    }

    public bool Contains(Vector3 point)
    {
        var dx = point.X - _center.X;
        var dz = point.Z - _center.Z;
        return MathF.Abs(dx) <= _halfX && MathF.Abs(dz) <= _halfZ;
    }

    public List<Vector3> SampleGrid(float spacing)
    {
        if (_gridCache.TryGetValue(spacing, out var cached))
            return cached;

        var points = new List<Vector3>();
        var y = _center.Y;
        for (var x = _center.X - _halfX; x <= _center.X + _halfX + spacing * 0.001f; x += spacing)
            for (var z = _center.Z - _halfZ; z <= _center.Z + _halfZ + spacing * 0.001f; z += spacing)
                points.Add(new Vector3(x, y, z));

        _gridCache[spacing] = points;
        return points;
    }

    public Vector3 GetCenter() => _center;
}
