# 悬浮窗模式动态切换 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让 WebUI 和 ImGui 两种悬浮窗模式可动态切换（不需重启），同时提供低配置选项完全禁用 CEF。

**Architecture:** 新增 `UIManager` 集中管理两种 UI 模式的生命周期。CEF 子进程默认常驻（仅 DisableCEF=true 时从不创建）。切换只是在 CEF Overlay 的显示/隐藏和 ImGui 窗口的动态添加/移除之间切换。ACRLifecycle 通过 `Plugin.IsWebUI` 判断状态推送通道。

**Tech Stack:** C#, Dalamud SDK, OmenTools, Browsingway (CEF)

---

### File Map

| 文件 | 类型 | 职责 |
|------|------|------|
| `HiAuRo/UI/UIManager.cs` | 新增 (~90行) | 集中管理 UI 模式：CEF 初始化、ImGui overlay 创建/移除、模式切换 |
| `HiAuRo/Infrastructure/PluginConfig.cs` | 修改 (+1字段) | 新增 `DisableCEF` 布尔字段 |
| `HiAuRo/Plugin.cs` | 修改 (~40行改动) | 委托 UI 给 UIManager，添加 `IsWebUI` 属性，替换 `_uiBridge` 空检查 |
| `HiAuRo/Plugin_Browsingway.cs` | 修改 (+1行) | `_browserHost` 从 `private` 改为 `internal` |
| `HiAuRo/UI/MainWindow.cs` | 修改 (~30行改动) | RadioButton 动态切换，低配模式灰掉 WebUI，新增 DisableCEF 复选框 |
| `HiAuRo/Runtime/ACRLifecycle.cs` | 修改 (10处替换) | `_uiBridge != null` → `Plugin.IsWebUI` |
| `Browsingway/Browsingway/Plugin.cs` | 修改 (+2行) | BrowserHost 添加 `OverlaysVisible` 属性，Render() 中跳过 |

---

### Task 1: PluginConfig.cs - 新增 DisableCEF 字段

**Files:**
- Modify: `HiAuRo/Infrastructure/PluginConfig.cs:38`

- [ ] **Step 1: 在 ImGuiThemeMode 后面添加 DisableCEF 字段**

在 `PluginConfig.cs` 第 40 行（`public ImGuiThemeMode ImGuiThemeMode` 之后）插入：

```csharp
/// <summary>低配置模式：完全禁用 CEF 渲染，节省 ~200MB 内存。需重启插件生效。</summary>
public bool DisableCEF { get; set; } = false;
```

- [ ] **Step 2: Commit**

```bash
git add HiAuRo/Infrastructure/PluginConfig.cs
git commit -m "feat: add DisableCEF config field for low-spec mode"
```

---

### Task 2: BrowserHost - 添加 OverlaysVisible 属性

**Files:**
- Modify: `Browsingway/Browsingway/Plugin.cs:51,203-217`

- [ ] **Step 1: 在 BrowserHost 类中添加 OverlaysVisible 属性**

在 `Browsingway/Browsingway/Plugin.cs` 第 51 行（`public event Action? OverlaysCreated;` 之后）插入：

```csharp
/// <summary>控制所有 CEF overlay 窗口的渲染可见性</summary>
public bool OverlaysVisible { get; set; } = true;
```

- [ ] **Step 2: 修改 Render() 方法，跳过时跳过所有 overlay 渲染**

将 `Render()` 方法（第 203-217 行）改为：

```csharp
private void Render()
{
    _dependencyManager.Render();

    if (!OverlaysVisible)
        return;

    if (++_renderFrameCount == 1)
        Services.PluginLog.Info($"[BW] 首帧渲染 (overlays={_overlays.Count}, renderProcess={_renderProcess != null})");

    ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));

    _renderProcess?.EnsureRenderProcessIsAlive();

    foreach (Overlay overlay in _overlays.Values) { overlay.Render(); }

    ImGui.PopStyleVar();
}
```

注意：把 `_dependencyManager.Render()` 放在 `OverlaysVisible` 检查之前，因为 DependencyManager 的渲染可能独立于 overlay 可见性。

- [ ] **Step 3: Commit**

```bash
git add Browsingway/Browsingway/Plugin.cs
git commit -m "feat: add OverlaysVisible toggle to BrowserHost"
```

---

### Task 3: Plugin_Browsingway.cs - 允许 UIManager 设置 _browserHost

**Files:**
- Modify: `HiAuRo/Plugin_Browsingway.cs:8`

- [ ] **Step 1: 将 _browserHost 从 private 改为 internal**

将 `HiAuRo/Plugin_Browsingway.cs` 第 8 行：
```csharp
private BrowserHost? _browserHost;
```
改为：
```csharp
internal BrowserHost? _browserHost;
```

这样 UIManager（同一程序集）可以赋值 `Plugin.Instance._browserHost = new BrowserHost(...)`。

- [ ] **Step 2: Commit**

```bash
git add HiAuRo/Plugin_Browsingway.cs
git commit -m "refactor: make _browserHost internal for UIManager access"
```

---

### Task 4: UIManager.cs - 创建 UI 模式管理器

**Files:**
- Create: `HiAuRo/UI/UIManager.cs`

- [ ] **Step 1: 创建新文件 HiAuRo/UI/UIManager.cs**

```csharp
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using HiAuRo.Infrastructure;
using HiAuRo.ImGuiLib;

namespace HiAuRo.UI;

/// <summary>
/// UI 模式管理器 —— 集中管理 WebUI (CEF) 和 ImGui 两种悬浮窗模式
/// </summary>
internal class UIManager : IDisposable
{
    private readonly PluginConfig _config;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly WindowSystem _windowSystem;
    private readonly Action _saveConfig;
    private readonly string _webRoot;

    private WebUiServer? _uiServer;
    private WebUiBridge? _uiBridge;

    private OverlayStatusBar? _overlayStatusBar;
    private OverlayQtPanel? _overlayQtPanel;
    private OverlayHotkeyPanel? _overlayHotkeyPanel;
    private DemoWindow? _demoWindow;

    /// <summary>当前是否为 WebUI 模式（CEF 已启用且当前选中 WebUI）</summary>
    public bool IsWebUI => _config.UIMode == UIMode.WebUI && !_config.DisableCEF;

    /// <summary>WebSocket 桥接，低配模式下为 null</summary>
    public WebUiBridge? Bridge => _uiBridge;

    public UIManager(PluginConfig config, IDalamudPluginInterface pluginInterface,
                     WindowSystem windowSystem, Action saveConfig, string webRoot)
    {
        _config = config;
        _pluginInterface = pluginInterface;
        _windowSystem = windowSystem;
        _saveConfig = saveConfig;
        _webRoot = webRoot;
    }

    /// <summary>根据当前配置初始化 UI 资源</summary>
    public void Init()
    {
        if (!_config.DisableCEF)
        {
            // 创建 CEF BrowserHost（渲染器进程异步启动）
            var browserHost = new Browsingway.BrowserHost(_pluginInterface);
            Plugin.Instance._browserHost = browserHost;

            // 创建 WebSocket 桥接 + HTTP 服务器
            _uiBridge = new WebUiBridge();
            _uiServer = new WebUiServer(_webRoot, _uiBridge);
            _uiServer.Start();

            // ImGui 模式下隐藏 CEF overlay
            if (_config.UIMode == UIMode.ImGui)
                browserHost.OverlaysVisible = false;
        }

        // ImGui 模式下创建 ImGui overlay 窗口
        if (_config.UIMode == UIMode.ImGui)
            CreateImGuiOverlays();
    }

    /// <summary>动态切换 UI 模式</summary>
    public void SwitchTo(UIMode mode)
    {
        if (mode == _config.UIMode) return;
        if (mode == UIMode.WebUI && _config.DisableCEF) return;

        _config.UIMode = mode;

        if (mode == UIMode.WebUI)
        {
            RemoveImGuiOverlays();
            if (Plugin.BrowserHost != null)
                Plugin.BrowserHost.OverlaysVisible = true;
        }
        else
        {
            if (Plugin.BrowserHost != null)
                Plugin.BrowserHost.OverlaysVisible = false;
            CreateImGuiOverlays();
        }

        _saveConfig();
        DService.Instance().Chat.Print($"[HiAuRo] UI 模式已切换为: {(mode == UIMode.WebUI ? "WebUI" : "ImGui")}");
    }

    private void CreateImGuiOverlays()
    {
        if (_overlayStatusBar != null) return; // 防止重复创建

        _demoWindow = new DemoWindow();
        _overlayStatusBar = new OverlayStatusBar(_config, _saveConfig);
        _overlayQtPanel = new OverlayQtPanel(_config, _saveConfig);
        _overlayHotkeyPanel = new OverlayHotkeyPanel(_config, _saveConfig);

        _windowSystem.AddWindow(_demoWindow);
        _windowSystem.AddWindow(_overlayStatusBar);
        _windowSystem.AddWindow(_overlayQtPanel);
        _windowSystem.AddWindow(_overlayHotkeyPanel);
    }

    private void RemoveImGuiOverlays()
    {
        if (_demoWindow != null) { _windowSystem.RemoveWindow(_demoWindow); _demoWindow = null; }
        if (_overlayStatusBar != null) { _windowSystem.RemoveWindow(_overlayStatusBar); _overlayStatusBar = null; }
        if (_overlayQtPanel != null) { _windowSystem.RemoveWindow(_overlayQtPanel); _overlayQtPanel = null; }
        if (_overlayHotkeyPanel != null) { _windowSystem.RemoveWindow(_overlayHotkeyPanel); _overlayHotkeyPanel = null; }
    }

    public void ShowDemoWindow()
    {
        if (_demoWindow != null)
        {
            _demoWindow.IsOpen = true;
        }
        else
        {
            _demoWindow = new DemoWindow();
            _windowSystem.AddWindow(_demoWindow);
            _demoWindow.IsOpen = true;
        }
    }

    public void Dispose()
    {
        RemoveImGuiOverlays();
        _uiServer?.Stop();
        _uiBridge?.Dispose();
        Plugin.Instance._browserHost?.Dispose();
        Plugin.Instance._browserHost = null;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add HiAuRo/UI/UIManager.cs
git commit -m "feat: add UIManager for dynamic overlay mode switching"
```

---

### Task 5: Plugin.cs - 集成 UIManager

**Files:**
- Modify: `HiAuRo/Plugin.cs:18-107,159-195,249-251,377-434`

改动较大，分步进行。

- [ ] **Step 1: 添加 _uiManager 字段和 IsWebUI 属性**

在 `Plugin.cs` 第 22 行（`_uiBridge` 声明后）添加：

```csharp
private UIManager? _uiManager;

/// <summary>当前是否处于 WebUI 模式（用于 ACRLifecycle 等判断状态推送通道）</summary>
public static bool IsWebUI => Instance._uiManager?.IsWebUI ?? false;
```

同时将 `_uiBridge` 添加 `set`（因为 UIManager 创建后需要赋值）：

第 22 行，将：
```csharp
internal readonly WebUiBridge? _uiBridge;
```
改为：
```csharp
internal WebUiBridge? _uiBridge;
```

- [ ] **Step 2: 修改构造函数 — 移除旧的模式分支初始化**

在构造函数中，删除第 47-48 行：
```csharp
if (_config.UIMode == Infrastructure.UIMode.WebUI)
    BrowsingwayPluginInit(pluginInterface);
```

（BrowsingwayPluginInit 调用被 UIManager 替代）

删除第 78-85 行的 WebUI 初始化块：
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

删除第 97-107 行的 ImGui overlay 创建块：
```csharp
if (_config.UIMode == Infrastructure.UIMode.ImGui)
{
    _demoWindow = new DemoWindow();
    _overlayStatusBar = new OverlayStatusBar(_config, ...);
    _overlayQtPanel = new OverlayQtPanel(_config, ...);
    _overlayHotkeyPanel = new OverlayHotkeyPanel(_config, ...);
    _windowSystem.AddWindow(_demoWindow);
    _windowSystem.AddWindow(_overlayStatusBar);
    _windowSystem.AddWindow(_overlayQtPanel);
    _windowSystem.AddWindow(_overlayHotkeyPanel);
}
```

同时删除不再需要的字段声明（第 25-28 行）：
```csharp
private OverlayStatusBar? _overlayStatusBar;
private OverlayQtPanel? _overlayQtPanel;
private OverlayHotkeyPanel? _overlayHotkeyPanel;
private DemoWindow? _demoWindow;
```

（这些字段移到 UIManager 中）

- [ ] **Step 3: 添加 UIManager 初始化代码**

在 `WindowSystem` 和 `MainWindow` 创建之前（原第 90 行附近），插入：

```csharp
// 初始化 UIManager（替代旧的 BrowsingwayPluginInit + WebUI/ImGui 分支）
_windowSystem = new WindowSystem("HiAuRo");
_uiManager = new UIManager(_config, _pluginInterface, _windowSystem,
    () => _pluginInterface.SavePluginConfig(_config), webRoot);
_uiManager.Init();
_uiBridge = _uiManager.Bridge;

if (_uiBridge != null)
{
    RegisterUiHandlers(_uiBridge);
    AuthoringServer.Instance.Register(_uiBridge);
}
```

注意：此时 `_windowSystem` 和 `_uiManager` 需要互换位置 — `_windowSystem` 必须在 `_uiManager.Init()` 之前创建（因为 Init 可能需要添加 ImGui overlay 窗口）。

原第 90-95 行的 WindowSystem/MainWindow 代码调整位置：
```csharp
// _windowSystem 已经在 UIManager 初始化之前创建
_mainWindow = new MainWindow(_config, () => _pluginInterface.SavePluginConfig(_config));
_windowSystem.AddWindow(_mainWindow);
_pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
_pluginInterface.UiBuilder.OpenMainUi += () => _mainWindow.IsOpen = !_mainWindow.IsOpen;
_pluginInterface.UiBuilder.OpenConfigUi += () => _mainWindow.IsOpen = !_mainWindow.IsOpen;
```

完整构造函数从第 90 行开始变为：

```csharp
// 第 90 行附近（替换原来到第 107 行的内容）
_windowSystem = new WindowSystem("HiAuRo");
_uiManager = new UIManager(_config, _pluginInterface, _windowSystem,
    () => _pluginInterface.SavePluginConfig(_config), webRoot);
_uiManager.Init();
_uiBridge = _uiManager.Bridge;
if (_uiBridge != null)
{
    RegisterUiHandlers(_uiBridge);
    AuthoringServer.Instance.Register(_uiBridge);
}

_mainWindow = new MainWindow(_config, () => _pluginInterface.SavePluginConfig(_config));
_windowSystem.AddWindow(_mainWindow);
_pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
_pluginInterface.UiBuilder.OpenMainUi += () => _mainWindow.IsOpen = !_mainWindow.IsOpen;
_pluginInterface.UiBuilder.OpenConfigUi += () => _mainWindow.IsOpen = !_mainWindow.IsOpen;
```

- [ ] **Step 4: 修改 Dispose() — 用 _uiManager.Dispose() 替代旧清理**

在 `Dispose()` 方法（第 228 行起）中，将：
```csharp
    _uiServer?.Stop();
    _uiBridge?.Dispose();
BrowsingwayDispose();
```
替换为：
```csharp
_uiManager?.Dispose();
```

同样在 `SafeDispose()` 方法（第 159 行起）中，将：
```csharp
        _uiServer?.Stop();
        _uiBridge?.Dispose();
        BrowsingwayDispose();
```
替换为：
```csharp
_uiManager?.Dispose();
```

- [ ] **Step 5: 替换 _uiBridge 空检查为 IsWebUI**

在 `SendStatusState()`（第 377-385 行）：
```csharp
private static async Task SendStatusState()
{
    if (!IsWebUI) return;
    await Instance._uiBridge!.SendAsync(new
    {
        type = "acrState",
        data = new { enabled = RuntimeCore.IsRunning }
    });
}
```

在 `SendUiSettings()`（第 387-404 行）：
```csharp
private static async Task SendUiSettings(HiAuRo.ACR.UiSettings s)
{
    if (!IsWebUI) return;
    await Instance._uiBridge!.SendAsync(new
    {
        type = "uiSettings",
        data = new { ... }
    });
}
```

在 `SendPauseState()`（第 406-414 行）：
```csharp
private static async Task SendPauseState()
{
    if (!IsWebUI) return;
    await Instance._uiBridge!.SendAsync(new
    {
        type = "pauseChanged",
        data = new { paused = HiAuRo.ACR.MainControlHelper.IsPaused }
    });
}
```

在 `OnHotkeyExecuted()`（第 416-424 行）：
```csharp
private static void OnHotkeyExecuted(string id, string label)
{
    if (!IsWebUI) return;
    _ = Instance._uiBridge!.SendAsync(new
    {
        type = "hotkeyExecuted",
        data = new { id, label }
    });
}
```

在 `OnQtChanged()`（第 426-434 行）：
```csharp
private static void OnQtChanged(string id, bool value)
{
    if (!IsWebUI) return;
    _ = Instance._uiBridge!.SendAsync(new
    {
        type = "qtChanged",
        data = new { id, value }
    });
}
```

注意：`_uiBridge` 在 IsWebUI 为 true 时保证非 null，所以使用 `!` 断言安全。

- [ ] **Step 6: 修改 ShowDemoWindow() 委托给 UIManager**

将第 436-448 行的 `ShowDemoWindow()` 方法改为：

```csharp
public void ShowDemoWindow()
{
    _uiManager?.ShowDemoWindow();
}
```

- [ ] **Step 7: Commit**

```bash
git add HiAuRo/Plugin.cs
git commit -m "feat: integrate UIManager into Plugin, replace uiBridge checks with IsWebUI"
```

---

### Task 6: ACRLifecycle.cs - 替换模式判断

**Files:**
- Modify: `HiAuRo/Runtime/ACRLifecycle.cs:106,223,241,277`

- [ ] **Step 1: 替换所有 _uiBridge != null 检查为 Plugin.IsWebUI**

**第 106 行** (CheckJobSwitch)：
```csharp
// Before:
if (Plugin.Instance._uiBridge != null)
// After:
if (Plugin.IsWebUI)
```

**第 108 行** (CheckJobSwitch 中的 SendAsync)：
```csharp
// Before:
_ = Plugin.Instance._uiBridge.SendAsync(new
// After:
_ = Plugin.Instance._uiBridge!.SendAsync(new
```
注意：进入这个分支时 IsWebUI 保证 _uiBridge 非 null。

**第 223 行** (LoadRotation)：
```csharp
// Before:
if (Plugin.Instance._uiBridge != null)
// After:
if (Plugin.IsWebUI)
```

**第 225 行** (LoadRotation 中的 SendAsync)：
```csharp
// Before:
_ = Plugin.Instance._uiBridge.SendAsync(new
// After:
_ = Plugin.Instance._uiBridge!.SendAsync(new
```

**第 230 行** (LoadRotation 中的 CacheControls)：
```csharp
// Before:
Plugin.Instance._uiBridge.CacheControls(controls);
// After:
Plugin.Instance._uiBridge!.CacheControls(controls);
```

**第 241 行** (LoadRotation)：
```csharp
// Before:
if (Plugin.Instance._uiBridge != null)
// After:
if (Plugin.IsWebUI)
```

**第 243 行** (LoadRotation 中的 SendAsync)：
```csharp
// Before:
_ = Plugin.Instance._uiBridge.SendAsync(new
// After:
_ = Plugin.Instance._uiBridge!.SendAsync(new
```

**第 257 行** (LoadRotation 中的 CacheUiSettings)：
```csharp
// Before:
Plugin.Instance._uiBridge.CacheUiSettings(new
// After:
Plugin.Instance._uiBridge!.CacheUiSettings(new
```

**第 277 行** (LoadRotation)：
```csharp
// Before:
if (Plugin.Instance._uiBridge != null)
// After:
if (Plugin.IsWebUI)
```

**第 279 行** (LoadRotation 中的 SendAsync)：
```csharp
// Before:
_ = Plugin.Instance._uiBridge.SendAsync(new
// After:
_ = Plugin.Instance._uiBridge!.SendAsync(new
```

- [ ] **Step 2: Commit**

```bash
git add HiAuRo/Runtime/ACRLifecycle.cs
git commit -m "refactor: use Plugin.IsWebUI instead of _uiBridge null check in ACRLifecycle"
```

---

### Task 7: MainWindow.cs - 动态切换 UI + 低配模式复选框

**Files:**
- Modify: `HiAuRo/UI/MainWindow.cs:64-105`

- [ ] **Step 1: 修改 DrawStatus() 中的 RadioButton 逻辑**

将第 64-105 行的 `DrawStatus()` 方法中的 UI 模式相关内容替换为：

```csharp
private void DrawStatus()
{
    // UI 渲染模式切换
    ImGui.TextColored(Theme.Colors.AccentBlue, "UI 渲染模式:");
    ImGui.SameLine();

    var isWebUI = _config.UIMode == Infrastructure.UIMode.WebUI;
    var cefDisabled = _config.DisableCEF;

    void ApplySwitch(UIMode mode)
    {
        Plugin.Instance._uiManager?.SwitchTo(mode);
    }

    // WebUI 选项 —— 低配模式下灰掉
    if (cefDisabled)
    {
        ImGui.BeginDisabled();
        ImGui.RadioButton("WebUI (CEF 已禁用)", false);
        ImGui.EndDisabled();
    }
    else if (ImGui.RadioButton("WebUI", isWebUI))
    {
        ApplySwitch(Infrastructure.UIMode.WebUI);
    }

    ImGui.SameLine();
    if (ImGui.RadioButton("ImGui", !isWebUI))
    {
        ApplySwitch(Infrastructure.UIMode.ImGui);
    }

    // 低配置模式复选框
    ImGui.Spacing();
    var newCefDisabled = cefDisabled;
    if (ImGui.Checkbox("低配置模式 (禁用 CEF 以节省 ~200MB 内存)", ref newCefDisabled))
    {
        _config.DisableCEF = newCefDisabled;
        _saveConfig();
    }

    if (_config.DisableCEF)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Colors.AccentOrange);
        ImGui.TextWrapped("CEF 已禁用，WebUI 不可用。切换此选项需重启插件生效。");
        ImGui.PopStyleColor();
    }

    // ImGui 主题模式（仅 ImGui 模式显示）
    if (!isWebUI)
    {
        ImGui.Spacing();
        var isLight = _config.ImGuiThemeMode == ImGuiThemeMode.Light;
        ImGui.TextColored(Theme.Colors.AccentBlue, "ImGui 主题:");
        ImGui.SameLine();
        if (ImGui.RadioButton("亮色", isLight))
        {
            _config.ImGuiThemeMode = ImGuiThemeMode.Light;
            Theme.Mode = Theme.ThemeMode.Light;
            _saveConfig();
        }
        ImGui.SameLine();
        if (ImGui.RadioButton("暗色", !isLight))
        {
            _config.ImGuiThemeMode = ImGuiThemeMode.Dark;
            Theme.Mode = Theme.ThemeMode.Dark;
            _saveConfig();
        }
    }

    // ... 后续保持不变的代码（ACR 运行状态等）...
    ImGui.Separator();
    ImGui.Spacing();
    ImGui.Text("ACR 运行状态");
    // ...（不变）
```

注意：需要引入 `using HiAuRo.Infrastructure;`（应该已存在）。

- [ ] **Step 2: Commit**

```bash
git add HiAuRo/UI/MainWindow.cs
git commit -m "feat: dynamic UI mode switch + disable CEF checkbox in MainWindow"
```

---

### Task 8: 构建验证

**Files:** 无新建，全项目构建

- [ ] **Step 1: 构建项目**

```bash
cmd.exe /c "dotnet build /mnt/e/HiAuRo/HiAuRo/HiAuRo.csproj -nologo"
```

期望: Build succeeded with 0 Errors

- [ ] **Step 2: 检查编译警告/错误**

如果有编译错误，根据错误信息修正对应文件。

- [ ] **Step 3: 检查无残留的旧引用**

```bash
grep -rn "_uiBridge" /mnt/e/HiAuRo/HiAuRo/HiAuRo/Runtime/ACRLifecycle.cs
grep -rn "IsWebUI" /mnt/e/HiAuRo/HiAuRo/HiAuRo/Runtime/ACRLifecycle.cs
```

确认所有 `_uiBridge` 引用都已替换为 `Plugin.Instance._uiBridge!` + `IsWebUI` 守卫。

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "chore: verify build after all changes"
```
