# HiAuRo — 技术栈

## 运行时

| 项目 | 版本 | 用途 |
|------|------|------|
| .NET | 10.0 | 运行时与 SDK |
| C# | 13.0 | 编程语言 |
| Dalamud.NET.Sdk | 15.0.0 | Dalamud 插件开发 SDK |

## 核心依赖

| 包 | 用途 | 接入时机 |
|----|------|----------|
| Dalamud.NET.Sdk 15.0.0 | 插件构建、Dalamud API | Phase 1 |
| OmenTools | DService 服务入口、ImGuiOm UI 组件、Managers 事件包装 | Phase 3 |
| CefSharp.OffScreen.NETCore | CEF 浏览器（offscreen 渲染） | Phase 5.3 |
| Microsoft.AspNetCore.App | Kestrel HTTP + WebSocket 服务器 | Phase 5.3 |
| SharedMemory | IPC 环形缓冲（插件 ↔ CEF 渲染进程） | Phase 5.3 |
| FlatSharp.Runtime | FlatBuffers 序列化（IPC 消息） | Phase 5.3 |
| FFXIVClientStructs | 游戏数据结构（通过 OmenTools 间接引用） | Phase 3 |

## CEF 使用边界

- **接入方式**: CEF 运行在独立外部进程（`HiAuRo.Renderer.exe`），不嵌入游戏进程
- **渲染方式**: CEF offscreen → D3D11 共享纹理 → ImGui Image（复用 Browsingway 架构）
- **IPC 通信**: SharedMemory + FlatBuffers（插件 ↔ 渲染进程）
- **Web UI 开发**: 浏览器直接打开 `localhost:5678`，不依赖游戏
- **CEF 打包**: CEF DLL 直接打包进插件（首次加载可用，离线无需下载）

## OmenTools 使用边界

- **接入方式**: `DService.Init(pluginInterface)` / `DService.Uninit()` 直接挂在 `Plugin.cs`
- **内部使用**: `DService.*` 作为数据层内部骨架
- **不创建** OmenTools 的额外包装层或 ServiceLocator
- **公开入口**: 上层推荐走 `HiAuRo.Data`，`DService` 允许在职业逻辑里直接使用但非推荐

## 外部依赖

| 包 | 用途 | 状态 |
|----|------|------|
| ImGui.NET | UI 渲染（浮窗控制面板） | Dalamud 内置（或通过 OmenTools ImGuiOm） |

## 开发工具

| 工具 | 用途 |
|------|------|
| dotnet CLI | 构建、还原依赖 |
| dotnet test | 单元测试 |
| XIVLauncher (dev mode) | 插件加载与调试 |
| Dalamud DevPlugins | 本地插件开发环境 |

## csproj 配置

```xml
<Project Sdk="Dalamud.NET.Sdk/15.0.0">
  <PropertyGroup>
    <AssemblyName>HiAuRo</AssemblyName>
    <RootNamespace>HiAuRo</RootNamespace>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Version>0.1.0</Version>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

## 项目结构

- 单项目（单 `.csproj`），不建 `.sln`
- `HiAuRo/` 目录下包含所有源码
- 按功能模块分子目录（Data / Runtime / ACR / Jobs / Execution / UI）
- 项目结构扩展：Execution/ 目录（Phase 6），包含 TriggerLine/NodeProgressor/ExecutionDebug + Triggers/
- 没有额外的 test project（MVP 阶段通过 in-game 验证）

## 约束

- 不在项目里引入 WinForms / WPF 依赖
- 不引入额外的 IoC 容器或 Service Locator 框架
- 配置持久化走 Dalamud 原生 `IPluginConfiguration` 路径
- 日志走 Dalamud 原生 `IPluginLog`

---

*Last updated: 2026-05-04*
