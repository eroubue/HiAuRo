# SafePointCalculator 设计文档

## 概述

为 HiAuRo 轴脚本提供低延迟战场安全点位计算工具。输入场地 + AOE 列表 + 约束条件，返回非 AOE 区域内的安全坐标点（1-8 个）。所有计算在 XZ 平面进行，忽略 Y 轴。

## 命名空间与文件结构

```
HiAuRo.ACR.Shapes                          ← 命名空间
├── IField.cs                              ← 场地接口
│   └── RectField / CircleField            ← 矩形/圆形场地
├── IAoeZone.cs                            ← AOE 区域接口
│   └── AoeCircle / AoeRect / AoeFan / AoeCross / AoeRing
├── SafePointCalculator.cs                 ← 计算器（持有场地引用）
├── CalculationBuilder.cs                  ← 每次计算的链式构建器
├── SafePointConfig.cs                     ← 约束配置（fluent builder）
└── SafeFieldContext.cs                    ← 静态全局上下文
```

## 数据结构

### 场地（IField）

```csharp
/// <summary>场地接口 —— 判断点是否在场内 + 网格采样</summary>
public interface IField
{
    /// <summary>点是否在场地内</summary>
    bool Contains(Vector3 point);
    /// <summary>网格采样生成候选点</summary>
    List<Vector3> SampleGrid(float spacing);
}

/// <summary>矩形场地（轴对齐，按 Center 为矩形中心）</summary>
public class RectField(Vector3 center, float widthX, float depthZ) : IField { }

/// <summary>圆形场地</summary>
public class CircleField(Vector3 center, float radius) : IField { }
```

### AOE 区域（IAoeZone）

```csharp
/// <summary>AOE 区域接口</summary>
public interface IAoeZone
{
    bool Contains(Vector3 point);
}

/// <summary>圆形 AOE</summary>
public class AoeCircle(Vector3 center, float radius) : IAoeZone { }

/// <summary>矩形 AOE（长沿 X 轴，旋转后生效）</summary>
public class AoeRect(Vector3 center, float widthX, float depthZ, float rotationDeg) : IAoeZone { }

/// <summary>扇形 AOE（从中心朝向某个方向，张开 arcDeg 角度）</summary>
public class AoeFan(Vector3 center, float radius, float facingDeg, float arcDeg) : IAoeZone { }

/// <summary>十字 AOE</summary>
/// totalLenX/totalLenZ = 十字两臂总长，armWidth = 臂宽</summary>
public class AoeCross(Vector3 center, float totalLenX, float totalLenZ, float armWidth, float rotationDeg) : IAoeZone { }

/// <summary>环形 AOE（内圈到外圈）</summary>
public class AoeRing(Vector3 center, float innerRadius, float outerRadius) : IAoeZone { }
```

### 约束配置（SafePointConfig）

```csharp
public class SafePointConfig
{
    public int Count { get; private set; } = 1;               // 需要几个点 (1~8)
    public Vector3? ReferencePoint { get; private set; }       // 参考点
    public ReferenceMode? RefMode { get; private set; }        // Nearest / Farthest
    public float? MinDistanceFromRef { get; private set; }     // Farthest 模式的最小距离
    public float? MinMutualDistance { get; private set; }      // 点位间最小间距
    public Vector3? Origin { get; private set; }               // 方向过滤原点
    public float? FacingDeg { get; private set; }              // 方向过滤朝向
    public float? HalfArcDeg { get; private set; }             // 方向过滤半张角
    public bool PreferEdge { get; private set; }               // true=靠边, false=靠中心
    public float GridSpacing { get; private set; } = 0.5f;     // 采样网格间距

    // --- fluent builder ---
    public SafePointConfig Count(int n)                             { Count = Math.Clamp(n, 1, 8); return this; }
    public SafePointConfig NearestTo(Vector3 refPoint)              { ReferencePoint = refPoint; RefMode = ReferenceMode.Nearest; return this; }
    public SafePointConfig FarthestFrom(Vector3 refPoint, float minDistance) { ReferencePoint = refPoint; RefMode = ReferenceMode.Farthest; MinDistanceFromRef = minDistance; return this; }
    public SafePointConfig MinMutualDistance(float dist)            { MinMutualDistance = dist; return this; }
    public SafePointConfig InDirection(Vector3 origin, float facingDeg, float halfArcDeg) { Origin = origin; FacingDeg = facingDeg; HalfArcDeg = halfArcDeg; return this; }
    public SafePointConfig PreferEdge()                             { PreferEdge = true; return this; }
    public SafePointConfig PreferCenter()                           { PreferEdge = false; return this; }
    public SafePointConfig SetGridSpacing(float spacing)            { GridSpacing = spacing; return this; }
}

public enum ReferenceMode { Nearest, Farthest }
```

### 计算构建器（CalculationBuilder）

每次计算通过 `SafePointCalculator.Begin()` 获得全新实例，链式设置 AOE 和约束，互不污染。

```csharp
public class CalculationBuilder
{
    readonly SafePointCalculator _owner;
    readonly List<IAoeZone> _aoes = new();

    internal CalculationBuilder(SafePointCalculator owner) { _owner = owner; }

    public CalculationBuilder WithAoe(IAoeZone aoe) { _aoes.Add(aoe); return this; }
    public List<Vector3> Calculate(SafePointConfig config);   // 终端方法
}
```

`Calculate()` 内部流程：
1. 从 `_owner._field.SampleGrid(config.GridSpacing)` 获取候选点
2. 过滤：`!aoe.Any(a => a.Contains(p))` + `_owner._field.Contains(p)`
3. 按 `config` 约束打分排序
4. 返回前 `Count` 个

### 计算器（SafePointCalculator）

```csharp
public class SafePointCalculator
{
    readonly IField _field;

    public SafePointCalculator(IField field) { _field = field; }

    /// <summary>开启一次新的计算，返回链式构建器</summary>
    public CalculationBuilder Begin() => new(this);
}
```

### 静态上下文（SafeFieldContext）

```csharp
/// <summary>全局副本级共享入口 —— 跨命名空间访问同一个场地</summary>
public static class SafeFieldContext
{
    public static SafePointCalculator? Current { get; set; }
}
```

## 调用流程

```csharp
// ===== 副本初始化（轴脚本入口，执行一次）=====
SafeFieldContext.Current = new SafePointCalculator(new RectField(center, 40, 30));

// ===== 任意类、任意命名空间、任意时机 =====
var config = new SafePointConfig()
    .Count(3)
    .NearestTo(npcPos)
    .MinMutualDistance(5)
    .PreferEdge();

var points = SafeFieldContext.Current
    .Begin()
    .WithAoe(new AoeCircle(bossPos, 8))
    .WithAoe(new AoeFan(bossPos, 20, 90, 60))
    .Calculate(config);

// points: List<Vector3>，1~3 个安全点位
```

## 打分规则（Calculate 内部）

安全点按以下规则综合分排序，取前 N 个：

1. **Farthest 模式**：先过滤 `DistToRef >= MinDistanceFromRef`，再按距离降序打分
2. **Nearest 模式**：按距离升序直接排序
3. **方向过滤**：先过滤非扇形内的点，再打分
4. **靠边/靠心**：`PreferEdge=true` 按到场中心距离降序；`false` 按升序
5. **多点检测**（MinMutualDistance > 0）：选中第一个点 → 排除半径内其他点 → 选第二个 → 循环

## 性能考量

- 20×20 场地、0.5y 间距 ≈ 1600 个候选点 → 单次全检 < 0.2ms
- 40×40 场地、0.5y 间距 ≈ 6400 个点 → < 1ms
- `SampleGrid` 默认缓存：同一场地+同一间距的网格在 `IField` 实现层面惰性缓存

## 不包含

- 游戏数据自动读取（场地边界/AOE 由轴脚本定义）
- 移动执行（仅返回坐标）
- GJK 碰撞检测（不需要该精度）
