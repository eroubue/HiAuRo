# HiAuRo → Browsingway IPC 需求

## 背景

HiAuRo 在 `localhost:5678`（冲突时 5679）启动 HTTP + WebSocket 服务器，需要 Browsingway 将 HTML 页面渲染为 FFXIV 游戏内覆盖层窗口。HiAuRo 是 IPC 消费者，Browsingway 作为 IPC Provider 暴露以下端点。

## IPC 端点

### 1. `Browsingway.IsReady`

判断 Browsingway 插件及 CEF 渲染进程是否已就绪，可以接收 overlay 指令。

| 项 | 值 |
|----|-----|
| 方向 | HiAuRo → Browsingway |
| 参数 | 无 |
| 返回 | `bool` |

---

### 2. `Browsingway.Overlay.Exists`

查询指定的 overlay 是否已创建。

| 项 | 值 |
|----|-----|
| 方向 | HiAuRo → Browsingway |
| 参数 | `name` (`string`) — overlay 名称 |
| 返回 | `bool` |

---

### 3. `Browsingway.Overlay.CreateOrUpdate`

创建 overlay（不存在时）或更新其属性（已存在时）。idempotent，可重复调用。

| 项 | 值 |
|----|-----|
| 方向 | HiAuRo → Browsingway |
| 返回 | `void` |

**参数：**

| 参数 | 类型 | 说明 |
|------|------|------|
| `name` | `string` | 唯一标识，建议前缀格式 `HiAuRo.xxx` |
| `url` | `string` | 完整 URL，如 `http://localhost:5678/main.html` |
| `width` | `int` | 窗口宽度 (px) |
| `height` | `int` | 窗口高度 (px) |
| `zoom` | `float` | 缩放百分比，默认 `100` |
| `locked` | `bool` | 是否锁定窗口位置（禁止用户拖动/调整大小） |

---

### 4. `Browsingway.Overlay.SetVisibility`

显示或隐藏 overlay。隐藏时保留 CEF 实例不销毁，再次显示无需重新加载页面。

| 项 | 值 |
|----|-----|
| 方向 | HiAuRo → Browsingway |
| 返回 | `void` |

**参数：**

| 参数 | 类型 | 说明 |
|------|------|------|
| `name` | `string` | overlay 名称 |
| `visible` | `bool` | `true`=显示，`false`=隐藏 |

---

### 5. `Browsingway.Overlay.SetPosition`

设置 overlay 窗口在屏幕上的位置。

| 项 | 值 |
|----|-----|
| 方向 | HiAuRo → Browsingway |
| 返回 | `void` |

**参数：**

| 参数 | 类型 | 说明 |
|------|------|------|
| `name` | `string` | overlay 名称 |
| `x` | `int` | 屏幕 X 坐标 |
| `y` | `int` | 屏幕 Y 坐标 |

---

## HiAuRo 的 Overlay 定义

| Name | URL | 默认尺寸 | Locked | 说明 |
|------|-----|---------|--------|------|
| `MainWindow` | `http://localhost:5678/main.html` | 310×480 | `true` | 主控面板 |
| `QtWindow` | `http://localhost:5678/qt.html` | 320×80 | `true` | QT 面板 |
| `HotkeyWindow` | `http://localhost:5678/hotkey.html` | 320×100 | `true` | 热键面板 |

## 调用时序

```
HiAuRo 启动
  │
  ├─ 轮询 Browsingway.IsReady() 直到返回 true
  │
  ├─ Browsingway.IsReady == true:
  │    Overlay.CreateOrUpdate("HiAuRo.MainWindow", url, 310, 480, 100, true)
  │    Overlay.CreateOrUpdate("HiAuRo.ActionPanel", url, 600, 180, 100, true)
  │    Overlay.SetPosition("HiAuRo.MainWindow", x, y)
  │    Overlay.SetPosition("HiAuRo.ActionPanel", x, y)
  │    Overlay.SetVisibility("HiAuRo.MainWindow", true)
  │    Overlay.SetVisibility("HiAuRo.ActionPanel", true)
  │
HiAuRo 卸载
  │
  └─ Overlay.SetVisibility("HiAuRo.MainWindow", false)
     Overlay.SetVisibility("HiAuRo.ActionPanel", false)
```

## HiAuRo → Browsingway IPC Provider 端点

HiAuRo 同时暴露以下端点，供 Browsingway 反向查询 WebUI 设置以同步状态：

| IPC 名称 | 签名 | 返回 | 说明 |
|----------|------|------|------|
| `HiAuRo.GetWebUiPort` | `Func<void, int>` | `int` | HTTP 服务器端口（5678 或 5679） |
| `HiAuRo.GetOverlaysJson` | `Func<void, string>` | `string` (JSON) | OverlayWindowSetting[] 的 CamelCase JSON |
| `HiAuRo.IsWebUIMode` | `Func<void, bool>` | `bool` | `true`=WebUI 模式（应显示覆盖层），`false`=ImGui 模式（应隐藏） |

### OverlayWindowSetting JSON 结构

```json
[
  {
    "name": "MainWindow",
    "url": "http://localhost:5678/main.html",
    "width": 310,
    "height": 480,
    "zoom": 100.0,
    "visible": true,
    "locked": true
  }
]
```

## 不需要的功能

以下 Browsingway 内部能力 HiAuRo **不需要** 通过 IPC 暴露：

- ❌ 鼠标/键盘事件转发（HiAuRo UI 需要正常交互，不穿透）
- ❌ CSS 注入（`InjectUserCss`）
- ❌ 静音控制（`Mute`）
- ❌ 帧率控制（`Framerate`）
- ❌ DevTools 调试（`Debug`）
- ❌ ClickThrough / TypeThrough 模式
- ❌ 战斗中自动隐藏 / PvP 隐藏 等条件逻辑
- ❌ 透明度控制
