# SafePointCalculator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现低延迟战场安全点位计算工具。输入场地+AOE列表+约束，一次计算同时返回近远两组安全坐标（每组0~4个点）。XZ平面计算，忽略Y轴。

**Architecture:** 接口驱动（IField/IAoeZone）→ 网格采样 → 硬过滤 → 贪心选点。场地和AOE各有5种形状。CalculationBuilder链式构建，SafeFieldContext静态全局共享。

**Tech Stack:** .NET 10, System.Numerics.Vector3, HiAuRo existing MathHelper

**New files (all in `HiAuRo/ACR/Shapes/`):**
| File | Responsibility |
|------|---------------|
| `IField.cs` | 场地接口 |
| `RectField.cs` | 矩形场地 |
| `CircleField.cs` | 圆形场地 |
| `IAoeZone.cs` | AOE区域接口 |
| `AoeCircle.cs` | 圆形AOE |
| `AoeRect.cs` | 矩形AOE（带旋转） |
| `AoeFan.cs` | 扇形AOE |
| `AoeCross.cs` | 十字AOE（复用AoeRect） |
| `AoeRing.cs` | 环形AOE |
| `SafePointResult.cs` | 结果DTO（NearPoints + FarPoints） |
| `SafePointConfig.cs` | 约束配置fluent builder |
| `SafePointCalculator.cs` | 核心计算器（持有场地，含网格缓存） |
| `CalculationBuilder.cs` | 每次计算的链式构建器 |
| `SafeFieldContext.cs` | 静态全局上下文 |

**Verify after each task:** 在 Windows 环境执行 `cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"` 必须通过

---

### Task 1: 接口定义（IField + IAoeZone）

**Files:**
- Create: `HiAuRo/ACR/Shapes/IField.cs`
- Create: `HiAuRo/ACR/Shapes/IAoeZone.cs`

- [ ] **Step 1: 创建目录**

```bash
mkdir -p HiAuRo/ACR/Shapes
```

- [ ] **Step 2: 写入 IField.cs**

```csharp
using System.Numerics;

namespace HiAuRo.ACR.Shapes;

/// <summary>
/// 场地接口 —— 判断点是否在场内 + 网格采样生成候选点
/// </summary>
public interface IField
{
    /// <summary>点是否在场地内（XZ平面，忽略Y）</summary>
    bool Contains(Vector3 point);
    /// <summary>网格采样生成候选点（spacing=采样间距）</summary>
    List<Vector3> SampleGrid(float spacing);
    /// <summary>获取场地中心点（用于靠边/靠心排序）</summary>
    Vector3 GetCenter();
}
```

- [ ] **Step 3: 写入 IAoeZone.cs**

```csharp
using System.Numerics;

namespace HiAuRo.ACR.Shapes;

/// <summary>
/// AOE 区域接口 —— 判断点是否在区域内
/// </summary>
public interface IAoeZone
{
    /// <summary>点是否在AOE区域内（XZ平面，忽略Y）</summary>
    bool Contains(Vector3 point);
}
```

- [ ] **Step 4: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```
Expected: Build succeeded.

- [ ] **Step 5: 提交**

```bash
git add HiAuRo/ACR/Shapes/IField.cs HiAuRo/ACR/Shapes/IAoeZone.cs
git commit -m "feat: add IField and IAoeZone interfaces for SafePointCalculator"
```

---

### Task 2: 场地实现（RectField + CircleField）

**Files:**
- Create: `HiAuRo/ACR/Shapes/RectField.cs`
- Create: `HiAuRo/ACR/Shapes/CircleField.cs`

- [ ] **Step 1: 写入 RectField.cs**

```csharp
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
```

- [ ] **Step 3: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```
Expected: Build succeeded.

- [ ] **Step 4: 提交**

```bash
git add HiAuRo/ACR/Shapes/RectField.cs HiAuRo/ACR/Shapes/CircleField.cs
git commit -m "feat: add RectField and CircleField with grid sampling cache"
```

---

### Task 3: AOE 实现（AoeCircle + AoeRect）

**Files:**
- Create: `HiAuRo/ACR/Shapes/AoeCircle.cs`
- Create: `HiAuRo/ACR/Shapes/AoeRect.cs`

- [ ] **Step 1: 写入 AoeCircle.cs**

```csharp
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
```

- [ ] **Step 2: 写入 AoeRect.cs**

```csharp
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
```

- [ ] **Step 3: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```
Expected: Build succeeded.

- [ ] **Step 4: 提交**

```bash
git add HiAuRo/ACR/Shapes/AoeCircle.cs HiAuRo/ACR/Shapes/AoeRect.cs
git commit -m "feat: add AoeCircle and AoeRect (with rotation) implementations"
```

---

### Task 4: AOE 实现（AoeFan + AoeCross + AoeRing）

**Files:**
- Create: `HiAuRo/ACR/Shapes/AoeFan.cs`
- Create: `HiAuRo/ACR/Shapes/AoeCross.cs`
- Create: `HiAuRo/ACR/Shapes/AoeRing.cs`

- [ ] **Step 1: 写入 AoeFan.cs**

```csharp
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
```

- [ ] **Step 2: 写入 AoeCross.cs**

```csharp
using System.Numerics;

namespace HiAuRo.ACR.Shapes;

/// <summary>
/// 十字 AOE —— 两条垂直矩形臂，绕中心旋转
/// </summary>
public sealed class AoeCross : IAoeZone
{
    readonly AoeRect _armX;
    readonly AoeRect _armZ;

    public AoeCross(Vector3 center, float totalLenX, float totalLenZ, float armWidth, float rotationDeg)
    {
        // 水平臂：宽=totalLenX，高=armWidth
        _armX = new AoeRect(center, totalLenX, armWidth, rotationDeg);
        // 垂直臂：宽=armWidth，高=totalLenZ
        _armZ = new AoeRect(center, armWidth, totalLenZ, rotationDeg);
    }

    public bool Contains(Vector3 point)
        => _armX.Contains(point) || _armZ.Contains(point);
}
```

- [ ] **Step 3: 写入 AoeRing.cs**

```csharp
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
```

- [ ] **Step 4: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```
Expected: Build succeeded.

- [ ] **Step 5: 提交**

```bash
git add HiAuRo/ACR/Shapes/AoeFan.cs HiAuRo/ACR/Shapes/AoeCross.cs HiAuRo/ACR/Shapes/AoeRing.cs
git commit -m "feat: add AoeFan, AoeCross, AoeRing implementations"
```

---

### Task 5: 结果类型（SafePointResult）

**Files:**
- Create: `HiAuRo/ACR/Shapes/SafePointResult.cs`

- [ ] **Step 1: 写入 SafePointResult.cs**

```csharp
using System.Numerics;

namespace HiAuRo.ACR.Shapes;

/// <summary>
/// 安全点位计算结果 —— 近远两组坐标
/// </summary>
public sealed class SafePointResult
{
    public List<Vector3> NearPoints { get; } = new();
    public List<Vector3> FarPoints { get; } = new();
}
```

- [ ] **Step 2: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```
Expected: Build succeeded.

- [ ] **Step 3: 提交**

```bash
git add HiAuRo/ACR/Shapes/SafePointResult.cs
git commit -m "feat: add SafePointResult DTO"
```

---

### Task 6: 约束配置（SafePointConfig fluent builder）

**Files:**
- Create: `HiAuRo/ACR/Shapes/SafePointConfig.cs`

- [ ] **Step 1: 写入 SafePointConfig.cs**

```csharp
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
    public float? MinMutualDistance { get; private set; }
    public Vector3? Origin { get; private set; }
    public float? FacingDeg { get; private set; }
    public float? HalfArcDeg { get; private set; }
    public Vector3? RangeCenter { get; private set; }
    public float? RangeRadius { get; private set; }
    public bool PreferEdge { get; private set; }
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
    public SafePointConfig MinMutualDistance(float dist) { MinMutualDistance = dist; return this; }

    /// <summary>点位必须在此方向扇形内</summary>
    public SafePointConfig InDirection(Vector3 origin, float facingDeg, float halfArcDeg) { Origin = origin; FacingDeg = facingDeg; HalfArcDeg = halfArcDeg; return this; }

    /// <summary>点位必须在圆形范围内</summary>
    public SafePointConfig WithinCircle(Vector3 center, float radius) { RangeCenter = center; RangeRadius = radius; return this; }

    /// <summary>优先场地边缘</summary>
    public SafePointConfig PreferEdge() { PreferEdge = true; return this; }

    /// <summary>优先场地中心</summary>
    public SafePointConfig PreferCenter() { PreferEdge = false; return this; }

    /// <summary>设置采样网格间距</summary>
    public SafePointConfig SetGridSpacing(float spacing) { GridSpacing = spacing; return this; }
}
```

- [ ] **Step 2: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```
Expected: Build succeeded.

- [ ] **Step 3: 提交**

```bash
git add HiAuRo/ACR/Shapes/SafePointConfig.cs
git commit -m "feat: add SafePointConfig fluent builder with dual-mode constraints"
```

---

### Task 7: 核心计算器（SafePointCalculator + CalculationBuilder）

**Files:**
- Create: `HiAuRo/ACR/Shapes/SafePointCalculator.cs`
- Create: `HiAuRo/ACR/Shapes/CalculationBuilder.cs`

- [ ] **Step 1: 写入 SafePointCalculator.cs**

```csharp
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
    public CalculationBuilder Begin() => new(this, _field);

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
        candidates = ApplyHardFilters(candidates, config);
        if (candidates.Count == 0) return result;

        // 3. 排序：按到场中心距离（PreferEdge 影响排序方向）
        var fieldCenter = GetFieldCenter();
        if (config.PreferEdge)
            candidates.Sort((a, b) => DistTo2D(b, fieldCenter).CompareTo(DistTo2D(a, fieldCenter)));
        else
            candidates.Sort((a, b) => DistTo2D(a, fieldCenter).CompareTo(DistTo2D(b, fieldCenter)));

        // 4. 近点选取
        if (config.NearCount > 0 && config.ReferencePoint != null)
        {
            var nearResult = GreedySelect(candidates, config.ReferencePoint.Value, config.NearCount, config.MinMutualDistance ?? 0, nearest: true);
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
            var farResult = GreedySelect(remaining, config.ReferencePoint.Value, config.FarCount, config.MinMutualDistance ?? 0, nearest: false);
            result.FarPoints = farResult;
        }

        return result;
    }

    /// <summary>贪心选点：每次选最近/最远的点，排除互斥距离内的其他候选</summary>
    static List<Vector3> GreedySelect(List<Vector3> candidates, Vector3 refPoint, int count, float minMutualDist, bool nearest)
    {
        var result = new List<Vector3>();
        var pool = new List<Vector3>(candidates); // 工作副本
        var minDistSq = minMutualDist * minMutualDist;

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            // 找到最佳候选
            Vector3? best = null;
            float bestDist = nearest ? float.MaxValue : float.MinValue;
            int bestIndex = -1;

            for (int j = 0; j < pool.Count; j++)
            {
                var d = DistSqTo2D(pool[j], refPoint);
                if (nearest ? d < bestDist : d > bestDist)
                {
                    bestDist = d;
                    best = pool[j];
                    bestIndex = j;
                }
            }

            if (best == null) break;

            result.Add(best.Value);

            // 排除互斥距离内的所有点（包括已选中的）
            pool.RemoveAll(p => DistSqTo2D(p, best.Value) < minDistSq);
        }

        return result;
    }

    /// <summary>硬过滤：WithinCircle、MaxDistance、InDirection</summary>
    static List<Vector3> ApplyHardFilters(List<Vector3> points, SafePointConfig config)
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

        // InDirection：点位必须在方向扇形内（原点 → 朝向 → 半张角）
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

        return points;
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

    Vector3 GetFieldCenter()
    {
        if (_field is RectField rf)
            return new Vector3(rf.GetType().GetField("_center", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(rf) is Vector3 c ? c.X : 0, 0, 0);
        // 简化：用采样格点中心估算
        var grid = _field.SampleGrid(0.5f);
        if (grid.Count == 0) return Vector3.Zero;
        return new Vector3(grid.Average(p => (double)p.X), 0, grid.Average(p => (double)p.Z));
    }
}
```

- [ ] **Step 2: 写入 CalculationBuilder.cs**

```csharp
namespace HiAuRo.ACR.Shapes;

/// <summary>
/// 每次计算的链式构建器 —— 持有 AOE 列表，终端方法 Calculate 触发实际计算
/// </summary>
public sealed class CalculationBuilder
{
    readonly SafePointCalculator _calculator;
    readonly IField _field;
    readonly List<IAoeZone> _aoes = new();

    internal CalculationBuilder(SafePointCalculator calculator, IField field)
    {
        _calculator = calculator;
        _field = field;
    }

    /// <summary>添加一个 AOE 区域</summary>
    public CalculationBuilder WithAoe(IAoeZone aoe) { _aoes.Add(aoe); return this; }

    /// <summary>执行计算并返回结果</summary>
    public SafePointResult Calculate(SafePointConfig config) => _calculator.Calculate(_aoes, config);
}
```

- [ ] **Step 3: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```
Expected: Build succeeded.

- [ ] **Step 4: 提交**

```bash
git add HiAuRo/ACR/Shapes/SafePointCalculator.cs HiAuRo/ACR/Shapes/CalculationBuilder.cs
git commit -m "feat: add SafePointCalculator core with greedy selection algorithm"
```

---

### Task 8: 静态全局上下文（SafeFieldContext）

**Files:**
- Create: `HiAuRo/ACR/Shapes/SafeFieldContext.cs`

- [ ] **Step 1: 写入 SafeFieldContext.cs**

```csharp
namespace HiAuRo.ACR.Shapes;

/// <summary>
/// 全局副本级共享入口 —— 跨命名空间访问同一个场地计算器
/// </summary>
public static class SafeFieldContext
{
    /// <summary>当前副本的计算器实例（轴脚本初始化时设置一次）</summary>
    public static SafePointCalculator? Current { get; set; }
}
```

- [ ] **Step 2: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```
Expected: Build succeeded.

- [ ] **Step 3: 提交**

```bash
git add HiAuRo/ACR/Shapes/SafeFieldContext.cs
git commit -m "feat: add SafeFieldContext static global access point"
```

---

### Task 9: 修复 GetFieldCenter（移除反射，改用接口方法）

**Files:**
- Modify: `HiAuRo/ACR/Shapes/SafePointCalculator.cs` — 替换反射为 `_field.GetCenter()`

- [ ] **Step 1: 替换 SafePointCalculator.GetFieldCenter()**

将 `SafePointCalculator.cs` 中的 `GetFieldCenter()` 方法替换为：

```csharp
    Vector3 GetFieldCenter() => _field.GetCenter();
```

- [ ] **Step 2: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```
Expected: Build succeeded.

- [ ] **Step 3: 提交**

```bash
git add HiAuRo/ACR/Shapes/SafePointCalculator.cs
git commit -m "refactor: replace reflection with _field.GetCenter() in SafePointCalculator"
```

---

### Task 10: 最终验证与检查

- [ ] **Step 1: 全量构建**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```
Expected: Build succeeded with 0 errors, 0 warnings.

- [ ] **Step 2: 检查文件清单**

```bash
ls HiAuRo/ACR/Shapes/
```
Expected: 13 files (IField, RectField, CircleField, IAoeZone, AoeCircle, AoeRect, AoeFan, AoeCross, AoeRing, SafePointResult, SafePointConfig, SafePointCalculator, CalculationBuilder, SafeFieldContext)

14 files total.

- [ ] **Step 3: 手动验证路径**

在 `Plugin.cs` 或任意测试位置添加临时验证代码（不提交），确认以下流程无异常：

```csharp
// 初始化
SafeFieldContext.Current = new SafePointCalculator(new RectField(Vector3.Zero, 40, 30));

// 计算
var result = SafeFieldContext.Current.Begin()
    .WithAoe(new AoeCircle(new Vector3(5, 0, 0), 5))
    .WithAoe(new AoeFan(new Vector3(-5, 0, 0), 10, 180, 60))
    .Calculate(new SafePointConfig()
        .RefPoint(new Vector3(0, 0, 3))
        .Nearest(2)
        .Farthest(2, minDist: 3)
        .MaxDistance(10)
        .MinMutualDistance(3));

// 预期：result.NearPoints 和 FarPoints 均非空，点数 ≤ 2
// 所有返回点均在场地内且不在 AOE 内
```
Expected: 无异常，结果符合预期。

- [ ] **Step 4: 最终提交（如有修正）**

```bash
git add -A
git commit -m "chore: final verification and cleanup for SafePointCalculator"
```

