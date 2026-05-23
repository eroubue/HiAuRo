# MainWindow UI 重设计实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 用 ComponentLibrary 重构 MainWindow 布局，实现侧边栏卡片式导航 + Tab 内容区 + Plugin 动态卡片

**Architecture:** 纯 ImGui 布局，不引入新依赖。MainWindow.Draw() 拆分为三个区域：顶部信息栏（Logo + Tips）、左侧卡片栏（固定模块 + 动态 Plugin）、右侧内容区（Tab + 内容）。保持现有 9 个 DrawXxx() 方法不变。

**Tech Stack:** .NET 10, Dalamud ImGui, 项目已有 ComponentLibrary + Theme

**涉及文件:**
- 修改: `HiAuRo/UI/MainWindow.cs`
- 修改: `HiAuRo/Plugin/IPlugin.cs`（新增 GetEmbeddedUI 方法）

---

### Task 1: IPlugin 接口新增 GetEmbeddedUI 方法

**Files:**
- Modify: `E:\DalamudPlugins\HiAuRo\HiAuRo\Plugin\IPlugin.cs`

- [ ] **Step 1: 在 IPlugin 接口末尾新增 GetEmbeddedUI 默认实现**

在 `IPluginWindow? GetWindow() => null;` 行之后添加新方法：

```csharp
/// <summary>返回嵌入主窗口的内容绘制 Action，无需嵌入时返回 null</summary>
Action? GetEmbeddedUI() => null;
```

该方法的完整上下文（修改后 IPlugin.cs 全文）：

```csharp
namespace HiAuRo;

/// <summary>
/// 通用插件入口 —— 实现此接口的 DLL 将被 PluginLoader 自动发现并加载
/// 扫描路径: Plugins/*.dll
/// </summary>
public interface IPlugin : IDisposable
{
    string Name { get; }
    string Version { get; }
    void Initialize();
    void Update();
    /// <summary>返回插件窗口，无需窗口时返回 null</summary>
    IPluginWindow? GetWindow() => null;
    /// <summary>返回嵌入主窗口的内容绘制 Action，无需嵌入时返回 null</summary>
    Action? GetEmbeddedUI() => null;
}
```

- [ ] **Step 2: 构建验证编译通过**

```bash
dotnet build HiAuRo/HiAuRo.csproj -nologo
```

预期：编译成功（新增默认方法不破坏现有实现）

- [ ] **Step 3: 提交**

```bash
git add HiAuRo/Plugin/IPlugin.cs
git commit -m "IPlugin 接口新增 GetEmbeddedUI() 方法，支持嵌入主窗口内容区"
```

---

### Task 2: MainWindow 新增布局状态字段和 Tips 数据

**Files:**
- Modify: `E:\DalamudPlugins\HiAuRo\HiAuRo\UI\MainWindow.cs`
  - 在构造函数之后、Draw() 之前插入新字段

- [ ] **Step 1: 在 MainWindow 类中添加状态字段**

定位到构造函数 `public MainWindow(...)` 结束后（约第 28 行 `}`），在 `Draw()` 方法之前插入以下字段。需要新增的 `using`：`using HiAuRo.Runtime;`

```csharp
    // ── 新布局状态字段 ──

    /// <summary>当前选中的主模块卡片索引 (0=主控, 1=调试, 2=时间轴, 3+=Plugin)</summary>
    private int _selectedCardIndex;

    /// <summary>当前模块内的子 Tab 索引</summary>
    private int _selectedTabIndex;

    /// <summary>上次选中卡片索引（用于检测切换时重置 Tab）</summary>
    private int _lastCardIndex = -1;

    /// <summary>Plugin 卡片选中时的 Plugin 记录名称</summary>
    private string? _selectedPluginName;

    /// <summary>Tips 轮播数据</summary>
    private static readonly string[] _tips = new[]
    {
        "提示：右键侧边栏 Plugin 卡片可弹出独立窗口",
        "快捷键 /hi 或点击 Dalamud 图标打开主界面",
        "在「设置」中调整技能队列窗口和 AOE 判定数",
        "时间轴模块支持录制副本并编辑执行/辅助/事实轴",
        "Debug 面板提供 Aura/Combo/Spell/SpellHistory/Target 实时测试工具",
    };

    /// <summary>Tips 轮播定时器（渲染帧累计）</summary>
    private float _tipsTimer;

    /// <summary>当前显示的 Tips 索引</summary>
    private int _tipsIndex;

    /// <summary>Tips 淡入动画进度 0~1</summary>
    private float _tipsFade;

    /// <summary>版本号（构建时通过 ci 更新）</summary>
    private const string _version = "1.0.0";

    // ── 固定模块定义 ──

    /// <summary>固定模块元数据</summary>
    private static readonly (string Name, string Icon, string[] Tabs)[] _modules = new[]
    {
        ("主控", "⚙", new[] { "状态", "设置", "窗口设置" }),
        ("调试", "◉", new[] { "Debug", "ACR Debug", "日志" }),
        ("时间轴", "◎", new[] { "录制", "事实轴", "执行轴", "辅助轴" }),
    };
```

- [ ] **Step 2: 构建验证编译通过**

```bash
dotnet build HiAuRo/HiAuRo.csproj -nologo
```

预期：编译成功，仅新增字段无语法错误

- [ ] **Step 3: 提交**

```bash
git add HiAuRo/UI/MainWindow.cs
git commit -m "MainWindow 新增布局状态字段和 Tips 轮播数据"
```

---

### Task 3: 重写 MainWindow.Draw() — 整体布局骨架

**Files:**
- Modify: `E:\DalamudPlugins\HiAuRo\HiAuRo\UI\MainWindow.cs`
  - 替换现有 `Draw()` 方法（第 31-87 行）

- [ ] **Step 1: 替换 Draw() 方法为新的三区布局**

将现有的 `Draw()` 方法（第 31 行到第 87 行）替换为：

```csharp
    /// <summary>绘制窗口</summary>
    public override void Draw()
    {
        // ── 窗口内边距 ──
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 10));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 6));

        // ── 计算布局区域 ──
        var avail = ImGui.GetContentRegionAvail();
        var topBarHeight = 62f;
        var statusBarHeight = 24f;
        var sidebarWidth = 168f;

        // ── 顶部信息栏 ──
        ImGui.BeginChild("##TopBar", new Vector2(avail.X, topBarHeight), false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        DrawTopBar();
        ImGui.EndChild();

        ImGui.Separator();

        // 中间区域高度
        var midHeight = avail.Y - topBarHeight - statusBarHeight - 18f;

        // ── 左侧栏 + 右侧内容 ──
        if (ImGui.BeginTable("##MainLayout", 2,
            ImGuiTableFlags.Resizable | ImGuiTableFlags.NoSavedSettings,
            new Vector2(avail.X, midHeight)))
        {
            ImGui.TableSetupColumn("##SidebarCol", ImGuiTableColumnFlags.WidthFixed, sidebarWidth);
            ImGui.TableSetupColumn("##ContentCol", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            DrawSidebar(sidebarWidth, midHeight);

            ImGui.TableSetColumnIndex(1);
            DrawContent();

            ImGui.EndTable();
        }

        // ── 底部状态栏 ──
        ImGui.Separator();
        DrawStatusBar(avail.X);

        ImGui.PopStyleVar(2); // WindowPadding, ItemSpacing
    }
```

- [ ] **Step 2: 构建验证，预期编译失败（缺少 DrawTopBar/DrawSidebar/DrawContent/DrawStatusBar 方法）**

```bash
dotnet build HiAuRo/HiAuRo.csproj -nologo
```

- [ ] **Step 3: 提交**

```bash
git add HiAuRo/UI/MainWindow.cs
git commit -m "MainWindow.Draw() 重构为三区布局骨架(顶部/侧栏+内容/底部)"
```

---

### Task 4: 实现 DrawTopBar — Logo + Tips + 主题切换

**Files:**
- Modify: `E:\DalamudPlugins\HiAuRo\HiAuRo\UI\MainWindow.cs`
  - 在 Draw() 方法之后（区域分隔注释前）插入新方法

- [ ] **Step 1: 定位到 Draw() 结束位置（替换后的 Draw），在其后添加 DrawTopBar 方法**

在 `// ── 新布局状态字段 ──` 区域之前或紧接 Draw() 之后插入：

```csharp
    /// <summary>绘制顶部信息栏：Logo + Tips + 主题切换按钮</summary>
    private void DrawTopBar()
    {
        // ── Layout: LOGO 左 | Tips 中 | 控件 右 ──
        var region = ImGui.GetContentRegionAvail();
        var logoWidth = 140f;
        var controlWidth = 36f;

        // ── LOGO 区域 (左) ──
        ImGui.BeginChild("##LogoArea", new Vector2(logoWidth, region.Y), false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        DrawLogo();
        ImGui.EndChild();

        ImGui.SameLine();

        // ── Tips 轮播 (中) ──
        var tipsWidth = region.X - logoWidth - controlWidth - 30f;
        ImGui.BeginChild("##TipsArea", new Vector2(tipsWidth, region.Y), false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        DrawTips(tipsWidth);
        ImGui.EndChild();

        ImGui.SameLine();

        // ── 主题切换按钮 (右) ──
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 8);
        var isDark = Theme.Mode == Theme.ThemeMode.Dark;
        if (ComponentLibrary.IconButton(
            isDark ? ComponentLibrary.IconType.Stop : ComponentLibrary.IconType.Play,
            Theme.Colors.AccentBlue,
            new Vector2(28, 28),
            ComponentLibrary.IconButtonStyle.Text))
        {
            Theme.Mode = isDark ? Theme.ThemeMode.Light : Theme.ThemeMode.Dark;
            _config.ImGuiThemeMode = isDark ? ImGuiThemeMode.Light : ImGuiThemeMode.Dark;
            _saveConfig();
        }
    }

    /// <summary>绘制 ASCII Art Logo</summary>
    private static void DrawLogo()
    {
        ImGui.PushFont(UiBuilder.MonoFont);
        ImGui.SetCursorPosY(4);
        var logoLines = new[]
        {
            "██╗  ██╗██╗ █████╗ ██╗   ██╗██████╗ ",
            "██║  ██║██║██╔══██╗██║   ██║██╔══██╗",
            "███████║██║███████║██║   ██║██████╔╝",
            "██╔══██║██║██╔══██║██║   ██║██╔══██╗",
            "██║  ██║██║██║  ██║╚██████╔╝██║  ██║",
            "╚═╝  ╚═╝╚═╝╚═╝  ╚═╝ ╚═════╝ ╚═╝  ╚═╝",
        };
        foreach (var line in logoLines)
            ImGui.TextColored(Theme.Colors.AccentBlue, line);
        ImGui.PopFont();
    }

    /// <summary>绘制 Tips 轮播文字</summary>
    private void DrawTips(float maxWidth)
    {
        // 更新轮播定时器
        _tipsTimer += ImGui.GetIO().DeltaTime;
        _tipsFade = MathF.Min(1f, _tipsFade + ImGui.GetIO().DeltaTime * 2f);

        if (_tipsTimer > 4f) // 每 4 秒切换
        {
            _tipsTimer = 0f;
            _tipsIndex = (_tipsIndex + 1) % _tips.Length;
            _tipsFade = 0f; // 重置淡入
        }

        var tip = _tips[_tipsIndex];
        var alpha = _tipsFade;
        var tipColor = new Vector4(
            Theme.Colors.TextSecondary.X,
            Theme.Colors.TextSecondary.Y,
            Theme.Colors.TextSecondary.Z,
            Theme.Colors.TextSecondary.W * alpha);

        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 12);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + maxWidth);
        ImGui.TextColored(tipColor, $"💡 {tip}");
        ImGui.PopTextWrapPos();
    }
```

- [ ] **Step 2: 构建验证编译通过**

```bash
dotnet build HiAuRo/HiAuRo.csproj -nologo
```

- [ ] **Step 3: 提交**

```bash
git add HiAuRo/UI/MainWindow.cs
git commit -m "MainWindow 实现 DrawTopBar: Logo + Tips 轮播 + 主题切换按钮"
```

---

### Task 5: 实现 DrawSidebar — 固定模块卡片 + Plugin 动态卡片

**Files:**
- Modify: `E:\DalamudPlugins\HiAuRo\HiAuRo\UI\MainWindow.cs`
  - 在 DrawTopBar/DrawLogo/DrawTips 方法之后插入

- [ ] **Step 1: 添加 DrawSidebar 方法和 SidebarCard 辅助方法**

```csharp
    /// <summary>绘制左侧栏：固定模块卡片 + 分隔线 + Plugin 动态卡片</summary>
    private void DrawSidebar(float width, float height)
    {
        ImGui.BeginChild("##Sidebar", new Vector2(width, height), false,
            ImGuiWindowFlags.NoScrollbar);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 4));

        // ── 固定模块卡片 ──
        for (var i = 0; i < _modules.Length; i++)
        {
            var (name, icon, _) = _modules[i];
            if (SidebarCard(name, icon, i == _selectedCardIndex && _selectedPluginName == null))
            {
                _selectedCardIndex = i;
                _selectedPluginName = null;
            }
        }

        // ── 分隔线（仅当有 Plugin 时显示）─
        var plugins = PluginLoader.Plugins;
        if (plugins.Count > 0)
        {
            ComponentLibrary.Divider();

            // ── Plugin 动态卡片 ──
            foreach (var (pluginName, record) in plugins.OrderBy(kv => kv.Key))
            {
                var isSelected = _selectedPluginName == pluginName;
                var name = record.Plugin.Name;
                var version = record.Plugin.Version;
                if (SidebarCard(name, "◉", isSelected, version))
                {
                    _selectedCardIndex = _modules.Length;
                    _selectedPluginName = pluginName;
                }
            }
        }

        ImGui.PopStyleVar(); // ItemSpacing
        ImGui.EndChild();
    }

    /// <summary>绘制单张侧边栏卡片（极简风格，左侧 3px 竖条 + 图标 + 标题）</summary>
    /// <returns>是否被点击</returns>
    private static bool SidebarCard(string title, string icon, bool isSelected, string? subtitle = null)
    {
        var cardWidth = ImGui.GetContentRegionAvail().X;
        var cardHeight = subtitle != null ? 46f : 38f;
        var cursorStart = ImGui.GetCursorPos();

        // 左侧竖条颜色
        var accentColor = isSelected ? Theme.Colors.SidebarActiveBorder : Theme.Colors.BorderSecondary;
        var bgColor = isSelected ? Theme.Colors.SidebarActive : Vector4.Zero;
        var textColor = isSelected ? Theme.Colors.TextPrimary : Theme.Colors.TextSecondary;

        // ── 背景绘制 ──
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(cardWidth, cardHeight);

        // 圆角背景
        dl.AddRectFilled(min, max,
            ImGui.ColorConvertFloat4ToU32(bgColor),
            Theme.RadiusMD);

        // 左侧竖条
        dl.AddRectFilled(min,
            min + new Vector2(3, cardHeight),
            ImGui.ColorConvertFloat4ToU32(accentColor),
            Theme.RadiusMD, ImDrawFlags.RoundCornersLeft);

        // ── 可点击区域 ──
        ImGui.SetCursorPos(cursorStart);
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.Colors.BgHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.Colors.BgSpotlight);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Theme.RadiusMD);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(12, 4));

        var clicked = ImGui.Button($"##card_{title}", new Vector2(cardWidth, cardHeight));

        // ── 文字叠加 ──
        var textPos = ImGui.GetItemRectMin() + new Vector2(16, 4);
        dl.AddText(textPos, ImGui.ColorConvertFloat4ToU32(textColor), icon);
        dl.AddText(textPos + new Vector2(16, 0), ImGui.ColorConvertFloat4ToU32(textColor), title);

        if (subtitle != null)
        {
            dl.AddText(textPos + new Vector2(0, 18),
                ImGui.ColorConvertFloat4ToU32(Theme.Colors.TextTertiary), subtitle);
        }

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(3);

        // 悬停时在卡片上叠加白色半透明
        if (ImGui.IsItemHovered() && !isSelected)
        {
            dl.AddRectFilled(min, max,
                ImGui.ColorConvertFloat4ToU32(Theme.Colors.BgHover),
                Theme.RadiusMD);
        }

        return clicked;
    }
```

- [ ] **Step 2: 构建验证编译通过**

```bash
dotnet build HiAuRo/HiAuRo.csproj -nologo
```

- [ ] **Step 3: 提交**

```bash
git add HiAuRo/UI/MainWindow.cs
git commit -m "MainWindow 实现 DrawSidebar: 固定模块卡片 + Plugin 动态卡片"
```

---

### Task 6: 实现 DrawContent — Tab 栏 + 内容联动

**Files:**
- Modify: `E:\DalamudPlugins\HiAuRo\HiAuRo\UI\MainWindow.cs`
  - 在 DrawSidebar/SidebarCard 方法之后插入

- [ ] **Step 1: 添加 DrawContent 方法**

```csharp
    /// <summary>绘制右侧内容区：Tab 栏 + 当前页面内容</summary>
    private void DrawContent()
    {
        var isPluginSelected = _selectedPluginName != null;

        // ── 检测卡片切换，重置 Tab 索引 ──
        if (_lastCardIndex != _selectedCardIndex)
        {
            _selectedTabIndex = 0;
            _lastCardIndex = _selectedCardIndex;
        }

        // ── Tab 栏 ──
        if (!isPluginSelected)
        {
            var (_, _, tabs) = _modules[Math.Clamp(_selectedCardIndex, 0, _modules.Length - 1)];
            ComponentLibrary.Tabs($"module_{_selectedCardIndex}", ref _selectedTabIndex, tabs);
            ImGui.Spacing();
        }
        else
        {
            // Plugin 选中时显示插件名称作为标题
            ImGui.TextColored(Theme.Colors.AccentBlue,
                $"插件: {_selectedPluginName}");
            ImGui.Separator();
            ImGui.Spacing();
        }

        // ── 内容区 ──
        var contentHeight = ImGui.GetContentRegionAvail().Y - 4f;
        ImGui.BeginChild("##ContentArea", new Vector2(-1, contentHeight), false);

        if (isPluginSelected)
        {
            DrawPluginContent();
        }
        else
        {
            DrawModuleContent();
        }

        ImGui.EndChild();
    }

    /// <summary>根据当前选中的模块卡片 + Tab 索引绘制内容</summary>
    private void DrawModuleContent()
    {
        var moduleIndex = Math.Clamp(_selectedCardIndex, 0, _modules.Length - 1);
        var (moduleName, _, tabs) = _modules[moduleIndex];
        var tabIndex = Math.Clamp(_selectedTabIndex, 0, tabs.Length - 1);
        var tabName = tabs[tabIndex];

        // 路由到对应的 DrawXxx 方法
        switch (tabName)
        {
            case "状态":       DrawStatus(); break;
            case "设置":       DrawSettings(); break;
            case "窗口设置":   DrawOverlaySettings(); break;
            case "Debug":      DrawDebug(); break;
            case "ACR Debug":  DrawAcrDebug(); break;
            case "日志":       DrawLog(); break;
            case "录制":       DrawRecording(); break;
            case "事实轴":     DrawFactAxisTab(); break;
            case "执行轴":     DrawExecutionAxisTab(); break;
            case "辅助轴":     DrawAssistAxisTab(); break;
        }
    }

    /// <summary>绘制当前选中 Plugin 的嵌入内容</summary>
    private void DrawPluginContent()
    {
        if (_selectedPluginName == null) return;

        var plugins = PluginLoader.Plugins;
        if (!plugins.TryGetValue(_selectedPluginName, out var record)) return;

        var embeddedUI = record.Plugin.GetEmbeddedUI();
        if (embeddedUI != null)
        {
            embeddedUI();
        }
        else
        {
            ImGui.TextColored(Theme.Colors.TextTertiary, "此插件未提供嵌入 UI。");
            ImGui.Spacing();
            ImGui.TextColored(Theme.Colors.TextSecondary, "提示：右键侧边栏卡片可打开独立窗口");
        }
    }
```

- [ ] **Step 2: 构建验证编译通过**

```bash
dotnet build HiAuRo/HiAuRo.csproj -nologo
```

- [ ] **Step 3: 提交**

```bash
git add HiAuRo/UI/MainWindow.cs
git commit -m "MainWindow 实现 DrawContent: Tab 栏联动 + Plugin 嵌入内容"
```

---

### Task 7: 实现 DrawStatusBar — 底部状态栏

**Files:**
- Modify: `E:\DalamudPlugins\HiAuRo\HiAuRo\UI\MainWindow.cs`
  - 在 DrawContent 相关方法之后插入

- [ ] **Step 1: 添加 DrawStatusBar 方法**

```csharp
    /// <summary>绘制底部状态栏</summary>
    private static void DrawStatusBar(float width)
    {
        ImGui.BeginChild("##StatusBar", new Vector2(width, 20f), false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

        var running = RuntimeCore.IsRunning;
        var paused = ACR.MainControlHelper.IsPaused;
        var runState = running ? (paused ? "已暂停" : "运行中") : "已停止";
        var stateColor = running ? (paused ? Theme.Colors.AccentOrange : Theme.Colors.AccentGreen) : Theme.Colors.AccentRed;

        ImGui.TextColored(Theme.Colors.TextTertiary, "HiAuRo");
        ImGui.SameLine();
        ImGui.TextColored(stateColor, $"[{runState}]");

        ImGui.SameLine();
        ImGui.TextColored(Theme.Colors.TextTertiary, $"v{_version}");

        ImGui.SameLine();
        ComponentLibrary.VSplit();

        ImGui.SameLine();
        var acrName = ACRLifecycle.CurrentAcrName ?? "未加载";
        ImGui.TextColored(Theme.Colors.TextSecondary, $"ACR: {acrName}");

        ImGui.SameLine();
        ComponentLibrary.VSplit();

        ImGui.SameLine();
        var pluginCount = PluginLoader.Plugins.Count;
        ImGui.TextColored(Theme.Colors.TextSecondary, $"Plugins: {pluginCount}");

        ImGui.EndChild();
    }
```

需要先确认 `ComponentLibrary` 有没有 `VSplit` 方法。从之前读取的代码知道它有：

```csharp
/// <summary>垂直分隔符</summary>
public static void VSplit()
```

因为不在我读取的那 600 行内，需要在 ComponentLibrary.cs 后面找。从组件列表看它存在。

- [ ] **Step 2: 构建验证编译通过**

```bash
dotnet build HiAuRo/HiAuRo.csproj -nologo
```

- [ ] **Step 3: 提交**

```bash
git add HiAuRo/UI/MainWindow.cs
git commit -m "MainWindow 实现 DrawStatusBar: 底部状态栏(版本/状态/ACR/Plugins)"
```

---

### Task 8: 完整构建验证 + 最终提交

**Files:**
- Modify: 无新修改，验证上述所有修改

- [ ] **Step 1: 完整构建**

```bash
dotnet build HiAuRo/HiAuRo.csproj -nologo
```

- [ ] **Step 2: 验证 MainWindow.cs 文件完整性**

检查以下内容全部存在：
- `_selectedCardIndex`, `_selectedTabIndex`, `_lastCardIndex`, `_selectedPluginName` 字段
- `_tips`, `_tipsTimer`, `_tipsIndex`, `_tipsFade` 字段
- `_modules` 固定模块定义（主控/调试/时间轴，Tab 映射正确）
- `Draw()` — 三区布局
- `DrawTopBar()` — Logo + Tips + 主题按钮
- `DrawLogo()` — ASCII Art
- `DrawTips()` — 轮播逻辑
- `DrawSidebar()` — 固定卡片 + 分隔线 + Plugin 卡片
- `SidebarCard()` — 极简卡片渲染
- `DrawContent()` — Tab 栏 + 内容
- `DrawModuleContent()` — switch 路由到原有 DrawXxx()
- `DrawPluginContent()` — 调用 `IPlugin.GetEmbeddedUI()`
- `DrawStatusBar()` — 底部状态栏
- 原有 9 个 `DrawXxx()` 方法保持完整不变
- 原有 Debug 测试字段保持完整不变

- [ ] **Step 3: 最终提交**

```bash
git add HiAuRo/UI/MainWindow.cs
git add HiAuRo/Plugin/IPlugin.cs
git commit -m "MainWindow UI 重设计完成: 侧边栏卡片导航 + Tab内容联动 + Plugin嵌入 + Tips轮播 + 状态栏"
```

---

### 任务依赖图

```
Task 1 (IPlugin扩展)     Task 2 (状态字段)
     │                        │
     └────────┬───────────────┘
              │
         Task 3 (Draw骨架)
              │
    ┌─────────┼─────────┬─────────┐
    │         │         │         │
  Task 4   Task 5    Task 6   Task 7
(顶部栏) (侧边栏)  (内容区) (状态栏)
    │         │         │         │
    └─────────┴─────────┴─────────┘
              │
         Task 8 (验证+最终提交)
```

Task 1 和 Task 2 可并行；Task 4~7 可并行（均依赖 Task 3）。
