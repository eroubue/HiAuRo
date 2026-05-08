# Web UI 层

## 架构
```
C# (Dalamud) ←──── WebSocket ────→ Web 前端 (HTML/CSS/JS)
     │      WebUiBridge/WebUiServer      │
     │      localhost:5678 (或 5679)      │
     └────────────────────────────────────┘
```

## 组件

### WebUiServer
- `HttpListener` + Kestrel 替代，端口 5678（冲突时 5679）
- 静态文件服务 + WebSocket 升级 (/ws)
- 前端文件位于 `Plugin.ConfigDirectory/web/`（每次启动从 `UI/web/` 覆盖）

### WebUiBridge
- C# ↔ JS 双向消息路由
- `On(type, handler)` 注册 C# 端消息处理器
- `SendAsync(object)` 推送 JSON 到所有 WebSocket 客户端
- 连接时自动推送初始状态（职业/开关/暂停/CD/hotkey/QT）

### MainWindow
- Dalamud ImGui 窗口（内嵌 CEF 渲染的 Web UI）
- 端口冲突时自动切换到 5679

## 前端页面

| 文件 | 用途 |
|------|------|
| `main.html` | 控制面板主页 |
| `jobview.html` | 职业悬浮窗 |
| `hotkey.html` | 热键按钮面板 |
| `qt.html` | Quick Toggle 面板 |
| `preview.html` | 预览页 |
| `app.js` | JS 逻辑 |
| `style.css` | 样式 |

## C# → JS 消息类型
- `acrState` — 开关状态
- `pauseChanged` — 暂停状态
- `hotkeyExecuted` — 热键执行反馈
- `qtChanged` — QT 切换反馈
- `uiSettings` — UI 设置同步

## JS → C# 消息类型
- `toggleACR` — 开关 ACR
- `pause` — 暂停/恢复
- `saveACR` — 保存设置
- `hotkey` — 热键点击
- `qttoggle` — QT 切换
- `setHkBinding` — 热键绑定
- `saveUiSettings` — 保存 UI 设置
- `log` — 前端调试日志
