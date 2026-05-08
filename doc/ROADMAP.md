# HiAuRo — 路线图

## 总体路线

自下而上 9 个 Phase，严格按依赖顺序推进。Phase 1-5 组成 MVP。

```
Phase 1 ──→ Phase 2 ──→ Phase 3 ──→ Phase 4 ──→ Phase 5  ← MVP 终点
                                                 │
                                                 ↓
                                          Phase 6 ──→ Phase 7 ──→ Phase 8 ──→ Phase 9
                                                              ↘───────────────↗
                                                                       │
                                                                       ↓
                                                            HiAuRo.Helper (全职业辅助库)
```

## 全局规则

所有 Phase 遵守：

- 代码简单直接，不过度架构化
- 优先直接使用已有 API
- 非必要不增加包装层和跳转层
- 中文注释
- 不对假想未来需求提前铺设

进入事实轴（Phase 7）前，额外遵守：

- 整体实现尽量能直接参考 AE
- ACR 开发模式尽量不变
- 暴露给 ACR 作者的主要接口尽量不变
- 新能力以 additive 方式出现，不做推翻式重构
- 让已有 AE 的 ACR 作者可以快速迁移到 HiAuRo

---

## Phase 详情

### Phase 1: Host Layer（工程宿主层）

**目标**: 建立独立 Dalamud 插件骨架，打通加载/卸载生命周期。

**依赖**: 无
**需求**: HOST-01 ~ HOST-03
**实现原则**: 优先最简单直接的独立宿主骨架，不预埋重框架扩展层。

**交付**:
- `HiAuRo.csproj` — 单项目 Dalamud 插件工程，引用 OmenTools
- `HiAuRo.json` — 插件 manifest（Author=嗨呀www、中文描述）
- `Plugin.cs` — DService.Init/Uninit 生命周期 + 启动/释放日志

**完成标准**:
1. `dotnet build` 通过，生成可加载的 DLL
2. Dalamud dev plugin 能加载并输出启动日志

---

### Phase 2: Infrastructure Layer（基础设施层）

**目标**: 提供日志、配置、调试开关等基础设施。

**依赖**: Phase 1
**需求**: INF-01 ~ INF-03
**实现原则**: 基础设施只补当前需要的能力，不为了"以后可能会用到"扩出复杂中间层。

**交付**:
- `PluginConfig.cs` — 主配置对象，含 Version、DebugEnabled、LoadCount
- `[Lifecycle]` / `[Config]` / `[Debug]` 分类日志
- Dalamud 原生配置持久化路径

**完成标准**: 配置可持久化，调试开关可控制日志输出。

---

### Phase 3: Data Layer（游戏数据层） ← 下一步

**目标**: 整理游戏运行时数据，提供统一入口 `HiAuRo.Data`；为首要目标职业（BRD）提供职业快捷入口 XXHelp.cs。

**依赖**: Phase 2
**需求**: DATA-01 ~ DATA-05
**实现原则**: 以 API 直取和必要整理为主，不把数据层做成笨重的大型包装仓库。

**Plan 03-01: OmenTools 接入 + 数据就绪 + 分区骨架**
- 接入 OmenTools：`Plugin.cs` 中 `DService.Init(pluginInterface)` / `DService.Uninit()`
- 建立 `HiAuRo.Data` 根入口 + Self/Target/Combat 三个转发分区
- 就绪判断直接用 `GameState.IsLoggedIn`

**Plan 03-02: Party/Objects 分类视图**
- `Data.Party`：一次扫描 PartyList，输出角色分类（T/H/DPS）、距离分桶（5y/10y/15y）
- `Data.Objects`：一次扫描 ObjectTable，输出语义分类（Enemies/Allies/Pets/Environment）
- Enemies 分类联合 ObjectKind + BattleNpcSubKind + OwnerId + BuddyList + IsTargetable

**Plan 03-03: BRDHelp.cs 职业快捷入口**
- 为首要目标职业 BRD 建立 `Data/Jobs/BRDHelp.cs`
- 组合 Data.Self + Data.Target + Data.Party + Data.Combat + Svc.Gauges
- 暴露职业特有状态：歌曲、DoT、Buff 关注点等
- 风格：职业作者优先的短平快

**交付**:
- `Data/Data.cs` — 根入口
- `Data/Data.Self.cs` — 转发 LocalPlayerState.*
- `Data/Data.Target.cs` — 转发 TargetManager.*
- `Data/Data.Party.cs` — 队伍扫描 + 角色/距离分类
- `Data/Data.Objects.cs` — 对象扫描 + 语义分类
- `Data/Data.Combat.cs` — 转发 GameState.* + Condition.*
- `Data/Jobs/BRDHelp.cs` — 诗人职业快捷入口

**完成标准**:
1. 上层可统一从 `HiAuRo.Data` 读取所有战斗数据
2. Self/Target/Combat 是转发 OmenTools，Party/Objects 是一次扫描多视图
3. 未登录/切图时空值语义稳定
4. BRDHelp.cs 可正确读取诗人特有职业状态

---

### Phase 4: Runtime Core（运行时核心）

**目标**: 建立每帧 Tick 循环、战斗上下文状态和 ACR 生命周期管理；预埋模式切换骨架。

**依赖**: Phase 3
**需求**: CORE-01 ~ CORE-05
**实现原则**: 运行时骨架优先保证清晰和可调试，不为了抽象完整度引入额外跳转层。

**Plan 04-01: 定义运行时上下文、状态与生命周期边界**
- `RuntimeCore`：基于 `FrameworkManager.Reg()` 的主 Tick 循环
- `CombatContext`：进战斗/脱战/切图状态机
- `ACRLifecycle`：ACR 的 Init/Update/Dispose 调度

**Plan 04-02: 建立模式切换与互斥约束**
- 预留执行轴/事实轴两种模式的切换入口
- 明确两种模式互斥的约束（不同时接管运行）
- MVP 阶段不接入实际模式切换

**Plan 04-03: 建立基础调度入口**
- 为节点推进和策略控制提供稳定调度入口
- 后续执行轴/事实轴/智能层的挂载点

**交付**:
- `Runtime/RuntimeCore.cs` — 主 Tick 循环入口
- `Runtime/CombatContext.cs` — 战斗上下文状态机
- `Runtime/ACRLifecycle.cs` — ACR 生命周期
- `Runtime/ModeSwitch.cs` — 模式切换骨架（预埋）

**完成标准**:
1. ACR 逻辑挂在每帧循环执行
2. 进战斗激活 ACR、脱战停 ACR、切图不异常
3. 模式切换入口已预埋

---

### Phase 5: ACR Abstraction + BRD 打样 ← MVP 终点

**目标**: 定义接近 AE 风格的完整 ACR 框架；用 BRD 打样验证所有链路。

**依赖**: Phase 4
**需求**: ACR-01 ~ ACR-16
**实现原则**: 以 AE/ACR 作者低迁移成本为最高优先级，接口改动保持克制。

**Plan 05-01: 职业执行器统一接口与运行契约**
- `IRotationEntry` / `Rotation` / `SlotResolverData` / `SlotMode` / `ISlotResolver`
- SlotResolver.Check() 返回 int（正=可用，负=禁止，0=不关心）
- `SlotMode`：Gcd / OffGcd / Always

**Plan 05-02: 起手爆发 + 技能序列 + 触发器**
- `IOpener` + `OpenerMgr`：起手爆发序列管理
- `ISlotSequence`：组合按键/连续技能
- `ITriggerAction` / `ITriggerCond`：触发动作与条件接口定义
- `IRotationEventHandler`：战斗事件回调

**Plan 05-03: 技能常量 + GCD 工具 + 热键系统**
- `SpellsDefine.cs` / `AurasDefine.cs`：常用技能和 BUFF ID 表（中文注释）
- `GCDHelper`：GCD 剩余时间、窗口判断
- Hotkey 系统：热键绑定 + QT 开关 + `HotkeyHelper`

**Plan 05-04: CEF Web UI 层（热键 + 命令 + 设置 + UI）**
- CEF 渲染基础设施（Browsingway 源码改造为工具库 `BrowserHost`，独立渲染进程 + D3D11 共享纹理 + SharedMemory IPC）
- 输入转发：WndProc 键盘钩子 + ImGui 鼠标转发 + 像素级 Alpha 点击穿透
- Kestrel HTTP + WebSocket 服务器（`localhost:5678`）
- HTML/CSS/JS 前端（3 个独立页面：main.html 主控制栏、qt.html QT 开关、hotkey.html 热键按钮）
- HotkeyHelper + QTHelper + MainControlHelper + UiSettingsStore 后端
- Web↔C# 调试日志双向通道（console 劫持 + WebSocket log 消息）
- ImGui 「窗口设置」Tab（URL/宽/高/缩放/锁定 实时修改）
- 稳定 GUID（MD5 of name）确保 ImGui 窗口位置跨加载持久化
- 浏览器直接访问 `localhost:5678` 调试，游戏内 CEF 悬浮窗渲染
- **此 Web UI 基础设施后续 Phase 9 事实轴编辑器直接复用**

**Plan 05-05: BRD 打样 + 全链路验证**
- 诗人 1 GCD（强力射击）+ 1 oGCD（失血箭）
- 验证 IOpener、ISlotSequence、触发器链路完整
- 验证悬浮面板和设置 UI

**交付**:
- `ACR/IRotationEntry.cs` — 职业执行器接口
- `ACR/Rotation.cs` — Rotation 容器
- `ACR/SlotResolverData.cs` / `ACR/SlotMode.cs` / `ACR/ISlotResolver.cs`
- `ACR/IOpener.cs` / `ACR/OpenerMgr.cs` — 起手爆发
- `ACR/ISlotSequence.cs` — 技能序列接口
- `ACR/ITriggerAction.cs` / `ACR/ITriggerCond.cs` — 触发器接口
- `ACR/IRotationEventHandler.cs` — 事件处理接口
- `ACR/SpellsDefine.cs` / `ACR/AurasDefine.cs` — 技能/BUFF 常量
- `ACR/GCDHelper.cs` — GCD 工具
- `ACR/SpellHelper.cs` — 技能可用性/冷却/距离
- `ACR/TargetHelper.cs` — 目标选择/敌人数/身位
- `ACR/AuraHelper.cs` — Buff/DOT 检测
- `ACR/CooldownHelper.cs` — 充能技能冷却
- `ACR/HotkeyHelper.cs` — 热键管理
- `Command/CommandMgr.cs` — `/hi` 命令系统
- `Setting/SettingMgr.cs` — 设置管理
- `UI/MainWindow.cs` — ImGui 主面板（状态/设置/窗口设置/Debug 四个 Tab）
- `UI/web/main.html` — 主控制栏（停止/暂停/保存/展开）
- `UI/web/qt.html` — QT 开关悬浮面板
- `UI/web/hotkey.html` — 热键按钮悬浮面板
- `UI/web/app.js` — 共享 JS（WebSocket + UI 逻辑 + 事件绑定）
- `UI/web/style.css` — Apple iOS 设计系统
- `Browsingway/Browsingway/Plugin.cs` — → `BrowserHost` 工具类
- `Browsingway/Browsingway/Overlay.cs` — ImGui 窗口 + 纹理 + 鼠标/键盘转发
- `Browsingway/Browsingway/SharedTextureHandler.cs` — D3D11 共享纹理 → ImGui Image
- `Browsingway/Browsingway/WndProcHandler.cs` — SetWindowLongPtr 键盘钩子
- `Browsingway/Browsingway/DxHandler.cs` — D3D11 Device + HWND
- `Browsingway/Browsingway/InlayConfiguration.cs` — overlay 配置（精简 8 字段）
- `Browsingway/Browsingway/Services.cs` — Dalamud IoC 服务注册（精简 4 属性）
- `HiAuRo/Plugin_Browsingway.cs` — partial class 委托 BrowserHost
- `HiAuRo/Infrastructure/PluginConfig.cs` — +`OverlayWindowSetting[]` 窗口设置持久化
- `Jobs/BRD/BRD_GCD_强力射击.cs` / `Jobs/BRD/BRD_oGCD_失血箭.cs`
- `Jobs/BRD/BRDOpener.cs` — 诗人起手

**完成标准**:
1. ACR 作者实现 `IRotationEntry.Build()` 即可接入
2. SlotMode.Gcd / OffGcd 正常运作
3. 起手爆发、技能序列、热键/QT 系统正常工作
4. `/hi` 命令可启停 ACR、切换职业
5. 3 个 CEF 悬浮窗在游戏中渲染，点击交互正常
6. 网页前端可独立于游戏在浏览器开发和调试
7. ACR 设置独立持久化
8. 暂停阻断 Build（Check 继续），停止阻断全部技能（Check+轴检测继续）
9. BRD 打样在游戏中执行正确

---

### Phase 6: Execution Axis（执行轴）

**目标**: 落地条件驱动的执行控制层，形成默认模式完整链路。补齐 Phase 5 留空的触发器具体实现。

**依赖**: Phase 5
**需求**: EXEC-01 ~ EXEC-04

**Plan 06-01: 补齐触发器具体实现**
- 实现 Phase 5 定义的 `ITriggerCond` / `ITriggerAction` 接口
- **实际实现**（远超首批计划）：
  - TriggerCond 18 种：敌人读条、经过时间、技能后、Actor死亡、倒计时、倒计时开始、单位可选中、单位移除、地图特效、天气变化、技能冷却、收到技能效果、检查目标图标、游戏日志、等待目标、上次技能、连线、等待目标
  - TriggerAction 10 种：切换目标、释放技能、切换停手、吃药、技能队列、高优Slot、发送命令、发送按键、设置Rotation、锁定技能
- 覆盖了 AEAssist 30+ TriggerCond 和 20+ TriggerAction 的核心子集

**Plan 06-02: 定义执行轴节点结构与运行约定**
- 执行轴核心数据结构：
  - **TriggerLine（触发线）**：在战斗时间轴上按顺序检查触发器的执行线；支持循环（Loop）和单次（Once）
  - **并行/顺序节点**：TreeParallel（并行执行子节点）、TreeSequence（顺序执行子节点）
  - **条件分支节点**：TreeSelect（if/else 分支）
  - **控制流节点**：TreeLoop（循环）、TreeDelayNode（延时等待）
  - **叶子节点**：TreeCondNode（条件检查）、TreeActionNode（执行动作）、TreeScriptNode（执行 C# 脚本）、TreeClearTargetNode（清除目标）、TreeClearWaitNode（清除等待）
- 节点类型完整清单见 `AEASSIST_STUDY.md` 第 2.3 节「触发器 AST 节点类型」
**Plan 06-03: 建立节点推进与触发判定逻辑**
**Plan 06-04: 建立节点调试与诊断能力**

**交付**:
- `Execution/ExecutionAxis.cs` — 执行轴主逻辑（async Task + WaitCond TCS + ExecutionOutput）
- `Execution/ExecutionNode.cs` — 10 种 AST 节点（Sequence/Parallel/Select/Loop/Delay/Cond/Action/Script/Print/ClearWait）
- `Execution/ExecutionJson.cs` — AE 格式 JSON 反序列化 + 类型注册机制
- `Execution/ScriptCompiler.cs` — Roslyn C# 动态编译（TreeScriptNode / ITriggerScript）
- `Execution/AssistAxis.cs` — 辅助轴（独立并行轴，.txt 加载）

**完成标准**:
1. 执行轴可按时间或事件推进节点
2. 可切换 ACR 行为模式（AOE/单体/停手）
3. 可指定特定技能
4. 可查看当前节点及触发/未触发原因

---

### Phase 7: Fact Axis（事实轴） ✅ 已完成

**目标**: Boss 技能时间线的结构化建模——阶段内纯时间推进，Sync 校准事件，切换点择分支。

**依赖**: Phase 6
**需求**: FACT-01 ~ FACT-04

**交付**:
- `FactAxis/FactNode.cs` (237行) — 数据模型（FactPhase → FactEvent → FactPhaseSwitch → FactBranch，嵌套分支）
- `FactAxis/FactTimeline.cs` (425行) — 主控制器（阶段时间线推进 + Sync 事件匹配 + 分支切换 + FactState 输出）
- `FactAxis/sample_timeline.json` — 示例时间线 JSON 文件

**实现完成**:
- 阶段内纯时钟推进，事件有 startSync/endSync 校准实际开始/结束时刻
- 切换点通过 Sync 事件触发，分支条件择选，替换后续事件列表
- StageTime + TotalTime 双时钟
- JSON 目录: `FactTimelines/{副本ID}.json`

**完成标准**:
1. ✅ 可用 JSON 定义 Boss 技能时间线（阶段 + 事件 + 切换点）
2. ✅ 根据 Sync 事件校准事件实际开始/结束时间
3. ✅ 支持条件分支切换后续事件列表
4. ✅ 输出当前事实状态供上层消费（Phase 8）

---

### Phase 8: Decision Layer（智能决策层） ✅ 已完成

**目标**: 消费事实轴需求 + 队伍组成，分配减伤/治疗技能。

**依赖**: Phase 7
**需求**: AI-01 ~ AI-03

**交付**:
- `Decision/DecisionTypes.cs` (118行) — 技能数据类（团队减伤/单人减伤/团队治疗）+ 注册表 + 需求动作 + 输出模型
- `Decision/DecisionEngine.cs` (206行) — 队伍扫描 + 冷却过滤 + 贪心分配 + 内置技能数据

**实现完成**:
- 技能数据由 C# 代码定义（`DecisionSkillRegistry.注册(job, ...)`）
- 事实轴通过 `需求动作(需求减伤, 需求治疗)` 声明需求
- 引擎按冷却升序排序（短的优先），贪心凑够 ≥ 需求
- 输出全部分配技能强制发给 ACR（SkipSlotExecutor）

**完成标准**:
1. ✅ 可读取事实轴需求 + 队伍组成
2. ✅ 减伤分配优先级按冷却升序，允许超出需求
3. ✅ 输出强制技能列表给 ACR 执行

---

### Phase 9: Authoring Layer（创作与表现层） ✅ 已完成

**目标**: 可视化编辑、调试、复盘工具。**复用 Phase 5.3 的 CEF + WebSocket 基础设施。**

**依赖**: Phase 8
**需求**: UX-01 ~ UX-03

**完成部分**:
- 前端编辑器已完成：`UI/web/editor.html` / `editor.js` / `editor.css`
- 事实编辑器已完成：`UI/web/fact-editor.html` / `fact-editor.js` / `fact-editor.css`

**交付**:
- `UI/web/editor.html` / `editor.js` / `editor.css` — 可视化编辑器前端（纯前端 File System Access API）
- `UI/web/fact-editor.html` / `fact-editor.js` / `fact-editor.css` — 事实轴编辑器前端
- `UI/web/axflow-editor.html` / `axflow-editor.js` / `axflow-editor.css` — 执行/辅助轴编辑器前端
- `Authoring/AuthoringServer.cs` — WebSocket trigger catalog 注册

**完成标准**:
1. ✅ 可视化编辑器前端完成，纯前端不需要后端 CRUD
2. ✅ 导出/导入 JSON（File System Access API）
3. ✅ 调试界面可查看时间线漂移和节点状态

---

### HiAuRo.Helper — 全职业数据辅助库 ✅ 已完成

独立仓库提供 21 个职业（含青魔法师）的数据辅助类，由 HelperUpdater 自动拉取更新。

**仓库**: https://github.com/denghaoxuan991876906/HiAuRo.Helper

**交付**:
- 21 个职业的 Helper 文件（`ASTHelper.cs`, `BLMHelper.cs`, `BRDHelper.cs` 等）
- `ILuminaHelper.cs` — 统一接口
- `HelperUpdater.cs` — 自动下载/更新/热重载

**集成**:
- 主插件启动时自动检查 Helper 更新
- 按需下载最新 DLL 到本地缓存
- 运行时动态加载，支持热重载

---

## 进度总览

| Phase | 内容 | 状态 |
|-------|------|------|
| 1 | Host Layer | 已完成 |
| 2 | Infrastructure Layer | 已完成 |
| 3 | Data Layer | 已完成 |
| 4 | Runtime Core | 已完成 |
| 5 | ACR Abstraction + BRD 打样 | 已完成 |
| 6 | Execution Axis | 已完成 ✅ |
| 7 | Fact Axis | 已完成 ✅ |
| 8 | Decision Layer | 已完成 ✅ |
| 9 | Authoring Layer | ✅ 已完成 |
| — | HiAuRo.Helper | 全职业辅助库 ✅ |

**全部 Phase 进度**: 9/9 Phase 完成 ✓
**Phase 1-8 进度**: 全部完成 ✅
**Phase 9 进度**: 全部完成 ✅（纯前端编辑器，无需后端）
**HiAuRo.Helper**: 全职业辅助库 21 个职业完成 ✅

---

*Last updated: 2026-05-08*
