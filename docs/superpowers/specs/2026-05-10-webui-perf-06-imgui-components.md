# WebUI 性能优化 — 方向 6: ImGui 组件库 + Ant Design 设计系统

## 问题

当前 Web UI（CEF + WebSocket + HTML/CSS/JS）存在：

- 交互延迟（WebSocket 来回 3-6ms）
- 内存开销（CEF 子进程 ~200-300MB）
- 多语言维护（C# + JS + CSS）
- 跨进程 IPC 复杂度

ImGui 可以解决以上全部问题，但 ImGui 原生控件缺乏统一的设计规范。

## 目标

参照 **Ant Design 5.0 设计语言** 和 **EUI-NEO 交互风格**，在 ImGui 上构建 HiAuRo 自己的组件库：

1. **设计系统** — 统一的色彩、间距、圆角规范
2. **通用组件** — Button、Switch、Slider、Select、Tabs、Tag 等
3. **示例窗口** — 展示全部组件效果和视觉风格（类似 Ant Design 组件画廊）
4. **HiAuRo 面板** — StatusBar、ActionPanel、SettingsPanel（组合通用组件）
5. **IUiBuilder 兼容** — ACR 作者接口不变，`UiControlDef` 自动映射到组件

## 架构

```
┌─────────────────────────────────────────────────┐
│  IUiBuilder (ACR 作者接口，不变)                   │
│  ↓                                               │
│  UiBuilderImpl → List<UiControlDef> (不变)        │
│  ↓                                               │
│  ═══════ 新增层：ImGui 渲染器 ═══════               │
│  ↓                                               │
│  ImGuiWidgetRenderer                             │
│    │  将 UiControlDef → ImGui 组件                │
│    │  checkbox → Switch (动画开关)                 │
│    │  slider   → Slider (平滑滑块)                 │
│    │  dropdown → Select (下拉选择器)               │
│    │  tab      → Tabs (标签页)                    │
│    │  group    → Card (分组卡片)                  │
│    │  separator→ Divider (分割线)                 │
│    │  intInput → InputNumber (数字输入)           │
│    │  label    → Label (纯文本)                   │
│    ↓                                               │
│  ImGuiComponentLibrary                            │
│    ├─ Theme.cs         设计令牌（颜色/间距/圆角）     │
│    ├─ Button.cs        通用按钮组件                 │
│    ├─ Switch.cs        动画开关组件                 │
│    ├─ Slider.cs        滑块组件                    │
│    ├─ Select.cs        下拉选择器                   │
│    ├─ Tabs.cs          标签页组件                   │
│    ├─ Card.cs          分组卡片容器                 │
│    ├─ Tag.cs           标签芯片（替换 QT chip）       │
│    ├─ Divider.cs       分割线                      │
│    ├─ Badge.cs         状态点/徽标                  │
│    └─ Notification.cs  通知提示                    │
│    ↓                                               │
│  HiAuRoPanels                                     │
│    ├─ DemoWindow.cs         组件展示窗口（开发/预览用）  │
│    ├─ OverlayStatusBar.cs   状态栏 + ACR 控制面板    │
│    ├─ OverlayActionPanel.cs QT + 热键面板           │
│    └─ OverlaySettings.cs   设置面板                │
│    ↓                                               │
│  ImGui (Dalamud UiBuilder.Draw 回调)                │
└─────────────────────────────────────────────────┘
```

## 设计系统 (Theme.cs)

基于 Ant Design 5.0 设计令牌，适配 ImGui 的 `PushStyleColor` / `PushStyleVar`：

```csharp
public static class Theme
{
    // 色彩（Dark 模式为默认）
    public static class Colors
    {
        // 背景层
        public const uint BgLayout    = 0xFF141414; // 页面底色
        public const uint BgContainer = 0xFF1C1C1E; // 容器底色
        public const uint BgElevated  = 0xFF2A2A2E; // 浮层/卡片底色

        // 文字
        public const uint TextPrimary   = 0xFFE8E8E8;
        public const uint TextSecondary = 0xFFA0A0A0;
        public const uint TextTertiary  = 0xFF808080;

        // 品牌色
        public const uint AccentBlue   = 0xFF1677FF;
        public const uint AccentGreen  = 0xFF30D158;
        public const uint AccentRed    = 0xFFFF453A;
        public const uint AccentOrange = 0xFFFF9F0A;

        // 边框
        public const uint Border       = 0xFF333333;
        public const uint BorderActive = 0xFF1677FF;
    }

    // 圆角
    public const float RadiusXS  = 4f;
    public const float RadiusSM  = 6f;
    public const float RadiusMD  = 8f;
    public const float RadiusLG  = 12f;

    // 间距
    public static readonly Vector2 PaddingXS  = new(4, 2);
    public static readonly Vector2 PaddingSM  = new(8, 4);
    public static readonly Vector2 PaddingMD  = new(12, 8);
    public static readonly Vector2 ItemSpacing = new(8, 6);

    // 字体大小
    public const float FontSizeSM = 11f;
    public const float FontSizeMD = 13f;
    public const float FontSizeLG = 16f;

    // 动画（通过 ImGui.GetTime() 驱动 lerp）
    public const float AnimDuration = 0.15f;  // 过渡时长
    public const float AnimEasing  = 0.3f;   // 缓动系数
}
```

## 通用组件库

每个组件封装为 `static bool Render*(...)` 方法，返回是否发生了交互：

| 组件 | 对应 ImGui 基础 | 增强特性 |
|------|---------------|---------|
| `Button` | `ImGui.Button` + 样式 Push | 圆角、hover 颜色渐变、disabled 态 |
| `Switch` | `ImGui.Checkbox` + 自定义绘制 | 动画 toggle（按钮滑动）、绿色/灰色状态 |
| `Slider` | `ImGui.SliderInt`/`SliderFloat` | Ant Design 风格滑轨 + 圆点 |
| `Select` | `ImGui.BeginCombo` | 统一弹出窗样式、搜索高亮 |
| `Tabs` | `ImGui.BeginTabBar` + `TabItem` | 下划线指示器、均匀分布 |
| `Card` | `ImGui.BeginChild` | 统一边框 + 圆角 + 内边距 |
| `Tag` | `ImGui.Button` 小号 | 圆角胶囊、多色（绿/蓝/橙/灰） |
| `Divider` | `ImGui.Separator` | 统一粗细 + 颜色 + 留白 |
| `Badge` | `ImGui.GetWindowDrawList()` | 圆点状态指示、带数字徽标 |
| `InputNumber` | `ImGui.InputInt` | 统一样式 + step 按钮 |

**示例：Switch 组件**

```csharp
public static bool Switch(string id, ref bool value)
{
    ImGui.PushID(id);
    ImGui.PushStyleColor(ImGuiCol.FrameBg,    value ? Theme.Colors.AccentGreen : Theme.Colors.BgElevated);
    ImGui.PushStyleColor(ImGuiCol.Button,     0xFFFFFFFF); // thumb 白色
    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFFFFFFFF);
    ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 12f);
    ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2, 2));

    // 40x24 的开关框 + 20x20 的圆形 thumb
    var changed = ImGui.Checkbox($"##{id}", ref value);

    ImGui.PopStyleVar(2);
    ImGui.PopStyleColor(3);
    ImGui.PopID();
    return changed;
}
```

## HiAuRo 专属面板

### DemoWindow — 组件展示窗口

在开发阶段和后续 ACR 作者查阅时，通过 `/hi` 命令打开一个组件画廊窗口，展示所有通用组件的外观和交互：

```
┌─────────────────────────────────────────┐
│  HiAuRo Component Gallery               │
├─────────────────────────────────────────┤
│  [按钮]  [开关]  [标签页]  [卡片]  [标签] │  ← 分类 Tabs
├─────────────────────────────────────────┤
│                                         │
│  Button                                 │
│  ┌──────────┐ ┌──────────┐             │
│  │  主按钮   │ │  默认按钮 │             │  ← 多种 button 变体
│  └──────────┘ └──────────┘             │
│                                         │
│  Switch                                 │
│  ☑ AoE 技能               [===○]       │  ← 动画状态
│  ☐ 爆发药                 [○===]       │
│                                         │
│  Slider                                 │
│  攻击距离  [━━━━━●━━━━━] 25.0          │
│                                         │
│  Select                                 │
│  技能顺序  [ 选择选项 ▼]               │
│                                         │
│  Tag                                    │
│  [AoE] [爆发] [疾跑] [吃药] [防击退]    │  ← 多色标签
│                                         │
│  Badge                                  │
│  ● 运行中    ● 已暂停    ● 已停止        │  ← 状态点
│                                         │
│  Theme: ■ Dark  ■ Light                 │  ← 主题切换预览
└─────────────────────────────────────────┘
```

- 通过 Dahamud WindowSystem 注册，`/hi gallery` 或 `/hi demo` 打开
- 展示所有组件的每种状态（正常 / hover / active / disabled）
- 展示暗色 / 亮色两种主题效果
- 可作为 ACR 作者的 UI 参考文档

### OverlayStatusBar — 状态栏 + ACR 控制面板

代替 `main.html` — 状态条 + 可展开面板 + Tab/分组控制。

```
┌─────────────────────────────────┐
│ ● 运行中          [⏸] [■] [▼] │  ← 状态栏（折叠时仅此行）
├─────────────────────────────────┤
│ HiAuRo           [启动][暂停][保存]│
│ ┌─────┬─────┬─────┬──────┐    │
│ │ Tab1 │ Tab2 │ Tab3 │ 设置 │    │  ← Tabs 组件
│ └─────┴─────┴─────┴──────┘    │
│ ┌──────────────────────────┐ │
│ │ 分组1                     │ │  ← Card 组件
│ │ ☑ 使用 AoE    [Switch]   │ │
│ │ ☑ 自动吃药    [Switch]   │ │  ← Switch 组件
│ │ ◎ 技能顺序    [Select ▼] │ │  ← Select 组件
│ └──────────────────────────┘ │
└─────────────────────────────────┘
```

- 折叠时：`ImGui.SetNextWindowSize` 限制高度为 ~48px（只显示状态栏）
- 展开时：窗口高度自适应内容（方向 1 的思路，但换为 ImGui 自行计算）
- Tab 切换：调用 `ImGuiWidgetRenderer.RenderControls(tabId)` 渲染对应标签的控件

### OverlayActionPanel — QT + 热键面板

代替 `qt.html` + `hotkey.html`（方向 2 的合并方案）。

```
┌──────────────────────┐
│ [AoE] [爆发] [吃药]   │  ← Tag 组件渲染 QT 芯片
│ [疾跑] [防击退]       │
├──────────────────────┤
│ [图标] [图标] [图标]  │  ← 热键网格（Icon + Label + Keybinding）
│ [图标] [图标] [图标]  │
└──────────────────────┘
```

- QT 芯片：`Tag` 组件，`on` 状态用绿色，`off` 状态用灰色
- 热键：`ImGui.ImageButton` 加载游戏图标 + 文字标签 + 快捷键角标
- 空区域自然收缩（无元素时高度为 0）

### OverlaySettings — 设置面板

QT 设置、热键设置、全局设置放在 StatusBar 展开后的设置 Tab 中。

## IUiBuilder 渲染器

`ImGuiWidgetRenderer` 接收 `List<UiControlDef>` 并按 Tab/Group 结构调用对应组件：

```csharp
public static void RenderControls(List<UiControlDef> controls, string activeTab)
{
    var groups = controls.Where(c => c.ParentId == activeTab && c.Type == "group");
    foreach (var group in groups)
    {
        Card.Begin(group.Label);
        var items = controls.Where(c => c.ParentId == group.Id);
        foreach (var item in items)
        {
            switch (item.Type)
            {
                case "checkbox":  Switch(item.Id, item.Label, ref state); break;
                case "slider":    Slider(item.Id, item.Label, ref val, min, max); break;
                case "dropdown":  Select(item.Id, item.Label, ref selectedIdx, options); break;
                case "intInput":  InputNumber(item.Id, item.Label, ref intVal); break;
                case "label":     Label(item.Value); break;
                case "separator": Divider(); break;
            }
        }
        Card.End();
    }
}
```

## 文件清单

### 新增

| 文件 | 用途 |
|------|------|
| `UI/ImGui/Theme.cs` | Ant Design 设计令牌（颜色/圆角/间距/字体/动画） |
| `UI/ImGui/Button.cs` | 通用按钮组件 |
| `UI/ImGui/Switch.cs` | 动画开关组件 |
| `UI/ImGui/Slider.cs` | 滑块组件 |
| `UI/ImGui/Select.cs` | 下拉选择器 |
| `UI/ImGui/Tabs.cs` | 标签页组件 |
| `UI/ImGui/Card.cs` | 分组卡片容器 |
| `UI/ImGui/Tag.cs` | 标签芯片组件 |
| `UI/ImGui/Divider.cs` | 分割线组件 |
| `UI/ImGui/InputNumber.cs` | 数字输入组件 |
| `UI/ImGui/ImGuiWidgetRenderer.cs` | UiControlDef → 组件映射渲染器 |
| `UI/ImGui/OverlayStatusBar.cs` | 状态栏 + ACR 控制面板浮动窗口 |
| `UI/ImGui/OverlayActionPanel.cs` | QT + 热键浮动窗口 |
| `UI/ImGui/DemoWindow.cs` | 组件展示窗口，`/hi gallery` 打开 |
| `UI/ImGui/AnimationHelper.cs` | 通用 lerp/easing 动画工具 |

### 修改

| 文件 | 改动 |
|------|------|
| `Plugin.cs` | 移除 CEF/WebUI 初始化，注册 ImGui overlay 窗口 |
| `Plugin_Browsingway.cs` | **删除**（不再需要 Browsingway） |
| `ACRLifecycle.cs` | 状态更新直接推给 overlay，不走 WebSocket |
| `UiBuilderImpl.cs` | 不变（接口完全兼容） |
| `MainWindow.cs` | 可选保留作为配置窗口 |

### 删除

| 删除项 | 原因 |
|--------|------|
| 整个 Browsingway 依赖 | 不再需要 CEF 渲染 |
| `WebUiServer.cs` | 不再需要 HTTP 服务器 |
| `WebUiBridge.cs` | 不再需要 WebSocket |
| `app.js` | 不再需要 |
| `style.css` | 不再需要 |
| `main.html` / `qt.html` / `hotkey.html` | 不再需要 |
| `puppertino-bridge.css` / `vendor/puppertino/` | 不再需要 |
| `IconServer.cs` | 图标直接走 `DService.Texture.GetIcon()` |
| 方向 1、2、3、5 的 spec | 全部被本方向覆盖（不再需要 CEF 相关优化） |

## 收益汇总

| 指标 | 改前（CEF Web UI） | 改后（ImGui 组件库） |
|------|------------------|-------------------|
| UI 交互延迟 | WebSocket 来回 3-6ms | **0ms**（同线程 C# 调用） |
| 内存 | CEF 进程 ~200-300MB | **0**（无子进程） |
| 部署体积 | CEF 二进制 ~150MB | **0**（无额外依赖） |
| 开发语言 | C# + JS + CSS | **仅 C#** |
| 设计一致性 | 手写 CSS | **统一设计系统** |
| ACR 接口兼容 | `IUiBuilder` | **不变** |
| 进程数 | 4（Plugin + 3x CEF 渲染器） | **1** |

## 前置依赖

本方向完全覆盖并替换方向 1、2、3、5。如果实施本方向，之前 4 份 spec 自动作废，DELETION 比 BUILD 多。
