# Phase 2: Infrastructure Layer（基础设施层）— 开发任务

## 目标

提供日志分类约定、配置持久化和调试总开关。

**依赖**: Phase 1
**需求**: INF-01, INF-02, INF-03

## 实现原则

- 只补当前真实需要的能力，不为"以后可能会用到"扩出复杂中间层
- 配置直接走 Dalamud 原生 `IPluginConfiguration`，不额外建配置服务层
- 调试开关只做全局 on/off，不拆成多分类开关

## 文件清单

| 操作 | 文件 | 说明 |
|------|------|------|
| 新建 | `HiAuRo/Infrastructure/PluginConfig.cs` | 配置根对象 |
| 修改 | `HiAuRo/Plugin.cs` | 接入配置持久化 + 分类日志 |

## 任务

### Task 1: 建立主配置对象与持久化

**操作**:
1. 新建 `Infrastructure/PluginConfig.cs`，实现 `IPluginConfiguration`
2. 字段：`Version`(int, 初始=1)、`DebugEnabled`(bool)、`LastSeenPluginVersion`(string?)、`LoadCount`(int)
3. 在 `Plugin` 构造中：读配置 → 比较版本 → 更新 LoadCount → 保存配置
4. 输出 `[Config]` 日志打印 SchemaVersion / PluginVersion / LoadCount / DebugEnabled

**验证**: 输出 `[Config]` 日志可确认配置读写正常；重启插件后 LoadCount 递增

**完成**: 配置可持久化，重启不丢失

---

### Task 2: 分类日志与调试开关

**操作**:
1. 在 Plugin 中统一日志分类前缀：
   - `[Lifecycle]` — 生命周期事件（加载/释放）
   - `[Config]` — 配置读写
   - `[Debug]` — 受 DebugEnabled 控制的调试信息
2. 实现 `LogDebug(string messageTemplate, params object[] args)` 私有方法
3. DebugEnabled=false 时调试日志不输出

**验证**: DebugEnabled=false 时无 [Debug] 日志；设为 true 后可输出调试信息

**完成**: 插件有统一的日志分类约定；调试开关可控，不要求改业务代码入口

---

## 阶段验证

- [x] 配置重启后持久化
- [x] `[Lifecycle]` / `[Config]` / `[Debug]` 三类日志正常
- [x] DebugEnabled=false 时无 [Debug] 日志
- [x] 没有额外的 ConfigService / LoggerService / StorageService 层

## 威胁模型

| 威胁 | 类别 | 处置 |
|------|------|------|
| 调试日志输出敏感身份信息 | I | [Debug] 日志只记录状态和元信息，不输出 ContentID、角色名等 |
| 配置损坏导致插件异常 | D | 使用 `?? new PluginConfig()` 兜底；Version 字段用于迁移 |

---

## 进度

| Task | 状态 |
|------|------|
| Task 1: 配置与持久化 | 已完成 |
| Task 2: 日志与调试开关 | 已完成 |

---

*Created: 2026-05-03*
