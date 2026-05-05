# Phase 1: Host Layer（工程宿主层）— 开发任务

## 目标

建立独立 Dalamud 插件骨架，接入 OmenTools，打通加载/卸载生命周期。

**依赖**: 无（第一个 Phase）
**需求**: HOST-01, HOST-02, HOST-03

## 实现原则

- 以最小骨架起步，不预埋 MainWindow、ConfigWindow、命令处理器等
- 只保留必需文件：csproj / Plugin.cs / manifest / 最小构建配置
- OmenTools 生命周期直接挂 Plugin，不加初始化壳层

## 文件清单

| 操作 | 文件 | 说明 |
|------|------|------|
| 新建 | `HiAuRo/HiAuRo.csproj` | Dalumud.NET.Sdk 15.0.0，引用 OmenTools |
| 新建 | `HiAuRo/HiAuRo.json` | 插件 manifest（Author=嗨呀www） |
| 新建 | `HiAuRo/Plugin.cs` | DService.Init/Uninit + 生命周期日志 |

## 任务

### Task 1: 创建插件工程骨架

**操作**:
1. 新建 `HiAuRo.csproj`，使用 `<Project Sdk="Dalamud.NET.Sdk/15.0.0">`
2. 配置：AssemblyName=HiAuRo, RootNamespace=HiAuRo, Version=0.1.0, Nullable=enable, ImplicitUsings=enable
3. 添加 OmenTools 项目引用或 NuGet/SourceLink 引用
4. 新建 `HiAuRo.json`，配置 Name/Author/Punchline/Description

**验证**: `dotnet build HiAuRo/HiAuRo.csproj -nologo` 通过

**完成**: csproj 可正确 restore + build；manifest 元信息正确（Author=嗨呀www、中文描述）

---

### Task 2: 接通插件入口生命周期

**操作**:
1. 新建 `Plugin.cs`，实现 `IDalamudPlugin` 接口
2. 构造函数中调用 `DService.Init(pluginInterface)`，输出 `[Lifecycle] HiAuRo 宿主已加载。` 日志
3. `Dispose()` 中调用 `DService.Uninit()`，输出 `[Lifecycle] HiAuRo 宿主已释放。` 日志
4. 直接从 `IDalamudPluginInterface` 读取插件版本等基础信息
5. **不创建**额外的 Bootstrapper、ServiceLocator 或初始化壳层

**验证**:
1. `dotnet build HiAuRo/HiAuRo.csproj -nologo` 通过
2. 在 Dalamud dev plugin 环境加载插件，确认启动/释放日志正常输出

**完成**: Plugin 作为唯一宿主组合入口，OmenTools 生命周期已接通；源码中不存在额外初始化壳层

---

## 阶段验证

- [x] `dotnet build` 通过
- [ ] Dalamud dev plugin 加载成功
- [ ] 启动日志输出 `[Lifecycle] HiAuRo 宿主已加载。`
- [ ] 卸载日志输出 `[Lifecycle] HiAuRo 宿主已释放。`
- [x] 源码中没有额外的 Bootstrapper / ServiceLocator / PluginContext

## 威胁模型

| 威胁 | 类别 | 处置 |
|------|------|------|
| DService 重复初始化导致资源泄漏 | D | 只在构造和 Dispose 各调一次 Init/Uninit |
| 调试日志泄露敏感信息 | I | 只记录插件版本和生命周期，不输出对象表、ContentID 等 |

---

## 进度

| Task | 状态 |
|------|------|
| Task 1: 工程骨架 | 已完成 |
| Task 2: 生命周期 | 已完成 |

---

*Created: 2026-05-03*
