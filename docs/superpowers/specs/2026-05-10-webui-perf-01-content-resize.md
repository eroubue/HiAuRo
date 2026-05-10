# WebUI 性能优化 — 方向 1: ImGui 窗口自适应 CEF 内容尺寸

## 问题

当前 ImGui overlay 窗口使用固定尺寸（MainWindow: 360×500, QtWindow: 320×80, HotkeyWindow: 300×160），
窗口尺寸和 CEF 渲染分辨率由 C# 端硬编码控制，与 HTML 内容实际所需尺寸无关。

## 目标

CEF 内容（HTML/CSS）的渲染结果决定 Overlay 窗口尺寸，窗口尺寸与内容匹配，无多余空白或裁切。

## 约束

- 仅在 overlay **锁定**状态下执行自适应；解锁后不做自适应，尊重用户手动调整
- 尺寸变化不是高频事件——只在加载、ACR 切换、Tab 切换、布局变化时触发
- 自适应过渡过程中允许 1-2 帧的纹理拉伸，「直接过渡」策略

## 架构

### 数据流

```
JS (app.js)
  │ 测量 document.documentElement.scrollWidth / scrollHeight
  │ 发送 WebSocket: { type: "contentResize", data: { overlay, width, height } }
  ▼
WebUiBridge
  │ 按 type 分发给注册的 handler
  ▼
Plugin.cs (contentResize handler)
  │ 检查 overlay 锁定状态 (通过 Browsingway/BrowserHost)
  │ 锁定 → 调用 _browserHost.UpdateOverlay(name, width, height)
  │ 解锁 → 忽略
  ▼
BrowserHost.UpdateOverlay()
  │ 1. 更新 _overlayConfig.Width / Height
  │ 2. 发送 ResizeOverlay(guid, width, height) 到 CEF 渲染器
  ▼
下一帧 Overlay.Render()
  │ ImGui.SetNextWindowSize(new Vector2(w, h))
  │ HandleWindowSize 检测新尺寸 (已有逻辑)
  ▼
CEF 以新尺寸 reflow + 渲染
  │ OnPaint → 共享纹理 → ImGui.Image()
```

### 消息格式

```json
{
  "type": "contentResize",
  "data": {
    "overlay": "MainWindow",
    "width": 252,
    "height": 320
  }
}
```

## JS 端实现

### 新增函数 (app.js)

```js
let _lastReportedSize = { width: 0, height: 0 };

function reportContentSize() {
  const w = document.documentElement.scrollWidth;
  const h = document.documentElement.scrollHeight;
  // 仅当变化超过阈值时才上报
  if (Math.abs(w - _lastReportedSize.width) < 5 &&
      Math.abs(h - _lastReportedSize.height) < 5) return;

  _lastReportedSize = { width: w, height: h };
  send('contentResize', {
    overlay: OVERLAY_NAME,
    width: Math.max(100, w),
    height: Math.max(50, h),
  });
}
```

### Overlay 名称常量

每个 HTML 文件定义自己的 overlay 名称：

| 文件 | 常量 |
|------|------|
| `main.html` | `const OVERLAY_NAME = 'MainWindow';` |
| `qt.html` | `const OVERLAY_NAME = 'QtWindow';` |
| `hotkey.html` | `const OVERLAY_NAME = 'HotkeyWindow';` |

### 调用时机

- `DOMContentLoaded` 后首次调用
- ACR 加载/切换，控件数据填充完毕后
- Tab 切换后（layout 可能变化）
- QT/热键数据变更导致 DOM 结构变化后

## C# 端实现

### Plugin.cs

```csharp
// 注册 contentResize 消息处理器
_bridge.On("contentResize", data => {
    if (data is null) return;
    var overlay = data.GetProperty("overlay").GetString();
    var width = data.GetProperty("width").GetInt32();
    var height = data.GetProperty("height").GetInt32();

    if (string.IsNullOrEmpty(overlay)) return;

    // 调用 BrowserHost 更新尺寸
    _browserHost?.UpdateOverlay(overlay, width: width, height: height);
});
```

### BrowserHost.UpdateOverlay 增强

```csharp
// 更新尺寸时同步修改 _overlayConfig
public void UpdateOverlay(string name, ...)
{
    if (!_overlayByName.TryGetValue(name, out var guid) ||
        !_overlays.TryGetValue(guid, out var overlay))
        return;

    if (width is not null && height is not null)
    {
        // 仅在锁定状态下执行自适应
        if (!overlay.IsLocked) return;

        overlay.Config.Width = width.Value;
        overlay.Config.Height = height.Value;
        _ = _renderProcess?.Rpc?.ResizeOverlay(guid, width.Value, height.Value);
    }
    // ... 其余逻辑不变
}
```

### Overlay 新增接口

```csharp
public bool IsLocked => _overlayConfig.Locked;
public InlayConfiguration Config => _overlayConfig;
```

## 修改清单

| 文件 | 类型 | 改动 |
|------|------|------|
| `HiAuRo/UI/web/app.js` | 新增 | `reportContentSize()` 函数 + 调用点 |
| `HiAuRo/UI/web/main.html` | 新增 | `const OVERLAY_NAME = 'MainWindow'` |
| `HiAuRo/UI/web/qt.html` | 新增 | `const OVERLAY_NAME = 'QtWindow'` |
| `HiAuRo/UI/web/hotkey.html` | 新增 | `const OVERLAY_NAME = 'HotkeyWindow'` |
| `HiAuRo/Plugin.cs` | 新增 | 注册 `contentResize` handler |
| `Browsingway/Plugin.cs` (BrowserHost) | 修改 | `UpdateOverlay()` 增加 config 更新 |
| `Browsingway/Overlay.cs` | 新增 | `IsLocked` / `Config` 属性 |

## 可能的陷阱

- **尺寸振荡**：JS 上报 → ImGui 调大 → CEF reflow → 内容可能在更大 viewport 下布局变化 → 再次上报。通过阈值（5px）和锁定状态判断（锁定后才响应）可防止无限循环
- **初始尺寸**：首次加载时使用现有固定尺寸，`DOMContentLoaded` 后首次上报调整。用户在首帧看到的可能是旧尺寸，1-2 帧后修正。不影响使用
- **CEF resize 时页面闪烁**：CEF 的 Resize 不会重新加载页面，DOM 和 JS 状态都保留，仅触发 reflow。无闪烁风险
