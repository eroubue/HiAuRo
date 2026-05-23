# ImGui 高级 UI 特效与动画可行性研究报告

> 针对 HiAuRo 项目 (FFXIV Dalamud 插件) 的 ImGui Overlay 渲染场景

## 0. 项目现状分析

HiAuRo 的 ImGui 层已具备以下基础能力：

| 已有能力 | 对应文件 | 说明 |
|----------|----------|------|
| 手动 DrawList 渲染 | `OverlayBase.cs` | 圆角背景、投影阴影、1px 细边框 |
| 基础动画工具 | `AnimationHelper.cs` | Lerp + SmoothLerp（指数衰减平滑跟随） |
| Ant Design 5 主题系统 | `Theme.cs` | 亮色/暗色双主题，毛玻璃色彩令牌 |
| 组件库 | `ComponentLibrary.cs` | 按钮、开关、滑块、标签、徽标等 Antd 风格组件 |
| 图标字体 | `IconHelper.cs` | 504 个 Game-Icon-Pack 图标 (PUA U+EA00+) |
| 窗口系统 | `OverlayBase.cs` | 无边框透明窗口、手动拖拽/缩放、位置持久化 |

**关键观察**：项目已经在用 `ImDrawList` 手动绘制一切（圆角背景、阴影、边框），完全绕过了 ImGui 默认的窗口装饰。这为高级特效打下了良好基础。

---

## 1. ImGui 原生能力

### 1.1 ImDrawList 绘图 API

ImGui 的 `ImDrawList` 提供了完整的 2D 矢量绘图能力：

| API 类别 | 具体方法 | 能力 |
|----------|----------|------|
| **图元** | `AddLine`, `AddRect`, `AddRectFilled`, `AddCircle`, `AddCircleFilled`, `AddTriangleFilled`, `AddQuadFilled` | 基础形状 |
| **路径** | `PathClear`, `PathLineTo`, `PathArcTo`, `PathBezierCurveTo`, `PathStroke`, `PathFillConvex` | 任意路径绘制 |
| **文本** | `AddText` (支持指定字体) | 文字渲染 |
| **图像** | `AddImage`, `AddImageRounded` | 纹理贴图（游戏图标、自定义贴图） |
| **多边形** | `AddConvexPolyFilled`, `AddPolyline` | 任意多边形 |
| **通道** | `ChannelsSplit`, `ChannelsMerge`, `ChannelsSetCurrent` | 绘制层级控制（分层渲染） |
| **裁剪** | `PushClipRect`, `PopClipRect` | 裁剪区域 |

**结论**：DrawList 是一个功能完整的 2D Canvas。理论上，任何 2D 图形效果都可以通过 DrawList 的 Path + 图元组合实现。

### 1.2 内置动画/过渡支持

ImGui **没有**内置的动画系统。它是一个即时模式 GUI，每一帧都是完整重绘：

- **无帧间状态**：Widget 不会记住"上一次的位置"或"上一次的颜色"
- **无补间/Tween**：没有 `transition` 或 `animation` 概念
- **有 delta time**：`ImGui.GetIO().DeltaTime` 提供帧间隔时间
- **有 hover/active 状态**：`IsItemHovered()`, `IsItemActive()` 等查询方法

唯一接近"动画"的内置行为：
- `ImGuiWindowFlags.Popup` 窗口有淡入淡出（由 ImGui 内部处理）
- ProgressBar 的进度条有轻微的条纹动画（`ImGuiCol_PlotHistogram` 条纹）

**结论**：所有动画必须自行实现——在每帧的 `Draw()` 中手动维护状态并插值。HiAuRo 的 `AnimationHelper.SmoothLerp` 已验证了这种模式的可行性。

### 1.3 颜色/渐变/圆角/阴影

| 视觉效果 | 原生支持程度 | 说明 |
|----------|-------------|------|
| **纯色填充** | 完全支持 | `AddRectFilled` 等 |
| **线性渐变** | **不支持** | ImDrawList 没有渐变 API |
| **径向渐变** | **不支持** | 同上 |
| **圆角** | 完全支持 | `AddRectFilled(min, max, color, rounding)` |
| **阴影** | 模拟实现 | 通过偏移绘制多个半透明矩形（项目已在使用，见 `OverlayBase.DrawWindowBackground`） |
| **边框** | 完全支持 | `AddRect` 可指定粗细 |
| **透明度** | 完全支持 | Vector4 的 W 分量即 alpha |

**渐变的替代方案**：
1. **分段渐变**：将区域切成 N 条矩形，每条颜色逐步过渡。可行但 draw call 数量 × N。
2. **纹理渐变**：预渲染一张 1×256 的渐变纹理，用 `AddImage` 拉伸。推荐方案，零额外 draw call。
3. **自定义 Shader**：通过 DX11 注入 shader 实现真正的渐变。成本最高。

---

## 2. 常见 ImGui 特效实现方案

### 2.1 粒子系统

**可行性：可以实现，需要自行实现。**

实现方式：
```
每帧 Draw() 中：
  1. 更新粒子状态（位置、速度、生命值、透明度）
  2. 遍历活跃粒子，用 DrawList 绘制每个粒子
     - AddCircleFilled / AddRectFilled 小图元
     - 或 AddImage 使用粒子纹理
```

关键设计点：
- **粒子数据结构**：`struct Particle { Vector2 Pos, Vel; float Life, MaxLife; Vector4 Color; float Size; }`
- **对象池**：预分配数组，避免 GC。`Particle[] _pool; int _activeCount;`
- **发射器**：控制粒子的生成位置、方向、速率
- **生命周期**：每帧 `Life -= DeltaTime`，Death 时回收

性能考量：
- 100 个粒子 ≈ 100 次 `AddCircleFilled`，对 DrawList 几乎无压力
- 1000+ 粒子需要注意：使用 `Span<Particle>` + 批量绘制同色粒子
- 粒子纹理（`AddImage`）比矢量图元性能更好
- **HiAuRo 场景建议**：50-200 粒子范围完全可行（技能触发特效、状态变化反馈）

示例伪代码：
```csharp
// 在 OverlayBase 子类中
private Particle[] _particles = new Particle[256];
private int _activeCount;

protected override void DrawContent()
{
    var dl = ImGui.GetWindowDrawList();
    var dt = ImGui.GetIO().DeltaTime;
    var basePos = ImGui.GetWindowPos();

    // 更新 + 绘制粒子
    for (int i = 0; i < _activeCount; i++)
    {
        ref var p = ref _particles[i];
        p.Life -= dt;
        if (p.Life <= 0) { /* 回收 */ continue; }
        p.Pos += p.Vel * dt;
        var alpha = p.Life / p.MaxLife;
        var color = new Vector4(p.Color.X, p.Color.Y, p.Color.Z, alpha);
        dl.AddCircleFilled(basePos + p.Pos, p.Size * alpha, ColorU32(color));
    }
}
```

### 2.2 平滑动画（补间/Tweening/Easing）

**可行性：简单直接。HiAuRo 已有基础。**

`AnimationHelper.cs` 已实现了：
- `Lerp(float/Vector2/Vector4)` — 线性插值
- `SmoothLerp(ref current, target, speed)` — 指数衰减平滑跟随

推荐的缓动函数扩展：

```csharp
public static class Easing
{
    // 经典缓动函数（t 范围 0~1）
    public static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - t, 3f);
    public static float EaseInOutCubic(float t) =>
        t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;
    public static float EaseOutElastic(float t)
    {
        if (t == 0 || t == 1) return t;
        return MathF.Pow(2f, -10f * t) * MathF.Sin((t * 10f - 0.75f) * (2f * MathF.PI) / 3f) + 1f;
    }
    public static float EaseOutBounce(float t) { /* ... */ }

    // 帧无关的动画驱动器
    public static float Animate(ref float progress, float duration, float speed = 1f)
    {
        progress += ImGui.GetIO().DeltaTime * speed;
        return Math.Clamp(progress / duration, 0f, 1f);
    }
}
```

**使用模式**：在窗口类中维护 `float _animProgress`，每帧更新并用缓动函数映射。

适用于 HiAuRo 的场景：
- 窗口展开/折叠动画（`OverlayStatusBar` 已有折叠功能，可加动画）
- 按钮悬停色渐变（代替当前的突然变色）
- QT 芯片切换时的过渡动画
- 技能图标冷却倒计时动画

### 2.3 发光/辉光效果

**可行性：可以模拟，但不能用 Shader。**

| 效果 | 实现方式 | 性能影响 |
|------|----------|----------|
| **文字发光** | 多层 `AddText`，每层加大字号并降低透明度 | 低（3-5 次 AddText） |
| **边框发光** | 多层 `AddRect`，逐步增大圆角和线宽，降低透明度 | 低 |
| **按钮脉冲高亮** | 用正弦波驱动颜色 alpha 值 + 多层边框 | 低 |
| **真正的 Bloom** | 需要 DX11 Shader 后处理 | 高（需要渲染目标） |

文字发光示例：
```csharp
static void GlowText(ImDrawListPtr dl, Vector2 pos, string text, Vector4 color, float glowRadius = 3f)
{
    var u32 = ColorU32(color);
    // 绘制 4 个偏移的模糊层
    for (int i = 0; i < 4; i++)
    {
        var angle = i * MathF.PI / 2f;
        var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * glowRadius;
        var glowColor = new Vector4(color.X, color.Y, color.Z, color.W * 0.3f);
        dl.AddText(pos + offset, ColorU32(glowColor), text);
    }
    // 绘制主体文字
    dl.AddText(pos, u32, text);
}
```

边框脉冲发光示例（适用于 HiAuRo 的运行状态指示）：
```csharp
float pulse = (MathF.Sin((float)ImGui.GetTime() * 3f) + 1f) * 0.5f; // 0~1 脉冲
var glowAlpha = 0.2f + pulse * 0.4f;
dl.AddRect(min - new Vector2(2), max + new Vector2(2),
    ColorU32(new Vector4(0.09f, 0.47f, 1f, glowAlpha)), radius + 2, 0, 2f);
```

### 2.4 波纹/涟漪效果

**可行性：可以实现。**

用 DrawList 的 `PathArcTo` 画一个逐渐扩大并淡出的圆环：

```csharp
struct Ripple { Vector2 Center; float Progress; float MaxRadius; }

// 每帧更新
ripple.Progress += dt * 2f; // 0.5 秒完成
if (ripple.Progress >= 1f) { /* 移除 */ }

// 绘制
var radius = ripple.MaxRadius * ripple.Progress;
var alpha = 1f - ripple.Progress;
dl.AddCircle(ripple.Center, radius, ColorU32(new Vector4(1,1,1, alpha * 0.3f)), 32, 2f);
```

HiAuRo 应用场景：QT 芯片切换时的点击反馈，Hotkey 按钮按下时的涟漪。

### 2.5 模糊/毛玻璃背景

**可行性：模拟可行，真正 blur 极难。**

| 方案 | 效果 | 难度 | 说明 |
|------|------|------|------|
| **静态半透明色** | ★★☆ | 极低 | 项目已在用（`GlassBg` 颜色令牌） |
| **多层叠加** | ★★★ | 低 | 底色 + 高光条 + 暗部，模拟毛玻璃质感（项目已实现） |
| **预渲染模糊贴图** | ★★★★ | 中 | 在初始化时截取游戏画面做高斯模糊，作为背景纹理。但无法实时跟踪游戏画面变化 |
| **实时读取后台缓冲** | ★★★★★ | 极高 | 需要访问 DX11 SwapChain，读回后台纹理做模糊。在 Dalamud 中可行但性能极差 |
| **DX11 Compute Shader** | ★★★★★ | 极高 | 编写 compute shader 做高斯/box blur。技术上可行但 Dalamud 环境中不稳定 |

**HiAuRo 的当前方案是最佳实践**：多层半透明叠加（底色 + 高光 + 暗部 + 细边框 + 投影）。对于 Overlay 场景，视觉效果已足够好。不建议投入精力做实时模糊。

### 2.6 3D 变换（旋转/缩放/倾斜）

**可行性：极其有限。**

ImDrawList 是纯 2D 的，没有变换矩阵。但可以通过以下方式模拟：

| 变换 | 模拟方式 | 逼真度 |
|------|----------|--------|
| **缩放** | 调整绘制区域大小 | 完美（直接修改 min/max） |
| **透明度渐变** | 调整颜色 alpha | 完美 |
| **X/Y 轴旋转** | 修改矩形的宽/高比例（透视压缩） | 一般（只能做简单透视） |
| **倾斜 (Skew)** | 修改四个顶点坐标（用 Quad 代替 Rect） | 一般 |
| **任意旋转** | 用 `PathLineTo` 手动旋转所有顶点坐标 | 可以但繁琐 |
| **Z 轴旋转 + 纹理** | `AddImage` 不支持旋转 | **不可行**（需要纹理旋转的 draw call） |

旋转示例（纯矢量图元）：
```csharp
static void DrawRotatedRect(ImDrawListPtr dl, Vector2 center, Vector2 size, float angle, uint color)
{
    var cos = MathF.Cos(angle);
    var sin = MathF.Sin(angle);
    var half = size / 2f;
    Span<Vector2> corners = stackalloc Vector2[4];
    for (int i = 0; i < 4; i++)
    {
        var local = new Vector2(
            (i % 2 == 0 ? -1 : 1) * half.X,
            (i < 2 ? -1 : 1) * half.Y);
        corners[i] = center + new Vector2(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos);
    }
    dl.AddQuadFilled(corners[0], corners[1], corners[2], corners[3], color);
}
```

**结论**：简单的缩放/脉冲动画很适合 HiAuRo（如技能图标按下缩放、QT 芯片切换缩放）。旋转不推荐。

### 2.7 自定义着色器

**可行性：技术上可能，但强烈不推荐。**

Dalamud 使用 DX11 渲染 ImGui。理论上可以：
1. 获取 `ID3D11Device` 和 `ID3D11DeviceContext`
2. 创建自定义 vertex/pixel shader
3. 在 ImGui 渲染前/后注入自定义渲染 Pass

**风险**：
- **与 Dalamud 渲染管线冲突**：Dalamud 的渲染框架会管理 ImGui 的 DX11 状态
- **与游戏渲染冲突**：可能破坏游戏的渲染状态
- **维护成本极高**：Dalamud 版本更新可能导致 API 变化
- **性能不可预测**：Shader 编译、状态切换的开销

**结论**：对 HiAuRo 而言，自定义 Shader 的 ROI 极低。不推荐。

### 2.8 SVG/矢量图标动画

**可行性：通过 DrawList Path 完全可行。**

HiAuRo 已有 `IconHelper` + Game-Icon-Pack TTF 字体。在此基础上可以实现：

| 动画类型 | 实现方式 | 复杂度 |
|----------|----------|--------|
| **图标缩放** | 调整 `DrawIcon` 的 `sizePx` 参数 | 低 |
| **图标旋转** | 在 `IconHelper.DrawIcon` 内部对坐标做旋转 | 中 |
| **图标淡入淡出** | 调整颜色的 alpha 分量 | 低 |
| **路径动画（描边）** | 用 DrawList Path 逐步绘制路径 | 中高 |
| **路径变形** | 需要插值两组 Path 的控制点 | 高 |

HiAuRo 最实用的图标动画：
- 技能冷却时的"扫扇"遮罩（用 `PathArcTo` 画扇形覆盖图标）
- 状态切换时的缩放弹跳（用 `EaseOutElastic` 缓动）
- 运行中图标的旋转脉冲

---

## 3. 第三方库/扩展方案

### 3.1 ImGui 扩展库

| 库 | 特效能力 | 与 HiAuRo 的适用性 |
|-----|----------|-------------------|
| **ImGuizmo** | 3D 变换 Gizmo（平移/旋转/缩放操纵器） | 不适用（3D 场景编辑工具） |
| **ImPlot** | 图表/绘图（折线、柱状、散点、热力图等） | 低（有实时数据可视化需求时有用） |
| **imgui_club** | `imgui_memory_editor`, `imgui_toggle`, `imgui_spin_value` | 中（toggle 动画可参考） |
| **ImSequencer** | 时间线序列编辑器 | 不适用 |
| **imgui-node-editor** | 节点图编辑器（连线、拖拽） | 不适用 |

### 3.2 专门的 ImGui 动画/特效库

| 库 | 说明 | GitHub |
|----|------|--------|
| **imgui_anim** | 简单的 ImGui 动画包装器（淡入、滑动、缩放） | 小型单头文件库 |
| **ImGuiTween** | 补间动画库，支持多种缓动函数 | 个人项目 |
| **imgui_markdown** | Markdown 渲染（有链接高亮等微动画） | 可参考交互模式 |

**现实情况**：ImGui 生态中**没有**成熟的、广泛使用的动画/特效专用库。大多数项目都是自行实现轻量的动画工具。这与 HiAuRo 当前 `AnimationHelper` 的做法一致。

### 3.3 Dalamud 生态中的 ImGui 特效

Dalamud 插件社区中一些知名项目使用了特效：

| 项目 | 特效 | 实现方式 |
|------|------|----------|
| **DelvUI** | 大量自定义 HUD 元素（血条、CD 计时器、Buff 图标） | DrawList + 自定义动画系统 |
| **BossMod** | 战术雷达、AOE 圆环、箭头指示 | DrawList Path + 游戏世界坐标投影 |
| **Penumbra** | 预览窗口、模型旋转 | 基本无特效，功能导向 |
| **Glamourer** | 设置界面 | 标准 ImGui 控件 |
| **Browsingway** (HiAuRo 子模块) | CEF 渲染 + DX11 纹理共享 | CEF offscreen → DX11 纹理 → ImGui AddImage |

**DelvUI 是最佳参考**：它是 Dalamud 生态中 ImGui 特效的天花板。其核心模式就是 `ImDrawList` 手绘 + 自定义动画 + 纹理贴图，与 HiAuRo 的技术路线一致。

---

## 4. 架构层面的考虑

### 4.1 即时模式的动画限制

ImGui 即时模式的核心约束：
- **无帧间状态**：每帧的 `Draw()` 是纯函数调用，不会自动记住上一帧的值
- **解决方式**：在窗口类（或静态类）中用字段保存动画状态

HiAuRo 已采用的正确模式：
```csharp
// 在 AnimationHelper.cs 中
public static float SmoothLerp(ref float current, float target, float speed)
{
    var dt = ImGui.GetIO().DeltaTime;
    current = Lerp(current, target, 1f - MathF.Exp(-speed * dt));
    return current;
}
```

这个模式应该被推广：每个需要动画的 Overlay 窗口维护自己的动画状态字段，在 `Draw()` 中更新并绘制。

### 4.2 性能考量

| 操作 | 性能影响 | 备注 |
|------|----------|------|
| DrawList AddRect/AddCircle | **极低** | CPU 端只是往列表添加顶点，GPU 一次提交 |
| 100 次图元绘制 | 极低 | ImGui 本身每帧绘制数千个图元 |
| 1000+ 次图元绘制 | 低 | 开始有可见影响 |
| AddImage（纹理） | 极低 | 比矢量图元更快（一个四边形） |
| 文字渲染 (AddText) | 低-中 | 取决于字体图集的 cache 情况 |
| 100 粒子系统 | 低 | 100 次 AddCircleFilled |
| 1000 粒子系统 | 中 | 需要优化：合并同色粒子、对象池 |
| 实时截图做模糊 | **极高** | GPU→CPU→GPU 回读，严重拖帧 |
| 自定义 Shader Pass | **高** | 状态切换 + Shader 编译 |

**优化策略**：
1. **帧率无关动画**：始终用 `DeltaTime` 驱动，不用帧计数
2. **对象池**：粒子、涟漪等效果预分配数组，避免 GC
3. **距离裁剪**：超出窗口可见区域的粒子不绘制
4. **状态懒更新**：不可见的窗口不更新动画
5. **颜色批处理**：相同颜色的 DrawList 调用尽量连续

### 4.3 Dalamud 插件的 Overlay 特殊性

HiAuRo 的 Overlay 渲染有这些特点：
- **透明背景**：`ImGuiWindowFlags.NoBackground` 禁用了默认背景，使用自定义 DrawList 绘制
- **游戏上层渲染**：ImGui 在游戏画面之上渲染，不影响游戏帧率（但自身受限于 Dalamud 的渲染回调频率）
- **窗口系统**：通过 Dalamud 的 `WindowSystem` 管理，每个 Overlay 是一个 `Window` 子类
- **CEF 集成**：Browsingway 提供 CEF → DX11 纹理 → `AddImage` 的管线

**这意味着**：
- Overlay 的特效不能使用游戏世界空间（不像 BossMod 的雷达）
- 特效只需要在窗口矩形区域内绘制
- 透明背景 + 半透明特效在游戏画面上效果很好（已有的毛玻璃风格就是证明）

### 4.4 DirectX 11 交互

Dalamud 使用 DX11 渲染。理论上可以访问：
- `ID3D11Device`（通过 Dalamud 的服务接口）
- `ID3D11DeviceContext`
- `IDXGISwapChain`

**但**：
- 直接操作 DX11 设备会与 Dalamud 的渲染管线产生冲突
- Dalamud 对 ImGui 的渲染有完整的状态管理
- 不建议在 HiAuRo 的 ImGui 层直接使用 DX11 API

**唯一推荐的 DX11 交互**：通过 Browsingway 的 CEF → DX11 纹理管线，将 Web 渲染的结果作为贴图叠加到 ImGui 中。HiAuRo 已具备此能力。

---

## 5. 实际案例与参考

### 5.1 知名的 ImGui 视觉效果项目

| 项目 | 领域 | 视觉亮点 | 参考价值 |
|------|------|----------|----------|
| **DelvUI** (Dalamud) | FFXIV HUD | 自定义血条、Buff 计时器、冷却环、迷你地图 | **极高** — 同生态、同技术栈 |
| **OBS Studio** | 直播软件 | 场景切换动画、音量条动画、拖拽高亮 | 高 — 展示了 ImGui 做精致 UI 的上限 |
| **Unreal Engine Editor** | 游戏引擎 | 完整的节点编辑器、动画时间线、材质预览 | 中 — UE 有大量定制，难以直接参考 |
| **Godot Engine Editor** | 游戏引擎 | 节点树动画、Inspector 面板过渡 | 中 |
| **Tracy Profiler** | 性能分析 | 实时图表、Zone 时间线、帧率曲线 | 高 — 纯 ImGui + DrawList 实现流畅图表 |
| **MeshCraft** | 3D 建模 | 视口渲染 + ImGui UI 叠加 | 低 |

### 5.2 Dalamud 生态中的特效实例

**DelvUI** 是必须研究的参考：
- 使用 `ImDrawList` 手绘所有 HUD 元素
- 实现了冷却计时器的"扫扇"动画（用 PathArcTo 画扇形遮罩）
- 血条平滑过渡（类似 HiAuRo 的 SmoothLerp）
- Buff 图标的倒计时数字 + 渐变边框

### 5.3 值得参考的开源项目

| 项目 | 参考点 | 地址 |
|------|--------|------|
| **DelvUI** | Dalamud ImGui 特效的天花板 | `github.com/DelvUI/delvui` |
| **Tracy** | 纯 DrawList 图表/时间线 | `github.com/wolfpld/tracy` |
| **imgui demos** | imgui_demo.cpp 中的 Custom Rendering 部分 | ImGui 源码 |
| **HiAuRo/AnimationHelper.cs** | 项目自有的平滑跟随模式 | 已实现 |
| **HiAuRo/ComponentLibrary.cs** | DrawList 手绘组件的模式 | 已实现 |

---

## 6. 可行性结论

### 6.1 简单直接（推荐立即实施）

| 特效 | 实现成本 | 效果 | 说明 |
|------|----------|------|------|
| **按钮悬停平滑过渡** | 半天 | 高 | 用 SmoothLerp 插值悬停颜色，代替当前突变 |
| **状态脉冲动画** | 2 小时 | 高 | 正弦波驱动边框/图标的 alpha 脉冲（运行状态指示） |
| **技能冷却扇形遮罩** | 1 天 | 极高 | PathArcTo 画扇形，覆盖技能图标 |
| **窗口展开/折叠动画** | 半天 | 中 | 高度插值 + 缓动函数 |
| **点击涟漪** | 4 小时 | 中 | QT 芯片 / 热键按钮的点击反馈 |
| **图标缩放弹跳** | 2 小时 | 中 | EaseOutElastic 缓动 + DrawIcon sizePx 参数 |
| **文字发光** | 1 小时 | 高 | 多层 AddText 叠加，用于关键状态文本 |

### 6.2 可以实现但需要较多工作

| 特效 | 实现成本 | 效果 | 说明 |
|------|----------|------|------|
| **粒子系统** | 2-3 天 | 高 | 需要设计粒子数据结构、发射器、对象池 |
| **进度条/CD条动画** | 1-2 天 | 高 | 带渐变、脉冲、圆角的动画条 |
| **通知弹窗 (Toast)** | 2-3 天 | 中 | 滑入/滑出 + 淡入淡出 + 自动消失 |
| **图标旋转动画** | 1 天 | 中 | 需要修改 IconHelper 支持旋转参数 |
| **渐变背景** | 1-2 天 | 中 | 预渲染渐变纹理或分段绘制 |
| **迷你实时图表** | 3-5 天 | 中 | DPS 曲线、Buff 覆盖率等（参考 Tracy） |

### 6.3 几乎不可行或性价比极低

| 特效 | 原因 | 建议 |
|------|------|------|
| **实时背景模糊** | 需要读取 DX11 后台缓冲，性能极差 | 保持当前的多层半透明方案 |
| **自定义 Shader 特效** | 与 Dalamud 渲染管线冲突风险高 | 不推荐 |
| **3D 变换/透视** | ImDrawList 是 2D 的，模拟效果差 | 不推荐 |
| **SVG 路径变形动画** | 实现复杂，ROI 低 | 不推荐 |
| **视频/动画纹理** | 需要持续的纹理上传，性能差 | 用 CEF + Web 方案替代 |

### 6.4 对 HiAuRo 的具体建议

#### 值得投入的特效（按优先级排序）

1. **技能冷却扇形遮罩** (P0)
   - 对战斗辅助插件来说是核心功能
   - 技术上只需 `PathArcTo` + 角度计算
   - 效果立竿见影

2. **状态脉冲动画** (P0)
   - 运行中/暂停/停止的状态指示需要明确的视觉反馈
   - OverlayStatusBar 的运行状态边框做脉冲发光
   - 实现极简：`MathF.Sin(time * freq)` 驱动 alpha

3. **悬停/点击微动画** (P1)
   - QT 芯片、Hotkey 按钮、控制按钮的悬停颜色过渡
   - 点击时的缩放弹跳（scale 1.0 → 0.95 → 1.0）
   - 涟漪效果

4. **窗口展开/折叠动画** (P1)
   - OverlayStatusBar 的折叠/展开用高度插值平滑过渡
   - 当前是瞬间切换，加动画后体验显著提升

5. **文字发光/高亮** (P1)
   - 关键状态文字（如"运行中"）加发光效果
   - 错误/警告信息加红色脉冲

6. **粒子系统** (P2)
   - 技能触发成功时的粒子爆发
   - 状态切换时的粒子特效
   - 锦上添花，非核心

#### 不推荐投入的特效

- **实时背景模糊**：当前的多层毛玻璃方案已经足够好
- **自定义 Shader**：维护成本和风险远大于收益
- **3D 变换**：ImDrawList 不支持，强做效果差
- **复杂图表**：如果需要数据可视化，用 Web UI (CEF) 方案更合适

---

## 7. 推荐的实现架构

基于项目现有模式，建议的动画架构：

```
AnimationHelper.cs  (已有，保持不变)
    ├── Lerp, SmoothLerp

Easing.cs  (新增)
    ├── EaseOutCubic, EaseInOutCubic, EaseOutElastic, etc.

ParticlePool.cs  (新增，按需)
    ├── Particle 结构体
    ├── 发射/更新/绘制

OverlayEffects.cs  (新增，静态工具类)
    ├── GlowText()
    ├── PulseRect()
    ├── RippleEffect()
    ├── CooldownSector()
    └── BounceScale()
```

关键原则：
- **保持轻量**：不需要动画框架，只需要几个静态工具方法
- **帧率无关**：所有动画用 `DeltaTime` 驱动
- **按需加载**：不是每个窗口都需要所有特效
- **渐进增强**：先做 P0 特效，验证效果后再扩展

---

## 8. 参考资源

- [ImGui Wiki - Custom Rendering](https://github.com/ocornut/imgui/wiki/Image-Loading-and-Displaying-Examples)
- [ImGui Demo Source](https://github.com/ocornut/imgui/blob/master/imgui_demo.cpp) — "Custom Rendering" section
- [DelvUI Source Code](https://github.com/DelvUI/delvui) — Dalamud 生态最佳参考
- [Tracy Profiler](https://github.com/wolfpld/tracy) — 纯 DrawList 图表
- [Easing Functions Cheat Sheet](https://easings.net/) — 缓动函数参考
- [Ant Design Motion](https://ant.design/docs/spec/motion-cn) — HiAuRo 主题体系的动画规范
