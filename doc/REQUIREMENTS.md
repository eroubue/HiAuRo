# HiAuRo — 需求文档

## 概述

本文档定义 HiAuRo 的功能需求、非功能需求与验收标准。需求按 Phase 组织，Phase 1-5 为 MVP 范围。

## 全局交付规则

- 实现优先简单直接，不为了形式化增加层级
- 现有 API 足够时直接使用，不新增包装层
- 注释优先中文
- 进入事实轴之前，保持对 AE 的可参考性
- 新能力做加法式扩展，不做推翻式重构
- 总体实现尽量能直接参考 AE 的设计思路
- ACR 开发模式和暴露给作者的主要接口尽量保持不变
- 让已有 AE 的 ACR 作者可以快速迁移

---

## MVP 需求（Phase 1-5）

### Phase 1: 宿主层

| ID | 需求 | 验收标准 |
|----|------|----------|
| HOST-01 | 单项目 Dalamud 插件工程 | `dotnet build` 通过，生成可加载的 DLL |
| HOST-02 | 宿主正确执行初始化和释放 | Dalamud 加载/卸载插件无异常 |
| HOST-03 | 启动日志可确认插件状态 | 启动时输出 `[Lifecycle]` 日志 |

### Phase 2: 基础设施层

| ID | 需求 | 验收标准 |
|----|------|----------|
| INF-01 | 统一格式运行日志 | 开发者可通过日志定位插件状态 |
| INF-02 | 配置持久化 | 插件重启后配置不丢失 |
| INF-03 | 调试总开关 | 不修改业务代码即可开关调试输出 |

### Phase 3: 数据层

| ID | 需求 | 验收标准 |
|----|------|----------|
| DATA-01 | 统一暴露战斗数据入口 `HiAuRo.Data` | 上层通过 `Data.Self` / `Data.Target` / `Data.Party` / `Data.Objects` / `Data.Combat` 读取数据 |
| DATA-02 | 直读优先，必要处整理 | Self/Target/Combat 转发 OmenTools；Party/Objects 做一次扫描多视图 |
| DATA-03 | 关键字段来源可追踪 | 字段附近可见当前值来源（调试用） |
| DATA-04 | 数据就绪判断 | 用 OmenTools `GameState.IsLoggedIn`，未就绪安全返回空值 |
| DATA-05 | 职业快捷数据入口 | 按职业拆分 XXHelp.cs，为首要目标职业（BRD）提供运行时快捷读取 |

#### Phase 3 数据分区明细

**Self 分区**（转发 OmenTools `LocalPlayerState.*`）

| 字段 | 来源 | 说明 |
|------|------|------|
| 玩家对象 | `LocalPlayerState.Object` | IPlayerCharacter |
| 名字 | `LocalPlayerState.Name` | 角色名称 |
| 职业/等级 | `LocalPlayerState.ClassJob` / `CurrentLevel` | 当前职业与等级 |
| HP/MP | `LocalPlayerState.Object` | 实时属性 |
| 位置/朝向 | `LocalPlayerState.Object.Position` / `Rotation` | |
| 施法状态 | `DService.Condition.IsCasting()` | 是否在读条、读条技能 ID |
| 移动状态 | `LocalPlayerState.IsMoving` | |
| Buff/DOT | `LocalPlayerState.HasStatus()` | |

**Target 分区**（转发 OmenTools `TargetManager.*`）

| 字段 | 说明 |
|------|------|
| Target / FocusTarget / MouseOverTarget / SoftTarget / PreviousTarget | OmenTools 全部直接支持，可读可写 |

**Party 分区**（扫描 `DService.PartyList`，方法写在 Data.Party 里）

| 视图 | 说明 |
|------|------|
| All | 全部队员 |
| Alive / Dead | 存活/死亡 |
| Tanks / Healers / Dps | 按角色分类 |
| Nearby5y / Nearby10y / Nearby15y | 按距离分桶 |
| CastableParty | 可施法队友（排除自己与距离外） |
| CastableTanks / CastableHealers / CastableDps | 可施法角色分类 |
| CastableMainTanks | 开着盾姿的 T |
| CastableMelees / CastableRangeds | 近战/远程可施法队友 |
| CastableAlliesWithin20 / 25 / 30 | 大范围 AOE/治疗判定 |

**Objects 分区**（扫描 `DService.ObjectTable`，方法写在 Data.Objects 里）

| 视图 | 说明 |
|------|------|
| All | 全对象表 |
| Allies | 友方战斗对象 |
| Enemies | 敌方战斗对象（联合 ObjectKind + BattleNpcSubKind + OwnerId + BuddyList + IsTargetable） |
| Party | 小队成员对象 |
| Pets / Summons | 宠物与召唤物 |
| Environment | 场景/NPC 对象 |

**Combat 分区**（转发 OmenTools `GameState.*` + `DService.Condition`）

| 字段 | 来源 | 说明 |
|------|------|------|
| InCombat | `DService.Condition` | 是否在战斗中 |
| IsPvP | `GameState.IsInPVPArea` | 是否 PvP |
| IsInInstance | `GameState.IsInInstanceArea` | 是否在副本中 |
| TerritoryType | `GameState.TerritoryType` | 当前地图 ID |
| Map | `GameState.Map` | 当前 Map ID |
| ServerTime | `GameState.ServerTime` | 服务器时间 |
| DeltaTime | `GameState.DeltaTime` | 帧时间 |

**Job 数据**（XXHelp.cs，Phase 3 先做 BRDHelp.cs）

| 文件 | 说明 |
|------|------|
| `Data/Jobs/BRDHelp.cs` | 诗人职业快捷入口：歌曲状态、DoT 监控 Buff 关注点、读条关注点 |

### Phase 4: 运行时核心

| ID | 需求 | 验收标准 |
|----|------|----------|
| CORE-01 | 每帧 Tick 循环 | 通过 OmenTools `FrameworkManager.Reg()` 注册，ACR 逻辑每帧驱动 |
| CORE-02 | 战斗上下文状态 | 进战斗/脱战/切图状态正确切换 |
| CORE-03 | ACR 生命周期管理 | Init / Update / Dispose 在正确的时机调用 |
| CORE-04 | 模式切换骨架（预埋） | 预留执行轴/事实轴两种模式的互斥切换入口，MVP 阶段不接入 |
| CORE-05 | 基础调度能力 | 为节点推进、策略控制提供调度入口 |

### Phase 5: ACR 抽象 + BRD 打样（MVP 终点）

| ID | 需求 | 验收标准 |
|----|------|----------|
| ACR-01 | 职业执行器统一接口 | ACR 作者实现 `IRotationEntry.Build()` 即可接入 |
| ACR-02 | Slot Resolver 机制 | 支持 Gcd / OffGcd / Always 三种 SlotMode；Resolver.Check() 返回 int |
| ACR-03 | 接近 AE 的开发体验 | Rotation + SlotResolverData + SlotMode 等概念与 AE 一致 |
| ACR-04 | 起手爆发序列 | `IOpener` 接口，OpenerMgr 管理起手逻辑 |
| ACR-05 | 技能序列 | `ISlotSequence` 接口，支持组合按键/连续技能 |
| ACR-06 | 触发器系统 | `ITriggerAction`（触发时执行的动作）和 `ITriggerCond`（触发条件判断）接口定义 |
| ACR-07 | 触发器事件处理 | `IRotationEventHandler` 接口，处理战斗事件回调 |
| ACR-08 | 技能/BUFF 常量定义 | SpellsDefine / AurasDefine，常用技能和 BUFF ID 表（中文注释） |
| ACR-09 | GCD 工具 | GCDHelper：辅助判断 GCD 剩余时间、窗口状态 |
| ACR-10 | 热键/QT 系统 | 支持热键绑定和 QT（Quick Toggle）开关；HotkeyHelper 管理热键状态 |
| ACR-11 | `/hi` 命令行 | CommandMgr 注册命令，可开关 ACR、切换职业 |
| ACR-12 | 设置管理 | SettingMgr 统一管理全局设置和职业独立设置；配置持久化 |
| ACR-13 | 悬浮控制面板 | ImGui 主面板，显示开关、职业、运行状态 |
| ACR-14 | 职业悬浮窗 | JobViewWindow，职业技能提示 + QT 开关面板 |
| ACR-15 | BRD 打样实例 | 1 GCD + 1 oGCD 技能，验证 IOpener、ISlotSequence、触发器链路完整 |
| ACR-16 | 设置 UI | 全局设置页 + 职业设置页的 UI 框架 |

---

## 后续需求（Phase 6-9）

### Phase 6: 执行轴 ✅ 已完成

| ID | 需求 | 验收标准 | 状态 |
|----|------|----------|------|
| EXEC-01 | 条件驱动的执行控制 | 根据时间/事件/状态触发节点推进 | ✅ |
| EXEC-02 | 可切换 ACR 行为 | 执行轴可控制 ACR 的 AOE/单体/停手等模式 | ✅ |
| EXEC-03 | 可指定技能 | 执行轴可强制 ACR 使用特定技能 | ✅ |
| EXEC-04 | 节点调试能力 | 可查看当前节点、为何触发/未触发 | ✅ |

### Phase 7: 事实轴 ✅ 已完成

| ID | 需求 | 验收标准 | 状态 |
|----|------|----------|------|
| FACT-01 | JSON 定义 Boss 技能时间线 | 节点含技能 ID、时间偏移、同步条件、分支 | ✅ |
| FACT-02 | 事件同步与校准 | 根据实际战斗事件对齐时间线 | ✅ |
| FACT-03 | 分支切换 | 根据 Boss 状态选择不同分支 | ✅ |
| FACT-04 | 输出"战斗打到哪了" | 不直接指定按键，输出当前战斗事实状态 | ✅ |

### Phase 8: 智能决策层 ✅ 已完成

| ID | 需求 | 验收标准 | 状态 |
|----|------|----------|------|
| AI-01 | 消费事实轴 + 运行时状态 | 读取事实轴位置、团队资源、自定义约束 | ✅ |
| AI-02 | 输出策略开关 | 统一接口控制各职业 ACR 的输出策略 | ✅ |
| AI-03 | 减伤/治疗明确指定 | 不模糊倾向，输出确切的减伤/治疗指令 | ✅ |

### Phase 9: 创作与表现层 ✅ 已完成

| ID | 需求 | 验收标准 | 状态 |
|----|------|----------|------|
| UX-01 | 可视化事实轴编辑器 | 编辑事实轴并导出 JSON | ✅ 已完成 (纯前端 File System Access API) |
| UX-02 | 调试与复盘界面 | 查看时间线漂移、节点状态、修正建议 | ✅ 已完成 (纯前端 File System Access API) |
| UX-03 | 调优建议（非自动写入） | 以建议形式输出，人工确认 | ✅ 已完成 (纯前端 File System Access API) |

---

## v2 需求（远期）

| ID | 需求 | 说明 |
|----|------|------|
| COOP-01 | 团队协同视图 | 完整的团队状态视图与跨职业策略面板 |
| AI-04 | 自适应兜底策略 | 主链路稳定后，评估偏离预案后的智能兜底 |

---

## 非功能需求

| ID | 需求 | 说明 |
|----|------|------|
| NF-01 | 性能 | 每帧数据处理 < 1ms，不明显影响游戏帧率 |
| NF-02 | 稳定性 | 插件崩溃不影响游戏本身 |
| NF-03 | 兼容性 | 跟随 Dalamud API 版本更新 |
| NF-04 | 可调试性 | 关键路径有可开关的调试日志 |
| NF-05 | 可移植性 | ACR 作者从 AE 迁移学习成本低 |

---

## 不做事项

| 项目 | 原因 |
|------|------|
| 完整职业循环实现 | ACR 作者负责，HiAuRo 只做框架 |
| AE 运行时依赖 | HiAuRo 是独立插件 |
| 自动走位/跑位 | 空间信息只提供点位 |
| 执行轴+事实轴同时运行 | 模式互斥 |
| 手写 DSL 维护事实轴 | JSON + 编辑器是长期方案 |
| 自动改写攻略 | 人工确认优先 |

---

## 需求追溯表

| ID | Phase | 说明 |
|----|-------|------|
| HOST-01 | Phase 1 | 单项目 Dalamud 插件工程 ✅ |
| HOST-02 | Phase 1 | 初始化和释放 ✅ |
| HOST-03 | Phase 1 | 启动日志 ✅ |
| INF-01 | Phase 2 | 运行日志 ✅ |
| INF-02 | Phase 2 | 配置持久化 ✅ |
| INF-03 | Phase 2 | 调试总开关 ✅ |
| DATA-01 | Phase 3 | 数据统一入口 ✅ |
| DATA-02 | Phase 3 | 直读优先 ✅ |
| DATA-03 | Phase 3 | 字段来源追踪 ✅ |
| DATA-04 | Phase 3 | 就绪判断 ✅ |
| DATA-05 | Phase 3 | XXHelp.cs 职业快捷入口 ✅ |
| CORE-01 | Phase 4 | Tick 循环 ✅ |
| CORE-02 | Phase 4 | 战斗上下文 ✅ |
| CORE-03 | Phase 4 | ACR 生命周期 ✅ |
| CORE-04 | Phase 4 | 模式切换骨架 ✅ |
| CORE-05 | Phase 4 | 调度能力 ✅ |
| ACR-01 | Phase 5 | 职业执行器接口 ✅ |
| ACR-02 | Phase 5 | Slot Resolver ✅ |
| ACR-03 | Phase 5 | AE 兼容体验 ✅ |
| ACR-04 | Phase 5 | 起手爆发（IOpener）✅ |
| ACR-05 | Phase 5 | 技能序列（ISlotSequence）✅ |
| ACR-06 | Phase 5 | 触发器系统 ✅ |
| ACR-07 | Phase 5 | 事件处理 ✅ |
| ACR-08 | Phase 5 | 技能/BUFF 常量 ✅ |
| ACR-09 | Phase 5 | GCD 工具 ✅ |
| ACR-10 | Phase 5 | 热键/QT 系统 ✅ |
| ACR-11 | Phase 5 | 命令系统 ✅ |
| ACR-12 | Phase 5 | 设置管理 ✅ |
| ACR-13 | Phase 5 | 悬浮主面板 ✅ |
| ACR-14 | Phase 5 | 职业悬浮窗 ✅ |
| ACR-15 | Phase 5 | BRD 打样 ✅ |
| ACR-16 | Phase 5 | 设置 UI ✅ |
| HELPER-01 | Helper | 全21职业辅助库 | ✅ (HiAuRo.Helper 独立仓库) |
| EXEC-01~04 | Phase 6 | 执行轴 ✅ |
| FACT-01~04 | Phase 7 | 事实轴 ✅ |
| AI-01~03 | Phase 8 | 智能决策层 ✅ |
| UX-01~03 | Phase 9 | 创作与表现层 ✅ |

**v1 需求总数: 46 个** (不含 NF，含 NF 为 51)

**Phase 7-9: FACT-01~04 + AI-01~03 + UX-01~03 = 10 个需求, 全部完成 ✅**

---

## 版本计划

| 版本 | 范围 | 核心交付 | 状态 |
|------|------|----------|------|
| v0.1 | Phase 1-2 | 插件骨架 + 基础设施 | ✅ |
| **v1.0 (MVP)** | **Phase 3-5** | **框架完整可用：数据层 + 运行时 + ACR 抽象（含起手/序列/触发器/热键/命令/设置/UI）+ BRD 打样** | ✅ |
| v1.5 | Phase 6 | 执行轴上线：TriggerLine + 节点推进 + 调试 + 首批 Trigger/Action 实现 | ✅ |
| v2.0 | Phase 7-9 | 事实轴 + 智能决策 + 编辑器 | ✅ 已完成 |
| v2.1 | P0~P3 对齐 | SpellTargetLimit + TargetResolver + 触发器扩展 + JobApi 覆盖 | 规划中 |

---

*Last updated: 2026-05-08*
