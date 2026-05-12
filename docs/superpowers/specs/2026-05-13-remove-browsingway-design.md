# Browsingway 移除设计

## 概述

移除 HiAuRo 仓库中的 Browsingway CEF 渲染引擎代码。HiAuRo 不再负责悬浮窗的绘制，仅保留 WebUiServer（HTTP + WebSocket）为外部插件提供 HTML/CSS/JS 资源。外部插件（用户自改的 Browsingway 分支）通过 Dalamud IPC 接收显示控制指令。

## 变更清单

### 1. 文件删除

| 路径 | 说明 |
|------|------|
| `Browsingway/` 整个目录 | CEF 渲染器子进程 + BrowserHost 库 + Common IPC |
| `HiAuRo/Plugin_Browsingway.cs` | 10 行 partial class，持 `_browserHost` 实例和 `BrowserHost` 静态属性 |

### 2. 项目/构建层

| 文件 | 变更 |
|------|------|
| `HiAuRo.slnx` | 删除 `<Folder Name="/Browsingway/">` 及其下 3 个 `<Project>` 条目 |
| `HiAuRo/HiAuRo.csproj:17-21` | 删除 3 个 `ProjectReference`（Browsingway.Common / Browsingway / Browsingway.Renderer） |
| `HiAuRo/HiAuRo.csproj:45-63` | 删除 `CopyRendererOutput` MSBuild Target（不再复制 `renderer/` 输出） |
| `HiAuRo/packages.lock.json` | 删除 Browsingway 相关锁定行 |

### 3. 代码清理

#### 3.1 `UiManager.cs`

- 删除 `using Browsingway;`
- `IsWebUI`: 从 `_uiBridge != null && _config.UIMode == UIMode.WebUI && !_config.DisableCEF` 简化为 `_config.UIMode == UIMode.WebUI`（WebUiServer 始终创建）
- `Init()`: 删掉整个 `if (!_config.DisableCEF)` 块。WebUiServer + WebUiBridge 移到外面**始终创建**（不再依赖配置开关）。移除 `new BrowserHost(pi)`、`Plugin.Instance._browserHost = browserHost`、`browserHost.OverlaysVisible = false`。
- `SwitchTo(mode)`: 删掉 `DisableCEF` 检查。WebUI → `RemoveImGuiOverlays()`，ImGui → `CreateImGuiOverlays()`，不再操作 `BrowserHost.OverlaysVisible`。
- `Dispose()`: 删掉 `Plugin.Instance._browserHost?.Dispose()` 行。

#### 3.2 `Plugin.cs`

- 将 `Browsingway.Services.Framework.RunOnFrameworkThread(...)`（第 285 行）替换为 `DService.Instance().Framework.RunOnFrameworkThread(...)`（OmenTools 等效 API）
- 删除 `Instance._browserHost?.UpdateOverlay(...)`（第 351 行，在 contentResize handler 中），保留其后的 UI Settings 持久化逻辑
- 启动消息（第 126 行）删除 `悬浮窗: localhost:5678/jobview.html`

#### 3.3 `MainWindow.cs`

- `DrawStatus()`: 
  - 简化 UI 模式单选按钮（WebUI / ImGui），移除 `DisableCEF` 低配模式相关灰掉/禁用逻辑
  - 删除 `DisableCEF` 复选框（第 97-111 行）
- `DrawOverlaySettings()`:
  - 删除 `var host = Plugin.BrowserHost;`（第 369 行）
  - 删除所有 `host?.UpdateOverlay(...)` 调用（第 390, 399, 408, 417, 425 行）
  - 删除 `CEF DevTools` 按钮（第 430-431 行）
  - 标题从 `CEF 游戏内悬浮窗` 改为 `外部悬浮窗`

#### 3.4 `ACRLifecycle.cs`

- 删除 `Plugin.BrowserHost?.UpdateOverlay(...)` 循环（第 317-325 行），保留 `settings.OverlayContentWidth` 等持久化数据（后续 IPC 使用）

### 4. 配置 (`PluginConfig.cs`)

| 变更 | 说明 |
|------|------|
| 删除 `DisableCEF` 字段 | 不再需要 |
| 保留 `UIMode` 枚举 | WebUI=隐藏ImGui窗口(发IPC通知外部)，ImGui=显示ImGui |
| 保留 `Overlays` 数组 | 悬浮窗定义数据，后续 IPC 使用，名称不变 |

### 5. 保留不变

- `WebUiServer`（HTTP localhost:5678，提供 `/main.html`, `/action.html` 等）
- `WebUiBridge`（WebSocket localhost:5679，JS ↔ C# 通信）
- `UI/web/` 下全部 HTML/CSS/JS
- `Overlays` 配置持久化
- UI Settings 中 `OverlayContentWidth/OverlayContentHeight` 持久化

## 兼容性

- 现有用户的 `PluginConfig` 会在下次加载时自动失去 `DisableCEF` 字段（Dalamud 序列化忽略未知字段），无需迁移
- UIMode 切换逻辑保持同名枚举值，WebUI 模式下 ImGui 悬浮窗隐藏行为不变
