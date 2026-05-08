# Phase 5.3: Web UI 层（CEF + WebSocket + HTML）

## 目标

用 HTML/CSS/JS + WebSocket 替代 ImGui，搭建热键、命令、设置和 Web 前端。CEF 嵌入游戏内渲染悬浮窗，复用于后续 Phase 9 时间轴编辑器。

**父阶段**: Phase 5
**依赖**: Phase 5.1
**需求**: ACR-10, ACR-11, ACR-12, ACR-13, ACR-14, ACR-16
**后续复用**: Phase 9 事实轴编辑器使用相同的 CEF + WebSocket 基础设施

## 实现原则

- CEF 渲染管线复用 Browsingway 的设计：独立渲染进程 + D3D11 共享纹理 + SharedMemory IPC
- Web 前端用原生 HTML/CSS/JS，不引入任何框架（保持轻量）
- Kestrel HTTP 服务器托管静态文件 + WebSocket 实时推送
- WebSocket 数据格式：JSON
- CEF 渲染帧率按需降低（10fps，控制面板不需要高帧率）
- WebSocket 只在数据变化时推送，不每帧盲推
- CEF 二进制直接打包进插件（离线可用）
- 开发调试时浏览器直接打开 `localhost:5678/mian` 即可预览，无需开游戏

## 架构总览

```
┌─────────────────────────────────────────────┐
│ HiAuRo (Dalamud Plugin)                     │
│                                             │
│  ┌───────────┐   ┌──────────────────────┐   │
│  │ Kestrel    │◄──│ WebSocket (JSON)      │   │
│  │ HTTP Server│   │ 推送: Data/ACR 状态   │   │
│  │ :5678      │──►│ 接收: 面板操作/设置   │   │
│  └───────────┘   └──────────┬───────────┘   │
│                             │               │
│  ┌──────────────┐  ┌────────▼──────────┐    │
│  │ CefManager   │  │ OverlayWindow     │    │
│  │ (CEF 启动)   │  │ (ImGui 画 CEF 纹理)│   │
│  └──────┬───────┘  └────────┬──────────┘    │
│         │                   │               │
│  ┌──────▼───────────────────▼──────────┐    │
│  │ SharedMemory IPC (FlatBuffers)      │    │
│  └──────────────────┬─────────────────┘    │
└─────────────────────│──────────────────────┘
                      │
┌─────────────────────▼──────────────────────┐
│ HiAuRo.Renderer.exe (独立进程)              │
│                                             │
│  ┌────────────────────────────────────┐    │
│  │ HiAuRoRenderHandler                │    │
│  │ (CefSharp.IRenderHandler)          │    │
│  │ CEF BGRA → D3D11 Shared Texture    │    │
│  └────────────────────────────────────┘    │
└─────────────────────────────────────────────┘
```

## 文件清单

### 插件侧（HiAuRo.UI/）
| 操作 | 文件 | 说明 |
|------|------|------|
| 新建 | `UI/CefManager.cs` | CEF 生命周期管理（DependencyManager + RenderProcess 启动/停止） |
| 新建 | `UI/RenderProcess.cs` | 启动/管理外部渲染器进程 |
| 新建 | `UI/SharedTextureHandler.cs` | 打开 D3D11 共享纹理 → ImGui Image |
| 新建 | `UI/WndProcHandler.cs` | 键盘输入 Hook → FlatBuffers IPC |
| 新建 | `UI/OverlayWindow.cs` | 极简 ImGui 窗口：只画 CEF 共享纹理 + 转发鼠标事件 |
| 新建 | `UI/WebUiServer.cs` | Kestrel HTTP + WebSocket 服务器 |
| 新建 | `UI/WebUiBridge.cs` | C# ↔ JS 消息路由（JSON 序列化/分发） |
| 新建 | `UI/IconServer.cs` | 游戏内图标代理（ITextureProvider → PNG → HTTP） |
| 新建 | `UI/UiBuilderImpl.cs` | IUiBuilder 实现（收集控件定义 → List\<UiControlDef\>） |
| 新建 | `UI/UiControlDef.cs` | 控件定义数据模型（id/type/label/value/options） |

### 前端（HiAuRo.UI/web/）
| 操作 | 文件 | 说明 |
|------|------|------|
| 新建 | `UI/web/index.html` | 主面板：ACR 启停/职业/状态/命令入口 |
| 新建 | `UI/web/jobview.html` | 职业悬浮窗：当前技能/GCD/QT 开关 |
| 新建 | `UI/web/settings.html` | 设置页：全局设置 + 职业设置 |
| 新建 | `UI/web/app.js` | 前端入口：WebSocket 客户端 + UI 逻辑 |
| 新建 | `UI/web/style.css` | 全局样式 |

### 渲染器进程（HiAuRo.Renderer/）
| 操作 | 文件 | 说明 |
|------|------|------|
| 新建 | `Renderer/Program.cs` | 渲染进程入口（接收 RenderParams CLI 参数） |
| 新建 | `Renderer/HiAuRoRenderHandler.cs` | CefSharp IRenderHandler：CEF 输出 → D3D11 共享纹理 |
| 新建 | `Renderer/CefBootstrap.cs` | CEF 初始化设置 |
| 新建 | `Renderer/DxHandler.cs` | D3D11 设备创建（匹配游戏 GPU 适配器） |
| 新建 | `Renderer/IpcHandler.cs` | 接收插件侧 IPC 消息（调整大小、输入转发） |

### IPC 共享（HiAuRo.Common/）
| 操作 | 文件 | 说明 |
|------|------|------|
| 新建 | `Common/IpcBase.cs` | SharedMemory 环形缓冲 IPC 基类 |
| 新建 | `Common/IpcMessages.cs` | IPC 消息定义（调整大小/鼠标/键盘/纹理更新） |

### 后端逻辑（ACR/Command/Setting/）
| 操作 | 文件 | 说明 |
|------|------|------|
| 新建 | `ACR/IHotkeyEventHandler.cs` | 已移至 5.3 创建 |
| 新建 | `ACR/HotkeyConfig.cs` | 热键配置数据结构 |
| 新建 | `ACR/HotkeyHelper.cs` | 热键绑定+解析 |
| 新建 | `ACR/IHotkeyResolver.cs` | QT 面板热键解析器 |
| 新建 | `Command/CommandMgr.cs` | /hi 命令系统 |
| 新建 | `Setting/SettingMgr.cs` | 全局+职业设置管理 |
| 新建 | `ACR/AcrType.cs` | ACR 类型枚举（Both/PvE/PvP） |

### UI 控件转换流程

ACR 作者的 C# 描述 → HiAuRo 转 JSON → Web 前端渲染 HTML：

```
ACR 作者 (C#):                              HiAuRo (JSON):                    前端 (HTML):
builder.AddCheckbox("AOE","启用AOE",false) → {"id":"AOE","type":"checkbox",   → <label>启用AOE</label>
                                              "label":"启用AOE","value":false}   <input type="checkbox">
builder.AddHotkey("Pot","爆发药","F1")      → {"id":"Pot","type":"hotkey",     → <label>爆发药</label>
                                              "label":"爆发药","key":"F1"}       <button>F1</button>
builder.AddDropdown("Song","起手歌曲",       → {"id":"Song","type":"dropdown",  → <label>起手歌曲</label>
  ["贤者","军神","放浪"],"贤者")              "label":"起手歌曲",                 <select>...</select>
                                              "options":["贤者","军神","放浪"],
                                              "value":"贤者"}
```

**流程**:
1. ACR 加载 → AIRunner 调用 `Rotation.RotationUI.RegisterControls(builder)`
2. `UiBuilderImpl`（IUiBuilder 的实现）收集所有控件定义为 `List<UiControlDef>`
3. `WebUiBridge` 序列化为 JSON → WebSocket 推送给前端
4. 前端 JS 根据 `type` 字段动态生成 HTML 控件
5. 用户操作控件 → JS 发 WebSocket `{type:"settingChanged",id:"AOE",value:true}`
6. `WebUiBridge` → `SettingMgr` 保存 → 回调 ACR 逻辑

## 任务

### Task 1: CEF 基础设施（渲染进程 + 共享纹理 + 输入转发）

**操作**:
1. 新建 `UI/CefManager.cs`
   - `Init()`: 检查 CEF DLL 是否存在 → 启动 Renderer.exe 子进程
   - `Shutdown()`: 关闭渲染进程
   - 自动重连：进程崩溃后自动重启
2. 新建 `UI/RenderProcess.cs`
   - 启动 `HiAuRo.Renderer.exe`，传入 CLI 参数（父 PID、IPC 通道名、GPU 适配器 LUID）
   - 通过 DService 获取游戏 D3D11 设备，查询 DXGI 适配器 LUID
3. 新建 `UI/SharedTextureHandler.cs`
   - 接收 IPC 发来的 D3D11 共享纹理句柄（IntPtr）
   - 调用 `device.OpenSharedResource()` → `ID3D11ShaderResourceView*`
   - 包装为 ImGui ImTextureID
4. 新建 `UI/WndProcHandler.cs`
   - Hook 游戏窗口 WndProc，截获 WM_KEYDOWN/UP/CHAR
   - 当 OverlayWindow 获得焦点时，转发键盘消息到渲染进程（IPC FlatBuffers `KeyEventMessage`）
5. 新建 `UI/OverlayWindow.cs`
   - ImGui 窗口（通过 DService.UiBuilder 注册）
   - Draw: 调用 `SharedTextureHandler.GetTexture()` → `ImGui.Image()`
   - 鼠标：捕获窗口内鼠标坐标/点击/滚轮 → IPC `MouseButtonMessage`
   - 极简实现：只画纹理，无其他 ImGui 控件
6. 新建 `Renderer/Program.cs` — 渲染进程入口
   - 解析 CLI → `RenderParams`（FlatBuffers）
   - `AssemblyResolve` 加载 CefSharp DLL
   - 初始化 IPC、DxHandler、CefBootstrap
   - 创建 CefSharp `ChromiumWebBrowser`（offscreen）
7. 新建 `Renderer/HiAuRoRenderHandler.cs`
   - 实现 `CefSharp.IRenderHandler`
   - `OnPaint()`: CEF BGRA 缓冲 → `UpdateSubresource()` → D3D11 纹理
   - `GetViewRect()`: 返回浏览器窗口尺寸
   - 创建共享纹理（`D3D11_RESOURCE_MISC_SHARED`），发送句柄到插件侧
8. 新建 `Common/IpcBase.cs` + `Common/IpcMessages.cs`
   - SharedMemory 环形缓冲（参考 Browsingway.Common）
   - IPC 消息类型：`UpdateTextureMessage`, `MouseButtonMessage`, `KeyEventMessage`, `ResizeOverlayMessage`

**验证**: `dotnet build` 通过；渲染进程可启动；共享纹理可被 ImGui 绘制

---

### Task 2: Kestrel HTTP + WebSocket 服务器

**操作**:
1. 新建 `UI/WebUiServer.cs`
   - 使用 ASP.NET Core Kestrel（`Microsoft.AspNetCore.App` 内置）
   - 监听 `http://localhost:5678`
   - 静态文件托管：`UI/web/` → `/`
    - 路由：
      - `GET /` → index.html（主面板）
      - `GET /jobview` → jobview.html（职业悬浮窗）
      - `GET /settings` → settings.html（设置页）
      - `GET /acr/{acrName}/settings` → ACR 自定义设置页（`{settingFolder}/settings.html`）
      - `GET /acr/{acrName}/jobview` → ACR 自定义悬浮窗（`{settingFolder}/jobview.html`）
      - `GET /api/status` → JSON（当前 ACR 状态快照）
      - `GET /api/settings` → JSON（当前设置）
      - `GET /api/icon/{iconId}?size=32` → PNG 图片（游戏内图标代理）
    - WebSocket 端点：`ws://localhost:5678/ws`
2. 新建 `UI/IconServer.cs` — 游戏图标代理
   - 浏览器无法直接调用 Dalamud 的 `ITextureProvider`，需要后端代理
   - `GetIcon(uint iconId, int size)`:
     1. 调用 `DService.Texture.GetIcon(iconId)` → 纹理
     2. 缩放到目标尺寸
     3. 编码为 PNG byte[]
     4. 返回 HTTP Response（Content-Type: image/png, Cache-Control: max-age=86400）
   - 支持图标类型：技能图标、BUFF/DOT 图标、职业图标、道具图标
   - 前端用法：`<img src="http://localhost:5678/api/icon/97?size=32">`（强力射击图标）
   - 状态推送中图标字段传 URL 而非原始数据：
     ```json
     { "type": "status", "data": { "currentSpell": { "id": 97, "name": "强力射击", "icon": "/api/icon/97?size=48" } } }
      ```
3. 新建 `UI/WebUiBridge.cs`

**自定义 HTML 模式**（ACR 作者 `UseCustomUi = true`）:
- ACR 作者在 DLL 同目录下放置 `settings.html` / `jobview.html`
- HiAuRo Kestrel 直接提供这些文件：`GET /acr/{acrName}/settings`
- 自定义 HTML 通过同一个 WebSocket `ws://localhost:5678/ws` 通信
- 示例 — ACR 作者的 `BRD/settings.html`:
  ```html
  <script>
    const ws = new WebSocket('ws://localhost:5678/ws');
    ws.onmessage = e => { const m = JSON.parse(e.data); /* 处理状态推送 */ };
    function save(id, val) { ws.send(JSON.stringify({type:"settingChanged",id,value:val})); }
  </script>
  <button onclick="save('AOE', true)">启用AOE</button>
  ```

4. 新建 `UI/WebUiBridge.cs`
   - WebSocket 消息路由：
     - **C# → JS 推送**:
       - `{ type: "status", data: { job, enabled, inCombat, currentSpell, gcdRemaining } }`（状态变化时推送）
       - `{ type: "settings", data: { ... } }`（设置变更时推送）
     - **JS → C# 接收**:
       - `{ type: "toggleACR" }` → AIRunner 启停
       - `{ type: "toggleQT", key: "AOE" }` → QT 开关切换
       - `{ type: "saveSetting", path: "Global.AttackRange", value: 3.0 }` → SettingMgr 保存
       - `{ type: "hotkey", key: "F1" }` → HotkeyHelper 触发
   - JSON 序列化/反序列化
   - 推送优化：只在状态值变化时才推送（不每帧盲推）

**验证**: 
1. `dotnet build` 通过
2. 浏览器打开 `localhost:5678` 能看到主面板 HTML
3. `localhost:5678/api/icon/97?size=32` 返回 PNG 图片
4. WebSocket 连接成功，`{type:"status"}` 消息可收发

---

### Task 3: Web 前端（HTML/CSS/JS）

**操作**:
1. 新建 `UI/web/index.html` — 主面板
   - 显示：当前职业名、ACR 运行状态（运行/暂停/关闭）
   - 按钮：启用/禁用 ACR、打开设置页
   - WebSocket 连接状态指示器
2. 新建 `UI/web/jobview.html` — 职业悬浮窗
   - 显示：当前执行技能名、GCD 倒计时条
   - QT 开关列表（动态生成，通过 WebSocket 获取可用 QT 列表）
3. 新建 `UI/web/settings.html` — 设置页
   - 全局设置表单：ActionQueueInMs、MaxAbilityTimesInGcd、OptimizeGcd、AttackRange、AoeCount
   - 职业设置区域（动态加载当前职业设置）
   - 保存按钮 → WebSocket 发送 `saveSetting`
4. 新建 `UI/web/app.js` — 前端逻辑
   ```javascript
   // WebSocket 客户端
   const ws = new WebSocket('ws://localhost:5678/ws');
   ws.onmessage = (e) => {
       const msg = JSON.parse(e.data);
       switch(msg.type) {
           case 'status': updatePanel(msg.data); break;
           case 'settings': updateSettings(msg.data); break;
       }
   };
   // 发送指令
   function send(type, data) { ws.send(JSON.stringify({type, ...data})); }
   ```
5. 新建 `UI/web/style.css` — 全局样式
   - 暗色主题（匹配 FFXIV 风格）
   - 响应式布局（适配小悬浮窗和大设置页）

**验证**:
1. 浏览器打开 `localhost:5678`，页面加载正常
2. 修改 HTML/JS/CSS → 刷新浏览器即时生效（热更新）
3. WebSocket 通信正常

---

### Task 4: 热键 + 命令 + 设置 + UI 控件转换（后端集成）

与 WebSocket 无关的纯后端逻辑：

1. `ACR/HotkeyHelper.cs` — 保持原有设计不变
2. `Command/CommandMgr.cs` — `/hi on|off|toggle|status|debug` 不变
3. `Setting/SettingMgr.cs` — 全局 + 职业设置读写不变，暴露 API 供 WebUiBridge 调用
   ```csharp
   SettingMgr:
     T GetSetting<T>() where T : class, new()
     T GetJobSetting<T>(string job) where T : class, new()
     void Save()
     string ConfigDirectory { get; }
   ```
4. 新建 `UI/UiControlDef.cs` — 控件定义数据模型
   ```csharp
   public record UiControlDef(
       string Id, string Type, string? ParentId,
       string Label, object Value,
       object? Options = null,     // Dropdown 选项 / Slider [min,max]
       object? Meta = null         // IntInput: {step,stepFast} / Tooltip: {targetId,text}
   );
   ```
   **支持的类型**: tab, group, checkbox, slider, dropdown, hotkey, intInput, label, separator, sameLine, tooltip
5. 新建 `UI/UiBuilderImpl.cs` — IUiBuilder 实现
   - 实现全部 11 个方法（Group/Separator/SameLine/Checkbox/Slider/Dropdown/Hotkey/IntInput/Label/Tooltip）
   - 支持嵌套：`AddGroup()` 后所有控件归属该分组，直到下一个 `AddGroup()` 或 `EndGroup()`
   - `GetControls()` 返回 `List<UiControlDef>`
6. WebUiBridge 中增加 `PushUiDefinition(List<UiControlDef> controls)` → WebSocket `{type:"uiDefinition", controls:[...]}`
7. WebUiBridge 中增加 `HandleSettingChanged(string id, object value)` → SettingMgr 保存

---

### Task 5: 全链路集成验证

**操作**:
1. CefManager 启动渲染进程
2. WebUiServer 启动 Kestrel
3. OverlayWindow 注册 ImGui 窗口
4. AIRunner 状态变化 → WebUiBridge 推送 JSON → JS 更新 UI
5. JS 点击"启用 ACR"按钮 → WebSocket → WebUiBridge → AIRunner
6. CEF 游戏内悬浮窗显示 web 页面内容
7. 键盘输入通过 WndProcHandler → IPC → CEF

**验证**:
1. 浏览器 `localhost:5678` 可操作 ACR（启停/设置/热键）
2. 游戏内 CEF 悬浮窗显示相同内容
3. 键盘输入可传递给 CEF（搜索框等控件可用）
4. CEF 进程崩溃后自动重启，不丢状态

---

## 阶段验证

- [ ] `dotnet build` 通过（主插件 + 渲染器进程）
- [ ] `localhost:5678` 浏览器可访问 Web UI
- [ ] WebSocket 收发正常
- [ ] 游戏内 CEF 悬浮窗可见
- [ ] 热键/命令/设置后端不变
- [ ] CEF 进程崩溃自动恢复
- [ ] CEF 渲染帧率可配置（默认 10fps）
- [ ] 内存占用（CEF 进程 + Kestrel）< 150MB

## 威胁模型

| 威胁 | 类别 | 处置 |
|------|------|------|
| CEF 进程崩溃 | D | 自动重启，不丢 AIRunner 状态 |
| WebSocket 断连 | D | JS 侧自动重连（指数退避） |
| WebSocket 消息泄露敏感数据 | I | 不推送账号/ContentID/密钥；只推送战斗状态 |
| CEF DLL 缺失 | D | 插件加载时检查，缺失则提示用户 |

## 进度

| Task | 状态 |
|------|------|
| Task 1: CEF 基础设施 | 已完成（骨架就位，待接入 NuGet 包） |
| Task 2: Kestrel HTTP + WebSocket | 已完成 |
| Task 3: Web 前端（HTML/CSS/JS） | 已完成 |
| Task 4: 热键 + 命令 + 设置（后端集成） | 已完成 |
| Task 5: 全链路集成验证 | 待游戏环境验证 |

---

*Created: 2026-05-03*
*架构参考: Browsingway (https://github.com/Styr1x/Browsingway)*
