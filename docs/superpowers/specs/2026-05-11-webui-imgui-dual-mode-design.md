# WebUI / ImGui 双模式切换设计

## 背景

基于 `2026-05-10-webui-perf-06-imgui-components.md` 的 ImGui 组件库方案，但 **保留 WebUI**，改为双模式可切换架构。

## 目标

1. 用户可在 WebUI 和 ImGui 两种 UI 渲染模式间切换
2. ImGui 模式下释放 CEF 的 ~200-300MB 内存和 3 个子进程
3. WebUI 模式保持现有体验不变（向后兼容）
4. 切换通过 MainWindow 控件完成，重启插件后生效

## 架构

```
PluginConfig.UIMode ──→ Plugin 构造函数 ──→ 分支初始化
                          │
              ┌───────────┴───────────┐
              ▼                       ▼
         WebUI 模式               ImGui 模式
    Browsingway ✓              Browsingway ✗
    WebUiServer ✓              WebUiServer ✗
    WebUiBridge ✓              ImGui Overlays ✓
    CEF 进程 ~3个              CEF 进程 0个
    内存 ~200-300MB额外        内存 ~0额外
```

### 切换流程

```
MainWindow [WebUI ● / ImGui ○] → 保存 PluginConfig.UIMode → 提示"重启后生效"
                                                              │
                                      用户 Disable/Enable 插件
                                                              ▼
                                  Plugin 重新构造 → 读取 UIMode → 分支初始化
```

### 状态通信（ImGui 模式）

ACRLifecycle 写入静态状态字典，ImGui overlay 窗口在 `Draw()` 中读取：

```
ACRLifecycle                  ImGuiOverlayState              ImGui 窗口
    │                              │                            │
    ├─ UpdateImGuiState() ──────→  │ IsRunning, IsPaused       │
    │                              │ CurrentJob, CurrentAcr     │
    │                              │ Controls[], UiSettings     │
    │                              │ Qts[], Hotkeys[]           │
    │                              │                            │
    │                              │ ←── Draw() 读取 ──────────┤
```

用户交互直接调用 C# handler（不经过 WebSocket）：
```
用户点击 ImGui 按钮 → ImGui 回调 → HotkeyHelper/QTHelper/ACRLifecycle 直接方法调用
```

## 配置

### PluginConfig 新增

```csharp
public enum UIMode { WebUI = 0, ImGui = 1 }

public UIMode UIMode { get; set; } = UIMode.WebUI; // 默认 WebUI，向后兼容
```

## 组件库 (`UI/ImGui/`)

遵循 "Keep code flat and direct" 原则，组件不拆分为每组件一文件：

| 文件 | 内容 |
|------|------|
| `Theme.cs` | Ant Design 设计令牌（颜色/间距/圆角/字体常量） |
| `ComponentLibrary.cs` | 所有通用组件：Button、Switch、Slider、Select、Tabs、Card、Tag、Divider、Badge、InputNumber、Notification（汇总在一个文件中） |
| `AnimationHelper.cs` | Lerp/easing 动画工具类（Switch 切换动画等） |
| `ImGuiOverlayState.cs` | 静态状态字典（ACRLifecycle ↔ ImGui 窗口的数据通道） |
| `ImGuiWidgetRenderer.cs` | UiControlDef → ImGui 组件映射渲染器 |
| `OverlayBase.cs` | Overlay 窗口基类（无边框拖动、位置持久化） |
| `OverlayStatusBar.cs` | 状态栏 + ACR 控制面板（可折叠） |
| `OverlayActionPanel.cs` | QT 芯片 + 热键网格面板 |
| `DemoWindow.cs` | 组件展示窗口 `/hi gallery` |

## Overlay 窗口

### OverlayStatusBar

- 无边框浮动窗口 (`ImGuiWindowFlags.NoTitleBar | NoResize | NoScrollbar`)
- 折叠态：48px 高度，显示状态圆点 + ACR 名称 + 控制按钮 + 展开箭头
- 展开态：Tab 切换 + Group 卡片 + ACR 控件
- 空白区域鼠标拖动改变位置，关闭时保存到 config

### OverlayActionPanel

- 无边框浮动窗口
- QT 芯片行（Tag 组件渲染，绿色=开，灰色=关）
- 热键网格（图标 + 文字 + 键位角标）
- 同样支持拖动 + 位置持久化

### DemoWindow

- `/hi gallery` 命令打开
- 标准 Dalamud 窗口（带标题栏，开发工具用途）
- 展示所有组件及状态（正常/hover/active/disabled）
- 附带暗色/亮色主题预览

### OverlayBase

提供所有 Overlay 窗口的公共逻辑：
- 无边框 + 自定义拖拽（检测空白区域 mousedown → 计算 delta → 更新位置）
- 位置从 config 读写 (`PluginConfig.OverlayX/Y`)
- `IsVisible` 属性控制显示/隐藏

## ImGuiWidgetRenderer

```csharp
public static void Render(List<UiControlDef> controls, string activeTab)
{
    // 按 Tab → Group → Items 结构遍历
    // 每个控件按 Type 路由到组件库对应方法
    // checkbox → Switch()    slider → Slider()
    // dropdown → Select()    intInput → InputNumber()
    // label → Label()        separator → Divider()
    // 交互返回值 → 更新 ImGuiOverlayState → SettingMgr 持久化
}
```

## MainWindow 切换控件

在 **状态 (Status)** Tab 最上方添加：

```
UI 渲染模式:  [● WebUI]  [○ ImGui]
⚠ 切换后请重启插件生效 (Disable/Enable)
```

两个 RadioButton，onClick 写入 `config.UIMode` 并 `config.Save()`。

## ACKLifecycle 修改

现有推送逻辑改为分支：

```csharp
if (Plugin.Instance.Config.UIMode == UIMode.WebUI)
{
    WebUiBridge.SendAsync(new { type = "status", data = ... });
    WebUiBridge.SendAsync(new { type = "controls", data = controls });
}
else
{
    ImGuiOverlayState.UpdateStatus(...);
    ImGuiOverlayState.UpdateControls(controls);
    ImGuiOverlayState.UpdateUiSettings(settings);
}
```

## 文件修改清单

### 新增 (9 个文件)

```
UI/ImGui/Theme.cs
UI/ImGui/ComponentLibrary.cs
UI/ImGui/AnimationHelper.cs
UI/ImGui/ImGuiOverlayState.cs
UI/ImGui/ImGuiWidgetRenderer.cs
UI/ImGui/OverlayBase.cs
UI/ImGui/OverlayStatusBar.cs
UI/ImGui/OverlayActionPanel.cs
UI/ImGui/DemoWindow.cs
```

### 修改 (4 个文件)

| 文件 | 改动 |
|------|------|
| `Infrastructure/PluginConfig.cs` | +UIMode 枚举和属性，+Overlay 位置属性 |
| `Plugin.cs` | 构造函数中根据 UIMode 分支初始化，RegisterUiHandlers 分支 |
| `Runtime/ACRLifecycle.cs` | 状态推送分支（WebUI vs ImGui） |
| `UI/MainWindow.cs` | 状态 Tab 添加模式切换控件 |

### 不删除任何文件

WebUI 全部保留（`UI/web/`、`Plugin_Browsingway.cs`、`WebUiServer.cs`、`WebUiBridge.cs`、`CefOverlayConfig.cs` 等）。

## 验证

1. 默认 WebUI 模式启动 → CEF overlay 正常工作（回归）
2. MainWindow 切换为 ImGui → 保存配置
3. 重启插件 → CEF 进程不存在，ImGui overlay 窗口显示
4. 加载 ACR（如 BLU）→ 控件在 StatusBar 中正确渲染
5. 点击按钮、切换 QT、触发热键 → 功能正常
6. `/hi gallery` → DemoWindow 展示所有组件
7. 切回 WebUI → 重启 → CEF overlay 恢复

## 收益

| 指标 | WebUI 模式 | ImGui 模式 |
|------|-----------|-----------|
| 交互延迟 | WebSocket 3-6ms | 0ms（同线程 C#） |
| 内存 | +200-300MB | +0MB |
| 进程数 | 4 | 1 |
| 开发语言 | C# + JS + CSS | 仅 C# |
| 设计一致性 | 手写 CSS | 统一设计系统 |

用户可根据场景选择：
- 开发/调试 ACR → WebUI 模式（浏览器 DevTools）
- 日常使用/低配机器 → ImGui 模式（零开销）
