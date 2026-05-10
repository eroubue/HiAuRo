# WebUI 性能优化 — 方向 2: 合并 QT + 热键为 ActionPanel

## 问题

当前有 3 个独立的 CEF overlay（MainWindow、QtWindow、HotkeyWindow），每个占用一个 CEF 实例、一个 ImGui 窗口、一个 D3D11 共享纹理。QT 和热键都是小型战斗操作面板，可以合并以减少资源占用。

## 目标

将 QtWindow（320×80）和 HotkeyWindow（300×160）合并为单个 ActionPanel overlay，CEF 实例数从 3 减至 2，节省约 33% 的渲染开销。

## 约束

- 三个 overlay 在屏幕上独立摆放位置不变（MainWindow 在非战斗区域，ActionPanel 在战斗区域）
- 合并后不改变用户操作流程——QT 点击和热键点击的交互方式不变
- 仅合并 UI 渲染层，WebSocket 消息、C# 业务逻辑不变

## 设计

### 布局

垂直布局：QT 芯片行在上，热键网格在下。

```
┌──────────────────────┐
│ [QT]  [QT]  [QT]    │  ← 自动高度（根据行数），为空时隐藏
│ [HK]  [HK]  [HK]    │  ← 自适应填充剩余空间，为空时隐藏
│ [HK]  [HK]  [HK]    │
└──────────────────────┘
```

- QT 和热键各自区域通过 CSS `:empty` 选择器隐藏（或 flex-basis: 0 + overflow: hidden）
- 初始尺寸 320×240，方向 1 的 `reportContentSize()` 首次上报后自动微调

### 新增文件

`HiAuRo/UI/web/action.html`

```html
<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="UTF-8">
  <title>HiAuRo Action</title>
  <link rel="stylesheet" href="style.css?v=2">
</head>
<body>
<div id="action-panel">
  <div class="drag-dot"></div>
  <div class="action-body" id="action-body">
    <div id="qt-grid"></div>
    <div id="hk-grid"></div>
  </div>
</div>
<script src="app.js"></script>
</body>
</html>
```

app.js 的 `OVERLAY_NAME`：

```
const OVERLAY_NAME = 'ActionPanel';
```

### app.js 变动

无新增逻辑。现有的 `renderQt()` / `renderHk()` 已在全局消息分发中自动调用（`renderAll()` → `renderQt()` → `renderHk()`）。action.html 同时包含 `#qt-grid` 和 `#hk-grid`，两个函数都会正确渲染。

`reportContentSize()` 汇报 `ActionPanel` 的整体尺寸，方向 1 的机制会自动调整 ImGui 窗口。

### C# 端变动

**Browsingway/Plugin.cs (BrowserHost)**

```csharp
// CreateHiAuRoOverlays: 替换
Add("QtWindow", "http://localhost:5678/qt.html", 320, 80);
Add("HotkeyWindow", "http://localhost:5678/hotkey.html", 300, 160);
// 为
Add("ActionPanel", "http://localhost:5678/action.html", 320, 240);
```

**HiAuRo/Infrastructure/PluginConfig.cs**

```csharp
// Overlays 默认值: 替换
new() { Name = "QtWindow", Url = "...", Width = 200, Height = 50 },
new() { Name = "HotkeyWindow", Url = "...", Width = 260, Height = 130 },
// 为
new() { Name = "ActionPanel", Url = "http://localhost:5678/action.html", Width = 320, Height = 240 },
```

### 旧配置兼容

已保存的 PluginConfig 中若包含 QtWindow/HotkeyWindow 条目，BrowserHost.CreateHiAuRoOverlays 按名称创建时找不到就不会创建。首次保存新配置时会写入 ActionPanel。无需额外迁移代码。

### 删除文件（可选）

`qt.html` 和 `hotkey.html` 不再被 BrowserHost 引用，可保留作为调试用途或删除。

## 资源节省

| 资源 | 改前 | 改后 | 节省 |
|------|------|------|------|
| CEF 实例 | 3 | 2 | -33% |
| ImGui 窗口 | 3 | 2 | -33% |
| D3D11 共享纹理 | 3 个 SRV | 2 个 SRV | -33% |
| OnPaint 帧率合计 | 3×30=90fps | 2×30=60fps | -33% |
| 渲染进程内存 | ~300MB | ~200MB | ~33% |

## 修改清单

| 文件 | 改动 |
|------|------|
| `HiAuRo/UI/web/action.html` | **新增** — 合并后的面板 HTML |
| `HiAuRo/UI/web/app.js` | 新增 `OVERLAY_NAME` 常量定义 |
| `Browsingway/Browsingway/Plugin.cs` | `CreateHiAuRoOverlays()` 替换 overlay 定义 |
| `HiAuRo/Infrastructure/PluginConfig.cs` | `Overlays` 默认值替换 |
