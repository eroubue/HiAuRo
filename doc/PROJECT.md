# HiAuRo — 项目章程

## What This Is

`HiAuRo` 是一个面向 FFXIV 的 Dalamud 战斗辅助插件。它**不是一个职业循环实现**，而是一个**框架/平台**——提供运行时、数据层、ACR 接口和执行控制系统，让 ACR 作者在上面开发各职业的战斗逻辑。

默认提供接近 AEAssist 风格的开发体验，长期扩展为以"战斗事实轴 + 智能决策层 + 可视化编辑器"为核心的高级战斗辅助平台。

## Core Value

先以最平直、最接近 AE 习惯的方式做出一个独立可用的 HiAuRo，让 ACR 作者能快速上手；再在不推翻已有 ACR 开发体验的前提下，逐步长出事实轴等高级能力。

## Product Boundaries

### 做什么

| 层 | 职责 |
|---|------|
| **框架运行时** | 每帧 Tick 循环、战斗上下文状态、ACR 生命周期、模式切换（预埋） |
| **数据层** | `HiAuRo.Data` 统一入口（Self/Target/Party/Objects/Combat）+ XXHelp.cs 职业快捷入口 |
| **ACR 抽象** | ACR 作者接口（Rotation、SlotResolver、Opener、Sequence、TriggerAction/Cond、Hotkey/QT、命令、设置、CEF Web 悬浮窗） |
| **执行轴** | 条件驱动的执行控制层，时间/事件触发节点推进，控制 ACR 行为 |
| **事实轴**（远期） | Boss 技能时间线的结构化建模（JSON），事件同步校准与分支 |
| **智能决策层**（远期） | 消费事实轴 + 运行时状态 → 策略输出 + 减伤/治疗明确指定 |
| **创作工具**（远期） | 可视化事实轴编辑器、调试复盘界面、调优建议 |

### 不做什么

- **不实现完整职业循环** — 职业逻辑由 ACR 作者自行开发，HiAuRo 只提供一个打样实例（BRD）
- **不绑定 AEAssist 运行时依赖** — HiAuRo 是独立 Dalamud 插件
- **不做自动走位/跑位方案** — 空间信息只提供点位，策略由人工定义
- **不让执行轴和事实轴同时接管运行** — 两种模式边界互斥
- **不自动改写攻略方案** — 系统输出以建议优先
- **不在终极目标里优先追求"偏离预案后的自适应兜底"** — 先做稳定、可理解、可调试的主链路

## 双模式设计

### 默认模式：执行轴 → ACR（Phase 6 已实现）
```
执行轴（条件驱动） ──→ ACR（职业逻辑） ──→ 技能输出
```
- 接近 AE 体验
- 执行轴可按阶段/条件切换 ACR 行为、指定技能、暂停/恢复
- TriggerLine 支持时间/事件驱动 + 顺序/循环 + 延迟
- 通过 ModeSwitch.None ↔ ExecutionAxis 切换

### 高级模式：事实轴 → 智能层 → ACR
```
事实轴（Boss 时间线） ──→ 智能决策层 ──→ ACR ──→ 技能输出
```
- 战斗事实建模与个人执行彻底解耦
- 事实轴以 JSON 为权威产物

## Constraints

| 约束 | 说明 |
|------|------|
| **宿主形态** | 独立 Dalamud 插件，单项目（`.csproj`）起步 |
| **架构风格** | 平直、轻量、少包装、少跳转 |
| **注释语言** | 中文优先 |
| **推进顺序** | 严格自下而上，9 个 Phase |
| **AE 兼容** | 进入事实轴阶段前，ACR 开发体验尽量贴近 AE；ACR 作者主接口尽量不变；新能力做 additive 扩展 |
| **事实轴产物** | 结构化 JSON（非手写 DSL） |
| **新增能力** | additive 方式叠加，不做推翻式重构 |
| **不能变重框架** | 不提前为未来扩层加多余抽象；不演化成笨重的中间仓库层 |

## MVP 定义

**MVP 范围 = Phase 1 ~ Phase 5 完成**

| Phase | 内容 | 状态 |
|-------|------|------|
| Phase 1 | 宿主层（Plugin 生命周期、csproj、OmenTools 接入） | ✅ 已完成 |
| Phase 2 | 基础设施层（配置、调试开关、分类日志） | ✅ 已完成 |
| Phase 3 | 数据层（HiAuRo.Data：Self/Target/Party/Objects/Combat + BRDHelp.cs） | ✅ 已完成 |
| Phase 4 | 运行时核心（Tick 循环、战斗上下文、ACR 生命周期、模式切换预埋） | ✅ 已完成 |
| Phase 5 | ACR 抽象（SlotResolver/Opener/Sequence/Trigger/Hotkey/Command/Setting/UI）+ BRD 打样 | ✅ 已完成 |
| Phase 6 | 执行轴（ExecutionAxis + TriggerCond ×5 + TriggerAction ×4 + 节点推进 + 调试） | ✅ 已完成 |

**MVP 交付物 ✅**: 一个独立可加载的 Dalamud 插件，ACR 作者可以基于 HiAuRo 框架开发职业循环。包含完整的 Rotation/SlotResolver/Opener/Sequence/Trigger/Hotkey/QT/命令/设置/UI/执行轴体系，附带 BRD 实例验证全部链路。

## Key Decisions

| 决策 | 理由 |
|------|------|
| 独立 Dalamud 插件起步 | 先验证宿主可行性，不依赖 AE 运行时 |
| 单项目结构（不建 .sln） | 保持宿主层最小复杂度，避免提前进入多项目组织 |
| .NET 10 + Dalamud.NET.Sdk 15.0 | 当前 Dalamud 标准栈 |
| OmenTools 作为工具库接入 | DService 统一管理 Dalamud 服务；ImGuiOm 提供 UI 组件；不包额外壳层 |
| HiAuRo.Data 做公开入口，DService 允许直接使用 | 项目入口清晰 + AE 开发者手感熟悉双重要求 |
| MVP 不含执行轴 → 已于 Phase 6 实现 | 先把框架和数据链路全部跑通 |
| ACR 接口走 AE 风格 | Rotation + SlotResolverData + SlotMode 等概念一致，降低迁移成本 |
| BRD 打样 | 诗人 1 GCD + 1 oGCD + Opener，最小验证框架能力 |
| 执行轴用 NodeProgressor + TriggerLine | 扁平条目列表（Cond + Action），按序推进，支持 Loop 和 Delay |
| ModeSwitch 双模式（None/ExecutionAxis） | 无轴模式保持向后兼容，执行轴做 additive 扩展 |
| 先手写 JSON 验证事实轴可行性 | 编辑器是远期产物 |
| HiAuRo.json 使用真实元信息 | Author=嗨呀www、中文描述，RepoUrl 暂省略 |

---

*Last updated: 2026-05-04*
