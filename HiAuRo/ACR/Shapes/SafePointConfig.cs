using System.Numerics;

namespace HiAuRo.ACR.Shapes;

/// <summary>
/// 安全点约束配置 —— fluent builder，一次计算同时取近远两组
/// </summary>
public sealed class SafePointConfig
{
    public int NearCount { get; private set; }
    public int FarCount { get; private set; }
    public Vector3? ReferencePoint { get; private set; }
    public float? MinDistanceFromRef { get; private set; }
    public float? MaxDistanceFromRef { get; private set; }
    public float? MutualMinDistance { get; private set; }
    public Vector3? Origin { get; private set; }
    public float? FacingDeg { get; private set; }
    public float? HalfArcDeg { get; private set; }
    public Vector3? RangeCenter { get; private set; }
    public float? RangeRadius { get; private set; }
    public bool EdgePreferred { get; private set; }
    public float GridSpacing { get; private set; } = 0.5f;

    /// <summary>设置参考点</summary>
    public SafePointConfig RefPoint(Vector3 refPoint) { ReferencePoint = refPoint; return this; }

    /// <summary>靠近参考点的点位数量 (0~4)</summary>
    public SafePointConfig Nearest(int count) { NearCount = Math.Clamp(count, 0, 4); return this; }

    /// <summary>远离参考点的点位数量 (0~4)，minDist=距参考点最小距离</summary>
    public SafePointConfig Farthest(int count, float minDist = 0) { FarCount = Math.Clamp(count, 0, 4); MinDistanceFromRef = minDist; return this; }

    /// <summary>所有点位不超过参考点的此距离（可选）</summary>
    public SafePointConfig MaxDistance(float dist) { MaxDistanceFromRef = dist; return this; }

    /// <summary>所有点位间最小间距（全局互斥）</summary>
    public SafePointConfig MinMutualDistance(float dist) { MutualMinDistance = dist; return this; }

    /// <summary>点位必须在此方向扇形内</summary>
    public SafePointConfig InDirection(Vector3 origin, float facingDeg, float halfArcDeg) { Origin = origin; FacingDeg = facingDeg; HalfArcDeg = halfArcDeg; return this; }

    /// <summary>点位必须在圆形范围内</summary>
    public SafePointConfig WithinCircle(Vector3 center, float radius) { RangeCenter = center; RangeRadius = radius; return this; }

    /// <summary>优先场地边缘</summary>
    public SafePointConfig PreferEdge() { EdgePreferred = true; return this; }

    /// <summary>优先场地中心</summary>
    public SafePointConfig PreferCenter() { EdgePreferred = false; return this; }

    /// <summary>设置采样网格间距</summary>
    public SafePointConfig SetGridSpacing(float spacing) { GridSpacing = spacing; return this; }
}
