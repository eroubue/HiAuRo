# ImGui 双模式切换 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 MainWindow 添加 WebUI/ImGui 切换控件，ImGui 模式下创建 ImGui 组件库 + overlay 浮动窗口替代 CEF Web UI。

**Architecture:** PluginConfig.UIMode 控制分支 — WebUI 模式走现有 Browsingway+WebSocket 路径，ImGui 模式跳过 CEF/WebUiServer，通过 ImGuiOverlayState 静态字典传递状态，ImGui overlay 窗口直接从字典读取。所有交互直接调用 C# handler。

**Tech Stack:** .NET 10, Dalamud SDK 15.0.0, OmenTools, ImGuiNet (Dalamud ImGui)

---

### Task 1: PluginConfig — 添加 UIMode 和 Overlay 位置属性

**Files:**
- Modify: `HiAuRo/Infrastructure/PluginConfig.cs`

- [ ] **Step 1: 在 PluginConfig 中添加 UIMode 枚举和属性**

将以下内容添加到 `PluginConfig` 类的 `AttackRange` 属性之后（约第 28 行后）：

```csharp
/// <summary>UI 渲染模式</summary>
public UIMode UIMode { get; set; } = UIMode.WebUI;

/// <summary>ImGui 模式 — StatusBar overlay X 位置</summary>
public float OverlayStatusBarX { get; set; } = 100f;

/// <summary>ImGui 模式 — StatusBar overlay Y 位置</summary>
public float OverlayStatusBarY { get; set; } = 100f;

/// <summary>ImGui 模式 — StatusBar 展开状态</summary>
public bool OverlayStatusBarExpanded { get; set; } = true;

/// <summary>ImGui 模式 — ActionPanel overlay X 位置</summary>
public float OverlayActionPanelX { get; set; } = 100f;

/// <summary>ImGui 模式 — ActionPanel overlay Y 位置</summary>
public float OverlayActionPanelY { get; set; } = 300f;
```

在 namespace 内、PluginConfig 类之前添加枚举：

```csharp
/// <summary>UI 渲染模式</summary>
public enum UIMode { WebUI = 0, ImGui = 1 }
```

- [ ] **Step 2: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded.

- [ ] **Step 3: 提交**

```bash
git add HiAuRo/Infrastructure/PluginConfig.cs
git commit -m "feat: add UIMode enum and ImGui overlay position properties to PluginConfig"
```

---

### Task 2: Theme.cs — Ant Design 设计令牌

**Files:**
- Create: `HiAuRo/UI/ImGui/Theme.cs`

- [ ] **Step 1: 创建 Theme.cs**

```csharp
using System.Numerics;

namespace HiAuRo.UI.ImGui;

/// <summary>
/// Ant Design 5.0 设计令牌 — 色彩/间距/圆角/字体
/// </summary>
public static class Theme
{
    private static Vector4 Hex(uint hex) => new(
        ((hex >> 16) & 0xFF) / 255f,
        ((hex >> 8)  & 0xFF) / 255f,
        (hex         & 0xFF) / 255f,
        ((hex >> 24) & 0xFF) / 255f
    );

    public static class Colors
    {
        public static readonly Vector4 BgLayout    = Hex(0xFF141414);
        public static readonly Vector4 BgContainer = Hex(0xFF1C1C1E);
        public static readonly Vector4 BgElevated  = Hex(0xFF2A2A2E);
        public static readonly Vector4 BgHover     = Hex(0xFF333336);

        public static readonly Vector4 TextPrimary   = Hex(0xFFE8E8E8);
        public static readonly Vector4 TextSecondary = Hex(0xFFA0A0A0);
        public static readonly Vector4 TextTertiary  = Hex(0xFF808080);

        public static readonly Vector4 AccentBlue   = Hex(0xFF1677FF);
        public static readonly Vector4 AccentGreen  = Hex(0xFF30D158);
        public static readonly Vector4 AccentRed    = Hex(0xFFFF453A);
        public static readonly Vector4 AccentOrange = Hex(0xFFFF9F0A);

        public static readonly Vector4 Border       = Hex(0xFF333333);
        public static readonly Vector4 BorderActive = Hex(0xFF1677FF);
    }

    public const float RadiusXS = 4f;
    public const float RadiusSM = 6f;
    public const float RadiusMD = 8f;
    public const float RadiusLG = 12f;

    public static readonly Vector2 PaddingXS  = new(4, 2);
    public static readonly Vector2 PaddingSM  = new(8, 4);
    public static readonly Vector2 PaddingMD  = new(12, 8);
    public static readonly Vector2 ItemSpacing = new(8, 6);

    public const float FontSizeSM = 11f;
    public const float FontSizeMD = 13f;
    public const float FontSizeLG = 16f;

    public const float AnimSpeed = 12f; // lerp 速度
}
```

- [ ] **Step 2: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded.

- [ ] **Step 3: 提交**

```bash
git add HiAuRo/UI/ImGui/Theme.cs
git commit -m "feat: add Ant Design theme tokens"
```

---

### Task 3: AnimationHelper.cs — 动画工具

**Files:**
- Create: `HiAuRo/UI/ImGui/AnimationHelper.cs`

- [ ] **Step 1: 创建 AnimationHelper.cs**

```csharp
using System.Numerics;

namespace HiAuRo.UI.ImGui;

/// <summary>
/// 通用 Lerp / Easing 动画工具
/// </summary>
public static class AnimationHelper
{
    /// <summary>线性插值（float）</summary>
    public static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0, 1);

    /// <summary>线性插值（Vector2）</summary>
    public static Vector2 Lerp(Vector2 a, Vector2 b, float t) =>
        new(Lerp(a.X, b.X, t), Lerp(a.Y, b.Y, t));

    /// <summary>线性插值（Vector4）</summary>
    public static Vector4 Lerp(Vector4 a, Vector4 b, float t) =>
        new(Lerp(a.X, b.X, t), Lerp(a.Y, b.Y, t), Lerp(a.Z, b.Z, t), Lerp(a.W, b.W, t));

    /// <summary>平滑跟随 — 每帧调用，current 逐步逼近 target</summary>
    public static float SmoothLerp(ref float current, float target, float speed)
    {
        var dt = ImGui.GetIO().DeltaTime;
        current = Lerp(current, target, 1f - MathF.Exp(-speed * dt));
        return current;
    }

    /// <summary>平滑跟随 — Vector4</summary>
    public static Vector4 SmoothLerp(ref Vector4 current, Vector4 target, float speed)
    {
        var dt = ImGui.GetIO().DeltaTime;
        current = Lerp(current, target, 1f - MathF.Exp(-speed * dt));
        return current;
    }
}
```

- [ ] **Step 2: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded.

- [ ] **Step 3: 提交**

```bash
git add HiAuRo/UI/ImGui/AnimationHelper.cs
git commit -m "feat: add AnimationHelper with lerp/easing"
```

---

### Task 4: ComponentLibrary.cs — ImGui 通用组件库

**Files:**
- Create: `HiAuRo/UI/ImGui/ComponentLibrary.cs`

- [ ] **Step 1: 创建 ComponentLibrary.cs**

```csharp
using System.Numerics;

namespace HiAuRo.UI.ImGui;

/// <summary>
/// ImGui 通用组件库 — 参照 Ant Design 风格
/// 每个组件为 static 方法，返回是否发生交互
/// </summary>
public static class ComponentLibrary
{
    /// <summary>按钮 — 主题色圆角按钮</summary>
    public static bool Button(string label, Vector2? size = null, bool disabled = false)
    {
        if (disabled) ImGui.BeginDisabled();
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Theme.RadiusSM);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Theme.PaddingSM);
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.Colors.AccentBlue);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(
            Theme.Colors.AccentBlue.X * 1.15f,
            Theme.Colors.AccentBlue.Y * 1.15f,
            Theme.Colors.AccentBlue.Z * 1.15f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.Colors.AccentBlue);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Colors.TextPrimary);

        var clicked = ImGui.Button(label, size ?? Vector2.Zero);

        ImGui.PopStyleColor(4);
        ImGui.PopStyleVar(2);
        if (disabled) ImGui.EndDisabled();
        return clicked;
    }

    /// <summary>开关 — 带颜色的 checkbox</summary>
    public static bool Switch(string id, ref bool value)
    {
        ImGui.PushStyleColor(ImGuiCol.FrameBg, value ? Theme.Colors.AccentGreen : Theme.Colors.BgElevated);
        ImGui.PushStyleColor(ImGuiCol.CheckMark, Theme.Colors.AccentGreen);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 12f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2, 2));

        var changed = ImGui.Checkbox($"##{id}", ref value);

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(2);
        return changed;
    }

    /// <summary>开关 + 标签（一行）</summary>
    public static bool Switch(string id, string label, ref bool value)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Theme.Colors.TextPrimary, label);
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - 44);
        return Switch(id, ref value);
    }

    /// <summary>滑块 — 主题色滑块</summary>
    public static bool Slider(string id, string label, ref float value, float min, float max, string format = "%.1f")
    {
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.Colors.BgElevated);
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, Theme.Colors.AccentBlue);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, Theme.Colors.AccentBlue);

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 60);
        var changed = ImGui.SliderFloat($"##{id}", ref value, min, max, format);

        ImGui.PopStyleColor(3);

        ImGui.SameLine();
        ImGui.TextColored(Theme.Colors.TextSecondary, label);
        return changed;
    }

    /// <summary>整数滑块</summary>
    public static bool SliderInt(string id, string label, ref int value, int min, int max)
    {
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.Colors.BgElevated);
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, Theme.Colors.AccentBlue);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, Theme.Colors.AccentBlue);

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 60);
        var changed = ImGui.SliderInt($"##{id}", ref value, min, max);

        ImGui.PopStyleColor(3);

        ImGui.SameLine();
        ImGui.TextColored(Theme.Colors.TextSecondary, label);
        return changed;
    }

    /// <summary>下拉选择器</summary>
    public static bool Select(string id, string label, ref int selectedIndex, string[] options)
    {
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.Colors.BgElevated);
        ImGui.PushStyleColor(ImGuiCol.Header, Theme.Colors.AccentBlue);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Theme.Colors.BgHover);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Theme.RadiusSM);

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 80);
        var changed = ImGui.Combo($"##{id}", ref selectedIndex, options, options.Length);

        ImGui.PopStyleVar();
        ImGui.PopStyleColor(3);

        ImGui.SameLine();
        ImGui.TextColored(Theme.Colors.TextSecondary, label);
        return changed;
    }

    /// <summary>标签页</summary>
    public static bool Tabs(ref int activeTab, string[] tabNames)
    {
        var changed = false;
        if (ImGui.BeginTabBar("##tabs", ImGuiTabBarFlags.FittingPolicyScroll))
        {
            for (var i = 0; i < tabNames.Length; i++)
            {
                if (ImGui.BeginTabItem(tabNames[i]))
                {
                    if (i != activeTab) changed = true;
                    activeTab = i;
                    ImGui.EndTabItem();
                }
            }
            ImGui.EndTabBar();
        }
        return changed;
    }

    /// <summary>卡片容器 — Begin/End 配对</summary>
    public static void CardBegin(string? title = null)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, Theme.RadiusMD);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1f);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.Colors.BgContainer);
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.Colors.Border);

        ImGui.BeginChild($"##card_{title ?? "unnamed"}", new Vector2(-1, 0),
            ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.Borders);

        if (title != null)
        {
            ImGui.PushFont(UiBuilder.DefaultFont);
            ImGui.TextColored(Theme.Colors.TextPrimary, title);
            ImGui.PopFont();
            ImGui.Spacing();
        }
    }

    public static void CardEnd()
    {
        ImGui.EndChild();
        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(2);
    }

    /// <summary>标签芯片 — 彩色小标签</summary>
    public static void Tag(string label, bool active, Vector4? activeColor = null, Vector4? inactiveColor = null)
    {
        var color = active
            ? (activeColor ?? Theme.Colors.AccentGreen)
            : (inactiveColor ?? Theme.Colors.BgElevated);

        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Theme.RadiusLG);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 2));
        ImGui.PushStyleColor(ImGuiCol.Button, color);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, color);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, color);
        ImGui.PushStyleColor(ImGuiCol.Text, active ? new Vector4(1, 1, 1, 1) : Theme.Colors.TextSecondary);

        ImGui.Button(label, Vector2.Zero);

        ImGui.PopStyleColor(4);
        ImGui.PopStyleVar(2);
    }

    /// <summary>分割线</summary>
    public static void Divider()
    {
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Separator, Theme.Colors.Border);
        ImGui.Separator();
        ImGui.PopStyleColor();
        ImGui.Spacing();
    }

    /// <summary>Badge — 状态圆点</summary>
    public static void Badge(bool active, Vector4? activeColor = null)
    {
        var color = active ? (activeColor ?? Theme.Colors.AccentGreen) : Theme.Colors.TextTertiary;
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos() + new Vector2(0, 5);
        dl.AddCircleFilled(pos + new Vector2(4, 4), 4, ImGui.ColorConvertFloat4ToU32(color));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 14);
    }

    /// <summary>数字输入</summary>
    public static bool InputNumber(string id, string label, ref int value, int step = 1, int stepFast = 10)
    {
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.Colors.BgElevated);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Theme.RadiusSM);

        ImGui.SetNextItemWidth(80);
        var changed = ImGui.InputInt($"##{id}", ref value, step, stepFast);

        ImGui.PopStyleVar();
        ImGui.PopStyleColor();

        ImGui.SameLine();
        ImGui.TextColored(Theme.Colors.TextPrimary, label);
        return changed;
    }

    /// <summary>纯文本标签</summary>
    public static void Label(string text)
    {
        ImGui.TextColored(Theme.Colors.TextPrimary, text);
    }
}
```

- [ ] **Step 2: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded.

- [ ] **Step 3: 提交**

```bash
git add HiAuRo/UI/ImGui/ComponentLibrary.cs
git commit -m "feat: add ImGui component library (Button/Switch/Slider/Select/Tabs/Card/Tag/Divider/Badge/InputNumber/Label)"
```

---

### Task 5: ImGuiOverlayState.cs — 状态字典

**Files:**
- Create: `HiAuRo/UI/ImGui/ImGuiOverlayState.cs`

- [ ] **Step 1: 创建 ImGuiOverlayState.cs**

```csharp
using HiAuRo.ACR;

namespace HiAuRo.UI.ImGui;

/// <summary>
/// ACRLifecycle ↔ ImGui overlay 窗口的状态通道
/// ACRLifecycle 写入，ImGui 窗口在 Draw() 中读取
/// </summary>
public static class ImGuiOverlayState
{
    public static bool IsRunning;
    public static bool IsPaused;
    public static string AcrName { get; set; } = "无ACR";

    /// <summary>ACR 声明的 UI 控件列表</summary>
    public static List<UiControlDef> Controls { get; set; } = [];

    /// <summary>当前 active tab ID</summary>
    public static string ActiveTab { get; set; } = string.Empty;

    /// <summary>ACR 持久化设置值（checkbox/slider/dropdown/intInput 的值）</summary>
    public static Dictionary<string, object> ControlValues { get; set; } = [];

    /// <summary>QT 芯片列表</summary>
    public static List<ACR.QtData> Qts { get; set; } = [];

    /// <summary>热键 resolver 列表</summary>
    public static List<ACR.IHotkeyResolver> Hotkeys { get; set; } = [];

    /// <summary>UI 设置（布局参数）</summary>
    public static ACR.UiSettings UiSettings { get; set; } = new();

    /// <summary>更新 ACR 状态（ACRLifecycle 调用）</summary>
    public static void UpdateStatus(string acrName, bool isRunning, bool isPaused,
        List<ACR.IHotkeyResolver> hotkeys, List<ACR.QtData> qts)
    {
        AcrName = acrName;
        IsRunning = isRunning;
        IsPaused = isPaused;
        Hotkeys = hotkeys;
        Qts = qts;
    }

    /// <summary>更新控件列表（ACRLifecycle 调用）</summary>
    public static void UpdateControls(List<UiControlDef> controls)
    {
        Controls = controls;
        if (controls.Count > 0)
        {
            ActiveTab = controls.FirstOrDefault(c => c.Type == "tab")?.Id ?? string.Empty;
        }
    }

    /// <summary>更新控件值（ACRLifecycle 调用）</summary>
    public static void UpdateControlValues(Dictionary<string, object> values)
    {
        ControlValues = values;
    }

    /// <summary>获取控件当前值</summary>
    public static T GetValue<T>(string id, T defaultValue)
    {
        if (ControlValues.TryGetValue(id, out var val) && val is T tv)
            return tv;
        return defaultValue;
    }

    /// <summary>设置控件值</summary>
    public static void SetValue(string id, object value)
    {
        ControlValues[id] = value;
    }
}
```

- [ ] **Step 2: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded.

- [ ] **Step 3: 提交**

```bash
git add HiAuRo/UI/ImGui/ImGuiOverlayState.cs
git commit -m "feat: add ImGuiOverlayState for ACRLifecycle ↔ ImGui communication"
```

---

### Task 6: ImGuiWidgetRenderer.cs — UiControlDef → ImGui 映射

**Files:**
- Create: `HiAuRo/UI/ImGui/ImGuiWidgetRenderer.cs`

- [ ] **Step 1: 创建 ImGuiWidgetRenderer.cs**

```csharp
using HiAuRo.Setting;

namespace HiAuRo.UI.ImGui;

/// <summary>
/// UiControlDef → ImGui 组件映射渲染器
/// 遍历控件列表，按 Tab → Group → Items 结构渲染
/// </summary>
public static class ImGuiWidgetRenderer
{
    /// <summary>渲染指定 Tab 下的所有控件</summary>
    public static void Render(List<UiControlDef> controls, string activeTab)
    {
        if (controls.Count == 0) return;

        // 渲染 mainControl
        var mainCtrl = controls.FirstOrDefault(c => c.Type == "mainControl");
        if (mainCtrl != null) RenderMainControl(mainCtrl);

        // 渲染该 Tab 下的 Groups
        var groups = controls.Where(c => c.Type == "group" && c.ParentId == activeTab).ToList();
        if (groups.Count == 0)
        {
            // 无 Group 时直接渲染 Items
            RenderItems(controls.Where(c => c.ParentId == null && c.Type is not ("tab" or "mainControl")));
            return;
        }

        foreach (var group in groups)
        {
            ComponentLibrary.CardBegin(group.Label);
            var items = controls.Where(c => c.ParentId == group.Id);
            RenderItems(items);
            ComponentLibrary.CardEnd();
            ImGui.Spacing();
        }
    }

    private static void RenderItems(IEnumerable<UiControlDef> items)
    {
        foreach (var item in items)
        {
            switch (item.Type)
            {
                case "checkbox":
                    RenderCheckbox(item);
                    break;
                case "slider":
                    RenderSlider(item);
                    break;
                case "dropdown":
                    RenderDropdown(item);
                    break;
                case "intInput":
                    RenderIntInput(item);
                    break;
                case "label":
                    ComponentLibrary.Label(item.Label);
                    break;
                case "separator":
                    ComponentLibrary.Divider();
                    break;
                case "sameLine":
                    ImGui.SameLine();
                    break;
            }
        }
    }

    private static void RenderMainControl(UiControlDef ctrl)
    {
        var meta = ctrl.Meta as System.Text.Json.JsonElement?;
        var showPause = true;
        var showSave = true;
        if (meta.HasValue)
        {
            showPause = meta.Value.TryGetProperty("showPause", out var p) ? p.GetBoolean() : true;
            showSave = meta.Value.TryGetProperty("showSave", out var s) ? s.GetBoolean() : true;
        }

        ComponentLibrary.Badge(ImGuiOverlayState.IsRunning, Theme.Colors.AccentGreen);
        ImGui.SameLine();
        ComponentLibrary.Label(ImGuiOverlayState.AcrName);

        ImGui.SameLine();
        if (ComponentLibrary.Button(ImGuiOverlayState.IsRunning ? "停止" : "启动"))
        {
            if (Runtime.RuntimeCore.IsRunning) Runtime.RuntimeCore.Stop();
            else Runtime.RuntimeCore.Start();
        }

        if (showPause && ImGuiOverlayState.IsRunning)
        {
            ImGui.SameLine();
            if (ComponentLibrary.Button(ImGuiOverlayState.IsPaused ? "继续" : "暂停"))
                HiAuRo.ACR.MainControlHelper.TogglePause();
        }

        if (showSave)
        {
            ImGui.SameLine();
            if (ComponentLibrary.Button("保存"))
                HiAuRo.ACR.MainControlHelper.Save();
        }
    }

    private static void RenderCheckbox(UiControlDef ctrl)
    {
        var val = ImGuiOverlayState.GetValue(ctrl.Id, ctrl.Value is bool b && b);
        if (ComponentLibrary.Switch(ctrl.Id, ctrl.Label, ref val))
        {
            ImGuiOverlayState.SetValue(ctrl.Id, val);
            SaveSettings();
        }
    }

    private static void RenderSlider(UiControlDef ctrl)
    {
        var val = ImGuiOverlayState.GetValue(ctrl.Id, ctrl.Value is float f ? f : 0f);
        float min = 0, max = 100;
        if (ctrl.Options is System.Text.Json.JsonElement opts)
        {
            min = opts.TryGetProperty("min", out var mn) ? mn.GetSingle() : 0;
            max = opts.TryGetProperty("max", out var mx) ? mx.GetSingle() : 100;
        }
        if (ComponentLibrary.Slider(ctrl.Id, ctrl.Label, ref val, min, max))
        {
            ImGuiOverlayState.SetValue(ctrl.Id, val);
            SaveSettings();
        }
    }

    private static void RenderDropdown(UiControlDef ctrl)
    {
        var options = Array.Empty<string>();
        if (ctrl.Options is System.Text.Json.JsonElement opts)
        {
            options = opts.EnumerateArray().Select(e => e.GetString() ?? "").ToArray();
        }
        var selectedIdx = ImGuiOverlayState.GetValue(ctrl.Id, 0);
        if (options.Length > 0 && selectedIdx >= options.Length) selectedIdx = 0;
        if (ComponentLibrary.Select(ctrl.Id, ctrl.Label, ref selectedIdx, options))
        {
            ImGuiOverlayState.SetValue(ctrl.Id, selectedIdx);
            SaveSettings();
        }
    }

    private static void RenderIntInput(UiControlDef ctrl)
    {
        var val = ImGuiOverlayState.GetValue(ctrl.Id, ctrl.Value is int i ? i : 0);
        var step = 1;
        var stepFast = 10;
        if (ctrl.Meta is System.Text.Json.JsonElement meta)
        {
            step = meta.TryGetProperty("step", out var s) ? s.GetInt32() : 1;
            stepFast = meta.TryGetProperty("stepFast", out var sf) ? sf.GetInt32() : 10;
        }
        if (ComponentLibrary.InputNumber(ctrl.Id, ctrl.Label, ref val, step, stepFast))
        {
            ImGuiOverlayState.SetValue(ctrl.Id, val);
            SaveSettings();
        }
    }

    private static void SaveSettings()
    {
        var author = Runtime.ACRLifecycle.CurrentAuthor;
        var jobId = Runtime.ACRLifecycle.CurrentJobId;
        if (string.IsNullOrEmpty(author) || jobId == 0) return;
        SettingMgr.SaveAcrUiSettings(author, jobId, ImGuiOverlayState.UiSettings);
    }
}
```

- [ ] **Step 2: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded.

- [ ] **Step 3: 提交**

```bash
git add HiAuRo/UI/ImGui/ImGuiWidgetRenderer.cs
git commit -m "feat: add ImGuiWidgetRenderer for UiControlDef → ImGui mapping"
```

---

### Task 7: OverlayBase.cs — 无边框 Overlay 窗口基类

**Files:**
- Create: `HiAuRo/UI/ImGui/OverlayBase.cs`

- [ ] **Step 1: 创建 OverlayBase.cs**

```csharp
using System.Numerics;
using Dalamud.Interface.Windowing;
using HiAuRo.Infrastructure;

namespace HiAuRo.UI.ImGui;

/// <summary>
/// 无边框 Overlay 窗口基类 — 拖动 + 位置持久化
/// </summary>
public abstract class OverlayBase : Window
{
    protected readonly PluginConfig _config;
    private bool _isDragging;
    private Vector2 _dragOffset;

    protected OverlayBase(string name, PluginConfig config) : base(name)
    {
        _config = config;
        Flags = ImGuiWindowFlags.NoTitleBar
              | ImGuiWindowFlags.NoResize
              | ImGuiWindowFlags.NoScrollbar
              | ImGuiWindowFlags.NoFocusOnAppearing;
        IsOpen = true;
        RespectCloseHotkey = false;
        ShowCloseButton = false;
    }

    /// <summary>子类重写此方法提供自定义渲染</summary>
    protected abstract void DrawContent();

    public override void Draw()
    {
        // 无标题栏时的拖动检测
        HandleDrag();

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Theme.PaddingMD);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, Theme.RadiusMD);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Theme.Colors.BgLayout);
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.Colors.Border);

        DrawContent();

        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(2);
    }

    private void HandleDrag()
    {
        var mousePos = ImGui.GetMousePos();
        var windowPos = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
            mousePos.X >= windowPos.X && mousePos.X <= windowPos.X + windowSize.X &&
            mousePos.Y >= windowPos.Y && mousePos.Y <= windowPos.Y + windowSize.Y)
        {
            _isDragging = false;
        }

        if (!_isDragging && ImGui.IsMouseDragging(ImGuiMouseButton.Left) &&
            mousePos.X >= windowPos.X && mousePos.X <= windowPos.X + windowSize.X &&
            mousePos.Y >= windowPos.Y && mousePos.Y <= windowPos.Y + windowSize.Y)
        {
            _isDragging = true;
            _dragOffset = mousePos - windowPos;
        }

        if (_isDragging)
        {
            if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            {
                ImGui.SetWindowPos(mousePos - _dragOffset);
            }
            else
            {
                _isDragging = false;
                SavePosition(ImGui.GetWindowPos());
            }
        }
    }

    /// <summary>子类实现：保存当前位置到 config</summary>
    protected abstract void SavePosition(Vector2 pos);
}
```

- [ ] **Step 2: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded.

- [ ] **Step 3: 提交**

```bash
git add HiAuRo/UI/ImGui/OverlayBase.cs
git commit -m "feat: add OverlayBase with borderless drag support"
```

---

### Task 8: OverlayStatusBar.cs — 状态栏 + ACR 控制面板

**Files:**
- Create: `HiAuRo/UI/ImGui/OverlayStatusBar.cs`

- [ ] **Step 1: 创建 OverlayStatusBar.cs**

```csharp
using System.Numerics;
using HiAuRo.Infrastructure;

namespace HiAuRo.UI.ImGui;

/// <summary>
/// 状态栏 + ACR 控制面板（可折叠）
/// </summary>
public sealed class OverlayStatusBar : OverlayBase
{
    private readonly Action _saveConfig;

    public OverlayStatusBar(PluginConfig config, Action saveConfig) : base("HiAuRoStatusBar##Overlay", config)
    {
        _saveConfig = saveConfig;
        Position = new Vector2(config.OverlayStatusBarX, config.OverlayStatusBarY);
        PositionCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(280, 40),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    protected override void DrawContent()
    {
        var expanded = _config.OverlayStatusBarExpanded;

        if (!expanded)
        {
            DrawCollapsedBar();
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - 20);
            if (ImGui.ArrowButton("##expandSB", ImGuiDir.Down))
            {
                _config.OverlayStatusBarExpanded = true;
                _saveConfig();
            }
            return;
        }

        DrawExpandedHeader();
        ImGui.Spacing();

        var controls = ImGuiOverlayState.Controls;
        if (controls.Count == 0)
        {
            ImGui.TextColored(Theme.Colors.TextSecondary, "等待 ACR 加载...");
            return;
        }

        var tabs = controls.Where(c => c.Type == "tab").ToList();
        if (tabs.Count > 0)
        {
            var tabNames = tabs.Select(t => t.Label).ToArray();
            var activeIdx = tabs.FindIndex(t => t.Id == ImGuiOverlayState.ActiveTab);
            if (activeIdx < 0) activeIdx = 0;
            if (ComponentLibrary.Tabs(ref activeIdx, tabNames))
            {
                ImGuiOverlayState.ActiveTab = tabs[activeIdx].Id;
            }
        }

        ImGui.Spacing();
        ImGuiWidgetRenderer.Render(controls, ImGuiOverlayState.ActiveTab);
    }

    private void DrawCollapsedBar()
    {
        ComponentLibrary.Badge(ImGuiOverlayState.IsRunning,
            ImGuiOverlayState.IsPaused ? Theme.Colors.AccentOrange : Theme.Colors.AccentGreen);
        ImGui.SameLine();
        var state = ImGuiOverlayState.IsRunning
            ? (ImGuiOverlayState.IsPaused ? "已暂停" : "运行中")
            : "已停止";
        ImGui.TextColored(Theme.Colors.TextPrimary, $"{state}  {ImGuiOverlayState.AcrName}");
    }

    private void DrawExpandedHeader()
    {
        ComponentLibrary.Badge(ImGuiOverlayState.IsRunning,
            ImGuiOverlayState.IsPaused ? Theme.Colors.AccentOrange : Theme.Colors.AccentGreen);
        ImGui.SameLine();
        var state = ImGuiOverlayState.IsRunning
            ? (ImGuiOverlayState.IsPaused ? "已暂停" : "运行中")
            : "已停止";
        ImGui.TextColored(Theme.Colors.TextPrimary, $"{state}  {ImGuiOverlayState.AcrName}");

        ImGui.SameLine();
        var avail = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, avail - 20));
        if (ImGui.ArrowButton("##collapseSB", ImGuiDir.Up))
        {
            _config.OverlayStatusBarExpanded = false;
            _saveConfig();
        }
    }

    protected override void SavePosition(Vector2 pos)
    {
        _config.OverlayStatusBarX = pos.X;
        _config.OverlayStatusBarY = pos.Y;
        _saveConfig();
    }
}
```

- [ ] **Step 2: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded.

- [ ] **Step 3: 提交**

```bash
git add HiAuRo/UI/ImGui/OverlayStatusBar.cs
git commit -m "feat: add OverlayStatusBar with collapsible ACR control panel"
```

---

### Task 9: OverlayActionPanel.cs — QT + 热键面板

**Files:**
- Create: `HiAuRo/UI/ImGui/OverlayActionPanel.cs`

- [ ] **Step 1: 创建 OverlayActionPanel.cs**

```csharp
using System.Numerics;
using HiAuRo.Infrastructure;

namespace HiAuRo.UI.ImGui;

/// <summary>
/// QT 芯片 + 热键网格面板
/// </summary>
public sealed class OverlayActionPanel : OverlayBase
{
    private readonly Action _saveConfig;

    public OverlayActionPanel(PluginConfig config, Action saveConfig) : base("HiAuRoActionPanel##Overlay", config)
    {
        _saveConfig = saveConfig;
        Position = new Vector2(config.OverlayActionPanelX, config.OverlayActionPanelY);
        PositionCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(200, 40),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    protected override void DrawContent()
    {
        var qts = ImGuiOverlayState.Qts;
        var hotkeys = ImGuiOverlayState.Hotkeys;

        // QT 芯片行
        if (qts.Count > 0)
        {
            foreach (var qt in qts)
            {
                var visible = ImGuiOverlayState.UiSettings.QtVisible.GetValueOrDefault(qt.Id, true);
                if (visible)
                {
                    ImGui.PushID(qt.Id);
                    TagWithClick(qt.Label, qt.Value, qt.Color);
                    if (ImGui.IsItemClicked())
                    {
                        HiAuRo.ACR.QTHelper.Toggle(qt.Id);
                    }
                    if (!string.IsNullOrEmpty(qt.Tooltip) && ImGui.IsItemHovered())
                        ImGui.SetTooltip(qt.Tooltip);
                    ImGui.PopID();
                    ImGui.SameLine();
                }
            }
            ImGui.NewLine();
            ImGui.Spacing();
        }

        // 热键网格
        if (hotkeys.Count > 0)
        {
            var cols = ImGuiOverlayState.UiSettings.HkCols;
            if (cols <= 0) cols = 5;

            for (var i = 0; i < hotkeys.Count; i++)
            {
                var hk = hotkeys[i];
                var visible = ImGuiOverlayState.UiSettings.HkVisible.GetValueOrDefault(hk.Id, true);
                if (!visible) continue;

                ImGui.PushID(hk.Id);
                var available = hk.Check() >= 0;
                ImGui.BeginDisabled(!available);
                var binding = HiAuRo.ACR.HotkeyHelper.GetBinding(hk.Id) ?? hk.DefaultKey;
                var btnSize = ImGuiOverlayState.UiSettings.HkBtnSize > 0
                    ? ImGuiOverlayState.UiSettings.HkBtnSize : 50;
                if (ImGui.Button($"{hk.Label}\n{binding}", new Vector2(btnSize)))
                {
                    if (Runtime.RuntimeCore.IsRunning)
                        HiAuRo.ACR.HotkeyHelper.ExecuteById(hk.Id);
                }
                ImGui.EndDisabled();

                if (!string.IsNullOrEmpty(hk.Label) && ImGui.IsItemHovered())
                    ImGui.SetTooltip(hk.Label);
                ImGui.PopID();

                if ((i + 1) % cols != 0)
                    ImGui.SameLine();
            }
        }
    }

    private static void TagWithClick(string label, bool active, string? colorHex)
    {
        var activeColor = Theme.Colors.AccentGreen;
        if (!string.IsNullOrEmpty(colorHex))
        {
            // 简单 hex 解析
            try { activeColor = ParseHexColor(colorHex); }
            catch { }
        }
        ComponentLibrary.Tag(label, active, activeColor);
    }

    private static Vector4 ParseHexColor(string hex)
    {
        if (hex.StartsWith('#')) hex = hex[1..];
        var r = Convert.ToInt32(hex[..2], 16);
        var g = Convert.ToInt32(hex[2..4], 16);
        var b = Convert.ToInt32(hex[4..6], 16);
        return new Vector4(r / 255f, g / 255f, b / 255f, 1f);
    }

    protected override void SavePosition(Vector2 pos)
    {
        _config.OverlayActionPanelX = pos.X;
        _config.OverlayActionPanelY = pos.Y;
        _saveConfig();
    }
}
```

- [ ] **Step 2: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded.

- [ ] **Step 3: 提交**

```bash
git add HiAuRo/UI/ImGui/OverlayActionPanel.cs
git commit -m "feat: add OverlayActionPanel with QT chips and hotkey grid"
```

---

### Task 10: DemoWindow.cs — 组件展示窗口

**Files:**
- Create: `HiAuRo/UI/ImGui/DemoWindow.cs`

- [ ] **Step 1: 创建 DemoWindow.cs**

```csharp
using System.Numerics;
using Dalamud.Interface.Windowing;

namespace HiAuRo.UI.ImGui;

/// <summary>
/// ImGui 组件展示窗口 — /hi gallery 打开
/// </summary>
public sealed class DemoWindow : Window
{
    private int _activeTab;
    private readonly string[] _tabs = ["按钮", "开关", "滑块", "选择器", "标签页", "标签", "其他"];

    // 演示状态
    private bool _demoSwitch = true;
    private bool _demoSwitch2;
    private float _demoSlider = 50f;
    private int _demoSliderInt = 3;
    private int _demoSelect;
    private readonly string[] _demoOptions = ["选项A", "选项B", "选项C", "选项D"];
    private int _demoInput = 42;
    private int _demoTab;
    private readonly string[] _demoTabNames = ["常规", "高级", "关于"];

    public DemoWindow() : base("HiAuRo 组件展示##Gallery")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        IsOpen = false;
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("##galleryTabs"))
        {
            if (ImGui.BeginTabItem("组件预览"))
            {
                DrawComponentPreview();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("主题色"))
            {
                DrawThemePreview();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void DrawComponentPreview()
    {
        // 按钮
        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "Button");
        ComponentLibrary.Button("主按钮");
        ImGui.SameLine();
        ComponentLibrary.Button("次要按钮");
        ImGui.SameLine();
        ComponentLibrary.Button("禁用按钮", disabled: true);
        ImGui.Spacing();
        ImGui.Spacing();

        // 开关
        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "Switch");
        ComponentLibrary.Switch("sw1", "AoE 技能", ref _demoSwitch);
        ComponentLibrary.Switch("sw2", "爆发药", ref _demoSwitch2);
        ImGui.Spacing();

        // 滑块
        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "Slider");
        ComponentLibrary.Slider("sl1", "攻击距离", ref _demoSlider, 5, 40);
        ComponentLibrary.SliderInt("sl2", "AOE 数量", ref _demoSliderInt, 1, 10);
        ImGui.Spacing();

        // 选择器
        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "Select");
        ComponentLibrary.Select("sel1", "技能顺序", ref _demoSelect, _demoOptions);
        ImGui.Spacing();

        // 标签页
        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "Tabs");
        ComponentLibrary.Tabs(ref _demoTab, _demoTabNames);
        ImGui.Text($"当前标签: {_demoTabNames[_demoTab]}");
        ImGui.Spacing();

        // 卡片
        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "Card");
        ComponentLibrary.CardBegin("基本设置");
        ComponentLibrary.Switch("cardSw", "启用功能", ref _demoSwitch);
        ComponentLibrary.InputNumber("cardNum", "最大数量", ref _demoInput, 1, 10);
        ComponentLibrary.CardEnd();
        ImGui.Spacing();

        // 标签
        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "Tag");
        ComponentLibrary.Tag("AoE", true, Theme.Colors.AccentGreen);
        ImGui.SameLine();
        ComponentLibrary.Tag("爆发", true, Theme.Colors.AccentBlue);
        ImGui.SameLine();
        ComponentLibrary.Tag("疾跑", true, Theme.Colors.AccentOrange);
        ImGui.SameLine();
        ComponentLibrary.Tag("吃药", false);
        ImGui.SameLine();
        ComponentLibrary.Tag("防击退", false);
        ImGui.Spacing();
        ImGui.Spacing();

        // Badge + 数字输入
        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "Badge");
        ComponentLibrary.Badge(true, Theme.Colors.AccentGreen);
        ImGui.SameLine(); ImGui.Text("运行中");
        ImGui.SameLine();
        ComponentLibrary.Badge(true, Theme.Colors.AccentOrange);
        ImGui.SameLine(); ImGui.Text("已暂停");
        ImGui.SameLine();
        ComponentLibrary.Badge(false);
        ImGui.SameLine(); ImGui.Text("已停止");
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "InputNumber");
        ComponentLibrary.InputNumber("num1", "数值输入", ref _demoInput);
        ImGui.Spacing();

        // 分割线
        ComponentLibrary.Divider();
        ImGui.Text("上方为 Divider 分割线");
    }

    private static void DrawThemePreview()
    {
        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "背景色");
        DrawColorSwatch("BgLayout", Theme.Colors.BgLayout);
        DrawColorSwatch("BgContainer", Theme.Colors.BgContainer);
        DrawColorSwatch("BgElevated", Theme.Colors.BgElevated);
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "文字色");
        DrawColorSwatch("TextPrimary", Theme.Colors.TextPrimary);
        DrawColorSwatch("TextSecondary", Theme.Colors.TextSecondary);
        DrawColorSwatch("TextTertiary", Theme.Colors.TextTertiary);
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "品牌色");
        DrawColorSwatch("AccentBlue", Theme.Colors.AccentBlue);
        DrawColorSwatch("AccentGreen", Theme.Colors.AccentGreen);
        DrawColorSwatch("AccentRed", Theme.Colors.AccentRed);
        DrawColorSwatch("AccentOrange", Theme.Colors.AccentOrange);
    }

    private static void DrawColorSwatch(string name, Vector4 color)
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        dl.AddRectFilled(pos, pos + new Vector2(20, 20), ImGui.ColorConvertFloat4ToU32(color));
        dl.AddRect(pos, pos + new Vector2(20, 20), ImGui.ColorConvertFloat4ToU32(Theme.Colors.Border));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 28);
        ImGui.Text(name);
    }
}
```

- [ ] **Step 2: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded.

- [ ] **Step 3: 提交**

```bash
git add HiAuRo/UI/ImGui/DemoWindow.cs
git commit -m "feat: add DemoWindow component gallery (/hi gallery)"
```

---

### Task 11: Plugin.cs — 分支初始化

**Files:**
- Modify: `HiAuRo/Plugin.cs`

- [ ] **Step 1: 添加 using 和 overlay 字段**

在文件顶部的 using 区域添加：
```csharp
using HiAuRo.UI.ImGui;
```

在 `Plugin` 类的字段区域（约第 23-25 行）添加：
```csharp
private OverlayStatusBar? _overlayStatusBar;
private OverlayActionPanel? _overlayActionPanel;
private DemoWindow? _demoWindow;
```

- [ ] **Step 2: 修改构造函数 — WebUI/CEF 初始化变为条件执行**

将第 38 行的 `BrowsingwayPluginInit(pluginInterface);` 替换为：
```csharp
if (_config.UIMode == Infrastructure.UIMode.WebUI)
    BrowsingwayPluginInit(pluginInterface);
```

将第 42-54 行（web 文件复制和 SettingMgr）保持不变。

将第 68-72 行（WebUiBridge + WebUiServer 初始化）用条件包裹：
```csharp
if (_config.UIMode == Infrastructure.UIMode.WebUI)
{
    _uiBridge = new WebUiBridge();
    RegisterUiHandlers(_uiBridge);
    AuthoringServer.Instance.Register(_uiBridge);
    _uiServer = new WebUiServer(webRoot, _uiBridge);
    _uiServer.Start();
}
```

注意 `_uiBridge` 和 `_uiServer` 的声明需要改为 nullable。修改字段声明（约第 21、23 行）：
```csharp
internal readonly WebUiBridge? _uiBridge;
private readonly WebUiServer? _uiServer;
```

- [ ] **Step 3: 修改构造函数 — 添加 ImGui overlay 注册**

在 WindowSystem 注册之后（约第 80 行之后）添加：
```csharp
if (_config.UIMode == Infrastructure.UIMode.ImGui)
{
    _demoWindow = new DemoWindow();
    _overlayStatusBar = new OverlayStatusBar(_config, () => _pluginInterface.SavePluginConfig(_config));
    _overlayActionPanel = new OverlayActionPanel(_config, () => _pluginInterface.SavePluginConfig(_config));
    _windowSystem.AddWindow(_demoWindow);
    _windowSystem.AddWindow(_overlayStatusBar);
    _windowSystem.AddWindow(_overlayActionPanel);
}
```

- [ ] **Step 4: 修改 Dispose — 条件释放 WebUI**

将第 223-224 行的 WebUI 释放用 null 检查包裹（已经是安全的，但确保 `_uiServer` 可为 null）：
```csharp
_uiServer?.Stop();
_uiBridge?.Dispose();
```

- [ ] **Step 5: 修改打印消息**

将第 114 行的启动消息添加模式信息：
```csharp
var modeText = _config.UIMode == Infrastructure.UIMode.WebUI ? "WebUI" : "ImGui";
DService.Instance().Chat.Print($"[HiAuRo] /hi on|off|toggle|status|panel|reload  UI模式: {modeText} 悬浮窗: localhost:5678/jobview.html");
```

将第 116 行的日志也添加模式：
```csharp
DService.Instance().Log.Information($"[Lifecycle] HiAuRo 初始化完成。版本: {_config.LastSeenPluginVersion}  模式: {modeText}");
```

- [ ] **Step 6: 删除旧的第 117 行日志**

将：
```csharp
DService.Instance().Log.Information($"[Lifecycle] 状态: BW={_browserHost != null} WS={_uiServer != null} ACR={ACRLifecycle.CurrentAcrName}");
```
替换为：
```csharp
DService.Instance().Log.Information($"[Lifecycle] 状态: Mode={modeText} ACR={ACRLifecycle.CurrentAcrName}");
```

- [ ] **Step 7: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded.

- [ ] **Step 8: 提交**

```bash
git add HiAuRo/Plugin.cs
git commit -m "feat: add UIMode branching for Plugin init (WebUI vs ImGui)"
```

---

### Task 12: ACRLifecycle.cs — 状态推送分支

**Files:**
- Modify: `HiAuRo/Runtime/ACRLifecycle.cs`

- [ ] **Step 1: 添加 using**

在文件顶部添加：
```csharp
using HiAuRo.UI.ImGui;
```

- [ ] **Step 2: 修改 CheckJobSwitch — 分支推送状态**

将第 105-133 行的 `_ = Plugin.Instance._uiBridge.SendAsync(...)` 调用包裹在条件中：

```csharp
if (Plugin.Instance._uiBridge != null)
{
    _ = Plugin.Instance._uiBridge.SendAsync(new
    {
        type = "status",
        data = new
        {
            job = CurrentAcrName,
            enabled = RuntimeCore.IsRunning,
            paused = ACR.MainControlHelper.IsPaused,
            hotkeys = ACR.HotkeyHelper.GetAll().Select(r => new
            {
                id = r.Id,
                label = r.Label,
                iconId = r.IconId,
                iconUrl = HiAuRo.UI.IconServer.GetIconUrl(r.IconId),
                available = r.Check() >= 0,
                binding = ACR.HotkeyHelper.GetBinding(r.Id)
            }).ToList(),
            qts = ACR.QTHelper.GetAll().Select(q => new
            {
                id = q.Id,
                label = q.Label,
                value = q.Value,
                tooltip = q.Tooltip,
                color = q.Color,
                binding = q.HotkeyBinding
            }).ToList()
        }
    });
}
else
{
    ImGuiOverlayState.UpdateStatus(CurrentAcrName, RuntimeCore.IsRunning,
        ACR.MainControlHelper.IsPaused, ACR.HotkeyHelper.GetAll(), ACR.QTHelper.GetAll());
}
```

- [ ] **Step 3: 修改 LoadRotation — 分支推送 controls/uiSettings/status**

在第 214 行的 `_ = Plugin.Instance._uiBridge.SendAsync(new { type = "controls" ... })` 用条件包裹：

```csharp
if (Plugin.Instance._uiBridge != null)
{
    _ = Plugin.Instance._uiBridge.SendAsync(new { type = "controls", data = controls });
    Plugin.Instance._uiBridge.CacheControls(controls);
}
else
{
    ImGuiOverlayState.UpdateControls(controls);
}
```

在第 228-251 行的 uiSettings 推送，同样包裹：
```csharp
if (Plugin.Instance._uiBridge != null)
{
    _ = Plugin.Instance._uiBridge.SendAsync(new
    {
        type = "uiSettings",
        data = new
        {
            qtCols = settings.QtCols,
            qtBtnW = settings.QtBtnW,
            qtVisible = settings.QtVisible,
            hkCols = settings.HkCols,
            hkBtnSize = settings.HkBtnSize,
            hkVisible = settings.HkVisible,
            hkBindings = settings.HkBindings
        }
    });
    Plugin.Instance._uiBridge.CacheUiSettings(new { ... });
}
else
{
    ImGuiOverlayState.UiSettings = settings;
}
```

在第 257-284 行的 status 推送，同样包裹：
```csharp
if (Plugin.Instance._uiBridge != null)
{
    _ = Plugin.Instance._uiBridge.SendAsync(new { type = "status", ... });
}
else
{
    ImGuiOverlayState.UpdateStatus(CurrentAcrName, RuntimeCore.IsRunning,
        ACR.MainControlHelper.IsPaused, hotkeyList, qtList);
}
```

- [ ] **Step 4: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded.

- [ ] **Step 5: 提交**

```bash
git add HiAuRo/Runtime/ACRLifecycle.cs
git commit -m "feat: add ImGui mode state push in ACRLifecycle"
```

---

### Task 13: MainWindow.cs — 模式切换控件

**Files:**
- Modify: `HiAuRo/UI/MainWindow.cs`

- [ ] **Step 1: 添加 using**

在文件顶部添加：
```csharp
using HiAuRo.UI.ImGui;
```

- [ ] **Step 2: 在 DrawStatus 最上方添加模式切换**

在 `DrawStatus()` 方法的最开始（`ImGui.Spacing()` 之前）添加：

```csharp
// UI 模式切换
ImGui.TextColored(Theme.Colors.AccentBlue, "UI 渲染模式:");
ImGui.SameLine();
var isWebUI = _config.UIMode == Infrastructure.UIMode.WebUI;
if (ImGui.RadioButton("WebUI", isWebUI))
{
    _config.UIMode = Infrastructure.UIMode.WebUI;
    _saveConfig();
}
ImGui.SameLine();
if (ImGui.RadioButton("ImGui", !isWebUI))
{
    _config.UIMode = Infrastructure.UIMode.ImGui;
    _saveConfig();
}

if (_config.UIMode == Infrastructure.UIMode.ImGui)
{
    ImGui.PushStyleColor(ImGuiCol.Text, Theme.Colors.AccentOrange);
    ImGui.TextWrapped("⚠ 切换后请重启插件生效 (Disable/Enable)");
    ImGui.PopStyleColor();
}

ImGui.Separator();
ImGui.Spacing();
```

- [ ] **Step 3: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded.

- [ ] **Step 4: 提交**

```bash
git add HiAuRo/UI/MainWindow.cs
git commit -m "feat: add UIMode toggle in MainWindow status tab"
```

---

### Task 14: CommandMgr.cs — /hi gallery 命令

**Files:**
- Modify: `HiAuRo/Command/CommandMgr.cs`

- [ ] **Step 1: 添加 gallery 和 demo 命令**

在 `OnCommand` 的 switch 语句中，`case "assist unload":` 之后添加：

```csharp
case "gallery":
case "demo":
    Plugin.Instance.ShowDemoWindow();
    DService.Instance().Chat.Print("[HiAuRo] 组件展示窗口已打开");
    break;
```

- [ ] **Step 2: 更新 help message**

将 `HelpMessage` 更新为：
```csharp
HelpMessage = "HiAuRo: /hi on|off|toggle|status|panel|reload|fact|assist [load|unload]|gallery|catalog [export|upload]"
```

- [ ] **Step 3: 在 Plugin.cs 添加 ShowDemoWindow 方法**

在 `Plugin.cs` 中添加公开方法：

```csharp
public void ShowDemoWindow()
{
    if (_demoWindow != null)
    {
        _demoWindow.IsOpen = true;
    }
    else
    {
        // WebUI 模式下 DemoWindow 不存在，动态创建
        _demoWindow = new DemoWindow();
        _windowSystem.AddWindow(_demoWindow);
        _demoWindow.IsOpen = true;
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
git add HiAuRo/Command/CommandMgr.cs HiAuRo/Plugin.cs
git commit -m "feat: add /hi gallery command to open DemoWindow"
```

---

### Task 15: 集成验证

- [ ] **Step 1: 完整构建**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded, 0 errors, 0 warnings.

- [ ] **Step 2: 检查所有新文件都存在**

```bash
ls -la HiAuRo/UI/ImGui/
```

Expected: Theme.cs, AnimationHelper.cs, ComponentLibrary.cs, ImGuiOverlayState.cs, ImGuiWidgetRenderer.cs, OverlayBase.cs, OverlayStatusBar.cs, OverlayActionPanel.cs, DemoWindow.cs

- [ ] **Step 3: 验证未删除任何 WebUI 文件**

```bash
ls HiAuRo/UI/web/main.html HiAuRo/UI/WebUiServer.cs HiAuRo/UI/WebUiBridge.cs HiAuRo/Plugin_Browsingway.cs
```

Expected: All four files still exist.

- [ ] **Step 4: 最终提交**

```bash
git add -A
git status
# 确认只有预期的文件变更
```
