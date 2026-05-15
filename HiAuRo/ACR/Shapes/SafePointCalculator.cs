using System.Numerics;
using HiAuRo.ACR;

namespace HiAuRo.ACR.Shapes;

/// <summary>
/// 安全点位计算器 —— 持有场地引用，每次 Begin() 开启新计算
/// </summary>
public sealed class SafePointCalculator
{
    readonly IField _field;

    public SafePointCalculator(IField field) { _field = field; }

    /// <summary>开启一次新的计算，返回链式构建器</summary>
    public CalculationBuilder Begin() => new(this);

    /// <summary>核心计算逻辑</summary>
    internal SafePointResult Calculate(List<IAoeZone> aoes, SafePointConfig config)
    {
        var result = new SafePointResult();
        if (config.NearCount == 0 && config.FarCount == 0)
            return result;

        // 1. 采样 + 安全过滤
        var candidates = new List<Vector3>();
        foreach (var p in _field.SampleGrid(config.GridSpacing))
        {
            if (!_field.Contains(p)) continue;
            if (aoes.Any(a => a.Contains(p))) continue;
            candidates.Add(p);
        }

        // 2. 硬过滤
        ApplyHardFilters(candidates, config);
        if (candidates.Count == 0) return result;

        // 3. 排序：按到场中心距离（EdgePreferred 影响排序方向）
        var fieldCenter = _field.GetCenter();
        if (config.EdgePreferred)
            candidates.Sort((a, b) => DistTo2D(b, fieldCenter).CompareTo(DistTo2D(a, fieldCenter)));
        else
            candidates.Sort((a, b) => DistTo2D(a, fieldCenter).CompareTo(DistTo2D(b, fieldCenter)));

        // 4. 近点选取
        if (config.NearCount > 0 && config.ReferencePoint != null)
        {
            var nearResult = GreedySelect(candidates, config.ReferencePoint.Value, config.NearCount, config.MutualMinDistance ?? 0, nearest: true);
            foreach (var p in nearResult)
            {
                result.NearPoints.Add(p);
                candidates.Remove(p);
            }
        }

        // 5. 远点选取（从剩余安全点中）
        if (config.FarCount > 0 && config.ReferencePoint != null)
        {
            var remaining = new List<Vector3>(candidates);
            // 过滤 MinDistanceFromRef
            if (config.MinDistanceFromRef > 0)
            {
                var minDistSq = config.MinDistanceFromRef.Value * config.MinDistanceFromRef.Value;
                remaining.RemoveAll(p => DistSqTo2D(p, config.ReferencePoint.Value) < minDistSq);
            }
            var farResult = GreedySelect(remaining, config.ReferencePoint.Value, config.FarCount, config.MutualMinDistance ?? 0, nearest: false);
            result.FarPoints.AddRange(farResult);
        }

        return result;
    }

    /// <summary>贪心选点：每次选最近/最远的点，排除互斥距离内的其他候选</summary>
    static List<Vector3> GreedySelect(List<Vector3> candidates, Vector3 refPoint, int count, float minMutualDist, bool nearest)
    {
        var result = new List<Vector3>();
        var pool = new List<Vector3>(candidates);
        var minDistSq = minMutualDist * minMutualDist;

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            Vector3? best = null;
            float bestDist = nearest ? float.MaxValue : float.MinValue;

            foreach (var p in pool)
            {
                var d = DistSqTo2D(p, refPoint);
                if (nearest ? d < bestDist : d > bestDist)
                {
                    bestDist = d;
                    best = p;
                }
            }

            if (best == null) break;
            result.Add(best.Value);
            pool.RemoveAll(p => DistSqTo2D(p, best.Value) <= minDistSq);
        }

        return result;
    }

    /// <summary>硬过滤：WithinCircle、MaxDistance、InDirection</summary>
    static void ApplyHardFilters(List<Vector3> points, SafePointConfig config)
    {
        // WithinCircle
        if (config.RangeCenter != null && config.RangeRadius != null)
        {
            var rangeC = config.RangeCenter.Value;
            var rangeRSq = config.RangeRadius.Value * config.RangeRadius.Value;
            points.RemoveAll(p => DistSqTo2D(p, rangeC) > rangeRSq);
        }

        // MaxDistanceFromRef
        if (config.MaxDistanceFromRef != null && config.ReferencePoint != null)
        {
            var maxDistSq = config.MaxDistanceFromRef.Value * config.MaxDistanceFromRef.Value;
            points.RemoveAll(p => DistSqTo2D(p, config.ReferencePoint.Value) > maxDistSq);
        }

        // InDirection
        if (config.Origin != null && config.FacingDeg != null && config.HalfArcDeg != null)
        {
            var origin = config.Origin.Value;
            var facingRad = config.FacingDeg.Value * MathF.PI / 180f;
            var halfArcRad = config.HalfArcDeg.Value * MathF.PI / 180f;
            points.RemoveAll(p =>
            {
                var dx = p.X - origin.X;
                var dz = p.Z - origin.Z;
                var angle = MathF.Atan2(dz, dx);
                var diff = MathHelper.NormalizeAngle(angle - facingRad);
                return MathF.Abs(diff) > halfArcRad;
            });
        }

    }

    static float DistSqTo2D(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return dx * dx + dz * dz;
    }

    static float DistTo2D(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
