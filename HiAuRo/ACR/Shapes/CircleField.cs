using System.Numerics;

namespace HiAuRo.ACR.Shapes;

/// <summary>
/// 圆形场地 —— 中心点+半径定位
/// </summary>
public sealed class CircleField : IField
{
    readonly Vector3 _center;
    readonly float _radius;
    readonly float _radiusSq;
    readonly Dictionary<float, List<Vector3>> _gridCache = new();

    public CircleField(Vector3 center, float radius)
    {
        _center = center;
        _radius = radius;
        _radiusSq = radius * radius;
    }

    public bool Contains(Vector3 point)
    {
        var dx = point.X - _center.X;
        var dz = point.Z - _center.Z;
        return dx * dx + dz * dz <= _radiusSq;
    }

    public List<Vector3> SampleGrid(float spacing)
    {
        if (_gridCache.TryGetValue(spacing, out var cached))
            return cached;

        var points = new List<Vector3>();
        var y = _center.Y;
        for (var x = _center.X - _radius; x <= _center.X + _radius + spacing * 0.001f; x += spacing)
            for (var z = _center.Z - _radius; z <= _center.Z + _radius + spacing * 0.001f; z += spacing)
            {
                var dx = x - _center.X;
                var dz = z - _center.Z;
                if (dx * dx + dz * dz <= _radiusSq)
                    points.Add(new Vector3(x, y, z));
            }

        _gridCache[spacing] = points;
        return points;
    }

    public Vector3 GetCenter() => _center;
}
