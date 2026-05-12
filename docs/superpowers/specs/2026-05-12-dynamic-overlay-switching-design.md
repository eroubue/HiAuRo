# 悬浮窗模式动态切换设计

**日期**: 2026-05-12
**状态**: 已确认

## 目标

让 WebUI 和 ImGui 两种悬浮窗模式可以在运行时动态切换，无需重启插件。同时提供低配置选项完全禁用 CEF 以节省内存。

## 新增配置

```csharp
// PluginConfig.cs 新增
public bool DisableCEF { get; set; } = false; // 低配置模式
```

## 三种运行时状态

| 状态 | DisableCEF | UIMode | CEF 进程 | 可切换 |
|------|-----------|--------|---------|--------|
| 默认 WebUI | false | WebUI | 运行中 | 随时切 ImGui |
| 默认 ImGui | false | ImGui | 后台运行 | 随时切 WebUI |
| 低配置 ImGui | true | ImGui | 未启动 | WebUI 选项灰掉 |

## 资源生命周期

- **BrowserHost (CEF)**: `DisableCEF=false` 时始终运行，永不销毁（仅插件 Dispose 清理）。`DisableCEF=true` 时从不创建。
- **WebUiServer + WebUiBridge**: 同上，`DisableCEF=false` 时始终运行。
- **ImGui Overlay 窗口** (StatusBar, QtPanel, HotkeyPanel): 动态添加/移除到 WindowSystem。

## 核心类型: UIManager

**文件**: `HiAuRo/UI/UIManager.cs` (~100行)

```csharp
class UIManager : IDisposable
{
    // 依赖
    PluginConfig _config;
    IDalamudPluginInterface _pluginInterface;
    WindowSystem _windowSystem;
    Action _saveConfig;
    
    // CEF 资源 (DisableCEF=false 时创建, 永不销毁)
    BrowserHost? _browserHost;
    WebUiServer? _uiServer;
    WebUiBridge? _uiBridge;
    
    // ImGui 资源 (按需创建/销毁)
    OverlayStatusBar? _overlayStatusBar;
    OverlayQtPanel? _overlayQtPanel;
    OverlayHotkeyPanel? _overlayHotkeyPanel;
    DemoWindow? _demoWindow;
    
    public bool IsWebUI => _config.UIMode == UIMode.WebUI && !_config.DisableCEF;
    public WebUiBridge? Bridge => _uiBridge;
    
    void Init()           // 根据当前配置初始化
    void SwitchTo(UIMode) // 动态切换模式
    void SetOverlaysVisible(bool) // 控制 CEF overlay 显示/隐藏
    void ShowDemoWindow()
    void Dispose()
}
```

## 切换流程

### WebUI → ImGui (默认模式, 瞬时)
1. config.UIMode = ImGui
2. BrowserHost.SetOverlaysVisible(false) — 隐藏 CEF 悬浮窗
3. WindowSystem.AddWindow(StatusBar+Qt+Hotkey) — 显示 ImGui 悬浮窗
4. 保存配置

### ImGui → WebUI (默认模式, 瞬时)
1. config.UIMode = WebUI
2. WindowSystem.RemoveWindow(StatusBar+Qt+Hotkey) — 隐藏 ImGui 悬浮窗
3. BrowserHost.SetOverlaysVisible(true) — 显示 CEF 悬浮窗
4. 保存配置

### 低配置模式 (CEF 从未启动)
- WebUI RadioButton 灰掉不可点击
- 切换 DisableCEF 标记需重启生效

## 文件改动清单

| 文件 | 改动类型 | 说明 |
|------|---------|------|
| `HiAuRo/UI/UIManager.cs` | 新增 | 集中管理 UI 模式生命周期 |
| `HiAuRo/Plugin.cs` | 修改 | 委托 UI 初始化给 UIManager，添加 IsWebUI 属性 |
| `HiAuRo/Plugin_Browsingway.cs` | 修改 | 添加 SetOverlaysVisible(bool) |
| `HiAuRo/UI/MainWindow.cs` | 修改 | 即时切换、低配模式灰掉 WebUI |
| `HiAuRo/Runtime/ACRLifecycle.cs` | 修改 | _uiBridge != null → Plugin.IsWebUI |
| `HiAuRo/Infrastructure/PluginConfig.cs` | 修改 | 新增 DisableCEF 字段 |
| `Browsingway/Browsingway/Plugin.cs` | 修改 | BrowserHost 添加 OverlaysVisible 属性 |

## 注意事项

- ACRLifecycle、Plugin 中所有 `_uiBridge != null` 分支判断改为 `Plugin.IsWebUI`
- DisableCEF 在低配模式切换时需重启插件
- ImGui overlay 窗口的位置从 PluginConfig 持久化恢复
