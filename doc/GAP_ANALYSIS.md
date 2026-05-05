# HiAuRo 文档 vs AEAssist 源码 — 差距分析

> 生成时间: 2026-05-03 | 基于 AEAssist 反编译源码与 HiAuRo 全量规划文档对比

---

## 严重问题

### 1. ISlotResolver 接口缺少 Build(Slot) 方法，Check() 语义偏移

- **位置**: doc/dev-tasks/PHASE5.1_ACR_CORE.md, Task 2; doc/ARCHITECTURE.md, ACR 接口定义段; doc/REQUIREMENTS.md, ACR-02
- **问题**: HiAuRo 定义 `ISlotResolver` 只有 `int Check()`，且文档描述为"返回技能 ID"（Check 返回 int = 技能 ID）。但 AE 中 `Check()` 返回的是优先级/可用性（>=0 表示可用），实际构建 Slot 的逻辑在 `void Build(Slot slot)` 中。两者设计完全不同。

  HiAuRo 的简化方案（Check 返回技能 ID → AI 引擎创建 Spell → 创建 SlotAction → 放入 Slot）虽然对简单技能有效，但对于以下 AE 常见场景会失效：
  - 一个 Slot 包含多个 SkillAction（如 GCD+oGCD 组合）
  - 技能需要根据运行时状态动态选择多个子技能
  - Slot 末尾调用 `AppendSequence()` 追加技能序列
  - 需要 `Add2NdWindowAbility()` 或 `AddDelaySpell()` 的复杂 Slot
  - Check 只返回一个 int 无法表达"嵌套的 Slot 构建逻辑"

- **AE 参考**: 
  - `AEAssist/CombatRoutine/Module/ISlotResolver.cs` — `int Check()` + `void Build(Slot slot)` 双方法
  - `AEAssist/CombatRoutine/Module/AILoop/PVE_RunSlotHelper.cs` line 225-248 — 实际调用 `v.Check()` 判断可用性，然后 `v.Build(slot)` 构造 Slot
  - Slot 的 `Add(spell)`、`Add2NdWindowAbility(spell)`、`AddDelaySpell(delay, spell)`、`AppendSequence(seq)` 等丰富 API 都依赖 Build 方法

- **建议**: 在 PHASE5.1_ACR_CORE.md 的 Task 2 中补充 `void Build(Slot slot)` 方法定义，并更新 Slot/SlotAction/Spell 的关系描述，避免 Check 返回 int 被理解为技能 ID 而非优先级判断值。

---

### 2. IOpener 接口设计与 AE 严重偏离

- **位置**: doc/dev-tasks/PHASE5.2_ACR_EXTENDED.md, Task 1; doc/ARCHITECTURE.md IOpener 定义段
- **问题**: HiAuRo 的 `IOpener` 定义为独立接口，包含 `Level` 和 `List<SlotResolverData> Resolvers`。
  AE 中 `IOpener : ISlotSequence, IScript` —— IOpener **继承 ISlotSequence**，拥有 `List<Action<Slot>> Sequence`（而非 `List<SlotResolverData>`），并且额外有 `InitCountDown(CountDownHandler)` 方法用于倒计时阶段行为注册。

  两者的差异意味着：
  - 起手本质上是"固定的技能序列"，不是"SlotResolver 列表"。AE 中起手 = Sequence，用 `Action<Slot>` 逐个构建 Slot。
  - AE 的 `InitCountDown` 允许职业在倒计时阶段注册预施法（如黑魔不同起手有不同倒计时处理），HiAuRo 完全没有这个入口。
  - AE 的 OpenerMgr 使用 `StartCheck()/StopCheck()` 控制起手开关（从 ISlotSequence 继承），HiAuRo 的 OpenerMgr 状态机只有 NotStarted/Running/Finished。

- **AE 参考**:
  - `AEAssist/CombatRoutine/Module/Opener/IOpener.cs` — `IOpener : ISlotSequence, IScript`, 含 `InitCountDown(CountDownHandler)`
  - `AEAssist/CombatRoutine/Module/Opener/OpenerMgr.cs` — 调用 `opener.StartCheck()` 和 `opener.InitCountDown(CountDownHandler.Instance)`
  - `AEAssist/CombatRoutine/Module/CountDownHandler.cs` — 管理倒计时行为，提供 `AddAction(timeleft, spellId, targetType)` 等 API

- **建议**: 
  - 在 PHASE5.2_ACR_EXTENDED.md 的 Task 1 中重新审视 IOpener 设计，使其继承或组合 ISlotSequence。
  - 补充 CountDownHandler（倒计时管理器）的需求和文件清单。
  - 如果 MVP 阶段不做倒计时处理，至少在文档中注明 Phase 6 补齐 InitCountDown 能力。

---

### 3. ISlotSequence 接口签名与 AE 不一致

- **位置**: doc/dev-tasks/PHASE5.2_ACR_EXTENDED.md, Task 1
- **问题**: HiAuRo 的 `ISlotSequence` 定义为：
  - `bool CanExecute()` — 能否执行
  - `List<SlotResolverData> GetResolvers()` — 返回 SlotResolver 列表

  AE 的 `ISlotSequence` 定义为：
  - `List<Action<Slot>> Sequence` — 一组对 Slot 的构建操作（每个 Action 构建一个 Slot）
  - `int StartCheck()` — 返回 >=0 表示可以启动
  - `int StopCheck(int index)` — 返回 >=0 表示可在第 index 步中断

  两者差异显著。AE 的 Sequence 方式允许每个步骤直接操作 Slot 对象（调用 `Add(spell)`, `AddDelaySpell()` 等），而 HiAuRo 的 SlotResolver 方式每个步骤都要通过 Check/Build 间接实现，增加复杂度。

- **AE 参考**: `AEAssist/CombatRoutine/Module/ISlotSequence.cs`
- **建议**: 在 PHASE5.2_ACR_EXTENDED.md 中增加注释说明 HiAuRo 版 ISlotSequence 是刻意简化的版本，标注偏离点及原因。如果决定保持 HiAuRo 的设计，需在 ARCHITECTURE.md 中明确此差异。

---

### 4. 缺少 Coroutine（协程）系统定义

- **位置**: doc/dev-tasks/PHASE4_RUNTIME.md（未提及）; doc/dev-tasks/PHASE5.4_ENGINE.md（未提及）
- **问题**: AEAssist 的核心执行流大量依赖 `Coroutine.Instance.WaitAsync()` 来实现异步等待（等待 GCD 冷却、等待动画锁、等待服务器确认等）。HiAuRo 的文档中完全没有提及协程/异步等待机制。SlotAction.Run() 方法是 async Task<bool>，没有协程支持无法实现等待动画锁、GCD 队列前置等核心功能。

  AE 的 `Plugin.cs` 中每帧调用 `Coroutine.Instance.Update()` 来推进所有等待中的协程。HiAuRo 没有对应机制。

- **AE 参考**:
  - `AEAssist/Plugin.cs` line 186-189 — 每帧 `Coroutine.Instance.Update()`
  - `AEAssist/CombatRoutine/Module/SlotAction.cs` line 49-101 — `Run()` 是 `async Task<bool>`，内部大量 `await Coroutine.Instance.WaitAsync()`
  - `AEAssist/Helper/SpellHelper.cs` line 204-304 — `NfCnlailSI` 方法中的协程等待流程

- **建议**: 在 PHASE4_RUNTIME.md 中增加 Coroutine/协程调度系统的需求（或在 Task 1 中明确接入方式），并评估是否需要引入 OmenTools 中的等价机制或自建。

---

### 5. GCD 窗口判断缺少 ActionQueueInMs 参数

- **位置**: doc/dev-tasks/PHASE5.2_ACR_EXTENDED.md, Task 3 (GCDHelper); doc/dev-tasks/PHASE5.4_ENGINE.md, Task 1 (AIRunner GCD 窗口规则)
- **问题**: HiAuRo 的 GCDHelper 定义了 `CanUseOffGcd()` 基于固定阈值（750ms/1500ms），GCD 窗口规则是硬编码数值。
  AE 中 `CanUseGCD()` 的判断依赖 `GeneralSettings.ActionQueueInMs`（默认 200ms，即可以在 GCD 剩余 200ms 时提前队列下一个 GCD）。这个值是关键的可调参数，直接影响技能衔接的流畅度。
  HiAuRo 没有在任何地方定义 `ActionQueueInMs` 配置项。

- **AE 参考**:
  - `AEAssist/Helper/GCDHelper.cs` line 31-35 — `CanUseGCD()` 使用 `SettingMgr.GetSetting<GeneralSettings>().ActionQueueInMs`
  - `AEAssist/CombatRoutine/GeneralSettings.cs` line 17 — `public int ActionQueueInMs` (初始值 200)
  - `AEAssist/CombatRoutine/Module/SlotAction.cs` line 60-63 — `if (gcdCooldown > SettingMgr.GetSetting<GeneralSettings>().ActionQueueInMs)` 用于确定是否需等待

- **建议**: 在 PHASE5.3_UI.md 的 SettingMgr 任务中增加 ActionQueueInMs 配置项；在 PHASE5.2_ACR_EXTENDED.md 和 PHASE5.4_ENGINE.md 中让 GCD 窗口判断引用此参数而非硬编码。

---

### 6. Spell 缺少 SpellTargetType 枚举和完整的 SpellCategory

- **位置**: doc/dev-tasks/PHASE5.1_ACR_CORE.md, Task 2.5 (Spell)
- **问题**: HiAuRo 的 `Spell.cs` 描述为"技能定义（ID、名称、类型、目标选择器）"但未定义类型系统。
  AE 的 Spell 有 `SpellTargetType` 枚举（Target/Self/TargetTarget/Pm1~Pm8/SpecifyTarget/Location/DynamicTarget/MapCenter），`SpellCategory` 枚举（Default/LimitBreak/Potion/Sprint/Dance/Item），以及 `SpecifyTarget`（指定目标对象）、`GetDynamicsTarget`（动态目标委托函数）、`UsePos`（地面技能坐标）、`WaitServerAcq`（服务器确认）、`DontUseGcdOpt`（不使用 GCD 偏移优化）。

  枚举缺失意味着 ACR 作者无法区分目标类型，需要自己维护映射，迁移成本高。SpellCategory 是区分普通技能、爆发药、疾跑、LB 等的关键字段。

- **AE 参考**:
  - `AEAssist/CombatRoutine/SpellTargetType.cs` — 完整的 11 种目标类型
  - `AEAssist/CombatRoutine/SpellCategory.cs` — 6 种技能分类
  - `AEAssist/CombatRoutine/Spell.cs` — 18 个字段属性

- **建议**: 在 PHASE5.1_ACR_CORE.md 的 Task 2.5 中增加 `SpellTargetType` 和 `SpellCategory` 两个枚举的文件，并补全 Spell 的字段说明。

---

### 7. IRotationUI 接口未在任何文档中定义

- **位置**: doc/dev-tasks/PHASE5.1_ACR_CORE.md, Task 1 (IRotationEntry 引用 IRotationUI 但未定义); doc/ARCHITECTURE.md 文件清单（未列出）
- **问题**: `IRotationEntry` 的 `IRotationUI? GetRotationUI()` 引用了 `IRotationUI`，但在所有 dev-tasks 和 ARCHITECTURE.md 中均未定义该接口。AE 的 IRotationUI 包含 `Update()`, `IsCustomMain()`, `OnDrawUI()` 三个成员，是职业悬浮窗的 UI 接口。

- **AE 参考**: `AEAssist/CombatRoutine/IRotationUI.cs` — `void Update()`, `bool IsCustomMain()`, `void OnDrawUI()`
- **建议**: 在 PHASE5.1_ACR_CORE.md 或 PHASE5.3_UI.md 中增加 IRotationUI 接口定义及其文件清单条目。

---

## 中等问题

### 8. 缺少 ITargetResolver 接口（目标选择器）

- **位置**: doc/dev-tasks/PHASE5.1_ACR_CORE.md（未提及）; doc/ARCHITECTURE.md（未列出）
- **问题**: AE 的 `Rotation` 可通过 `AddTargetResolver(params ITargetResolver[])` 注册多个目标选择器。`ITargetResolver` 的 `ResolveTarget(out IBattleChara agent)` 在进战前/进战后主动选择目标。HiAuRo 完全未提及此接口。

  目标选择是 ACR 框架的重要部分——近战需要切换目标、法系需要智能选择、AOE 需要找最优位置目标。缺少此接口会让 ACR 作者需要自己在每个 SlotResolver 中处理目标切换。

- **AE 参考**: 
  - `AEAssist/CombatRoutine/ITargetResolver.cs` — `bool ResolveTarget(out IBattleChara agent)`
  - `AEAssist/CombatRoutine/Rotation.cs` line 79, 110-115 — `TargetResolvers` 列表和 `AddTargetResolver` 方法

- **建议**: 在 PHASE5.2_ACR_EXTENDED.md 中增加 ITargetResolver 接口定义，或在 TargetHelper 中增加说明。

---

### 9. 热键系统缺少 IHotkeyEventHandler（与 IHotkeyResolver 不同）

- **位置**: doc/dev-tasks/PHASE5.3_UI.md, Task 1 (HotkeyHelper)
- **问题**: HiAuRo 的热键系统只定义了 `IHotkeyResolver`（`uint Resolve()` 返回技能 ID）。
  AE 有两层热键系统：
  - `IHotkeyEventHandler`：注册到 `Rotation.HotkeyEventHandlers`，接受 `HotkeyConfig config`，可以处理复杂热键行为（不止释放技能）
  - `HotkeyManager`：管理所有热键注册/注销/触发
  - `IHotkeyResolver` 实际存在于 AE 的 GUI/QT 系统中，是悬浮窗使用的

  两者不是同一个东西。HiAuRo 的 `IHotkeyResolver` 适合 QT 面板的简单热键释放技能，但 `IHotkeyEventHandler` 才是 ACR 作者在 Rotation 层注册热键的接口。

- **AE 参考**: 
  - `AEAssist/CombatRoutine/Module/Hotkey/IHotkeyEventHandler.cs` — `void Run(HotkeyConfig config)`
  - `AEAssist/CombatRoutine/Rotation.cs` line 40, 119-124 — `HotkeyEventHandlers` 和 `AddHotkeyEventHandlers`
  - `AEAssist/CombatRoutine/Module/Hotkey/HotkeyManager.cs` — 热键管理器

- **建议**: 在 PHASE5.3_UI.md 中区分 IHotkeyResolver（QT 面板用）和 IHotkeyEventHandler（ACR Rotation 注册用），补充 HotkeyConfig 数据结构。

---

### 10. Rotation 类缺少若干 AE 中的扩展入口

- **位置**: doc/dev-tasks/PHASE5.1_ACR_CORE.md, Task 1 (Rotation); doc/ARCHITECTURE.md Rotation 定义段
- **问题**: HiAuRo 的 `Rotation` 类只列出了 6 个字段：
  - `SlotResolvers`, `SlotSequences`, `Opener`, `EventHandler`, `TriggerActions`, `TriggerConditions`

  AE 的 Rotation 还有：
  - `TargetJob` / `AcrType` / `MinLevel` / `MaxLevel` / `Description` — 元信息
  - `TargetResolvers` — 目标选择器列表
  - `HotkeyEventHandlers` — 热键事件处理列表
  - `AddCanUseHighPrioritySlotCheck()` — 时间轴高优先级技能合法性检查
  - `AddCanPauseACRCheck()` — 暂停 ACR 的条件检查
  - `AddTriggerlineUpgradeFromStr/Data()` — 时间轴版本升级处理
  - `SetACRAutoUpdateTimeline()` — 自动更新时间轴

  虽然 MVP 不需要全部功能，但至少 `TargetJob`、`TargetResolvers`、`HotkeyEventHandlers` 应该在 Rotation 中预留。

- **AE 参考**: `AEAssist/CombatRoutine/Rotation.cs` — 完整 196 行，含 16+ 成员
- **建议**: 在 Rotation 定义中增加 `TargetJob` 和 `HotkeyEventHandlers` 字段（基本必需），其他 Phase 6+ 的入口标注为预埋。

---

### 11. 缺少 Spell.Idle 空转技能

- **位置**: doc/dev-tasks/PHASE5.1_ACR_CORE.md, Task 2.5 (Spell)
- **问题**: AE 中 `Spell.Idle` 是一个特殊的 sentinel 技能（ID=0, TargetType=Self），SlotAction.Run() 遇到它时会等待 100ms 而不是尝试释放。这个设计用于"空转 100ms"等场景。
  HiAuRo 的 Spell 定义没有 Idle 静态实例。

- **AE 参考**: 
  - `AEAssist/CombatRoutine/Spell.cs` line 22 — `public static readonly Spell Idle`
  - `AEAssist/CombatRoutine/Module/SlotAction.cs` line 53-56 — 判断 `this.Spell == Spell.Idle` 时等待 100ms

- **建议**: 在 PHASE5.1_ACR_CORE.md 的 Task 2.5 中增加 `Spell.Idle` 的设计说明。

---

### 12. Slot 缺少内部执行标志 InSequence / breakTime

- **位置**: doc/dev-tasks/PHASE5.1_ACR_CORE.md, Task 2.5 (Slot)
- **问题**: HiAuRo 的 Slot 只定义了 `Actions`、`MaxDuration`、`Wait2NextGcd`、`AppendSequence`。
  AE 的 Slot 还有 `internal InSequence`（指示正在序列中执行，影响失败重试行为）和 `internal breakTime`（下次重试的最后时间点）。这些是 PVE_RunSlotHelper 中序列执行逻辑依赖的内部状态。

- **AE 参考**: `AEAssist/CombatRoutine/Module/Slot.cs` line 31-33 — `internal bool InSequence`, `internal long breakTime`
- **建议**: 在 PHASE5.4_ENGINE.md 中补充 Slot 的运行时内部状态说明，或在 Slot 定义中增加 `InSequence` 标记。

---

### 13. SpellQueue 设计需要与 AE 的实际队列机制对齐

- **位置**: doc/dev-tasks/PHASE5.4_ENGINE.md, Task 1 (SpellQueue)
- **问题**: HiAuRo 的 SpellQueue 定义为简单的 `Enqueue/Dequeue/Update` 模式。AE 中并没有独立的"SpellQueue"类，而是通过以下机制实现：
  - `BattleData.NextSlot` — 下一个要执行的 Slot（倒计时/起手注入）
  - `BattleData.HighPrioritySlots_GCD/OffGCD` — 时间轴注入的高优先级技能队列
  - `BattleData.CurrSlot/CurrSequence` — 当前正在执行的 Slot/序列栈
  - `BattleData.WaitGcdSlot` — 等待下个 GCD 再执行的 Slot（如 AppendSequence 产生的）

  此外，AE 的技能实际上是通过 `UseActionManager.UseAction()` 直接调用（依托 OmenTools），不是排入 FFXIV 的 1-deep 技能队列。SpellQueue 的职责更像是"管理待执行 Slot 的调度表"。

- **AE 参考**: `AEAssist/CombatRoutine/Module/BattleData.cs` — NextSlot/HighPrioritySlots_GCD/HighPrioritySlots_OffGCD/CurrSlot/CurrSequence/WaitGcdSlot
- **建议**: 在 PHASE5.4_ENGINE.md 中明确 SpellQueue 的定位——不是 FFXIV 客户端的技能队列，而是 HiAuRo 内部的 Slot 调度队列。梳理 NextSlot / HighPrioritySlots / CurrSlot 的关系。

---

### 14. Phase 6 触发器具体实现列表过少

- **位置**: doc/ROADMAP.md, Phase 6; doc/REQUIREMENTS.md, EXEC-01~04
- **问题**: HiAuRo Phase 6 计划列出首批触发器仅 5-6 种（TriggerCond_敌人读条、TriggerCond_经过时间、TriggerCond_技能后、TriggerCond_Actor死亡 + TriggerAction_切换目标、TriggerAction_释放技能、TriggerAction_切换停手、TriggerAction_吃药）。
  AE 有 30+ 种 TriggerCond 和 20+ 种 TriggerAction，包括：
  - TriggerCond_倒计时、TriggerCond_ReceviceAbilityEffect、TriggerCond_CheckTargetIcon、TriggerCond_ActorControlTether、TriggerCond_MapEffect、TriggerCond_WeatherChange、TriggerCond_GameLog、TriggerCond_VFX_Create、TriggerCond_NpcYell、TriggerCond_Variable
  - TriggerAction_SpellQueue、TriggerAction_HighPrioritySlot、TriggerAction_LockSpell、TriggerAction_SetRotation、TriggerAction_SendCommand、TriggerAction_SendKey、TriggerAction_MoveTo、TriggerAction_ReplayOpener

  虽然 Phase 6 不必实现全部 50+ 种，但文档至少应列出完整的 AE 触发器清单作为参考，帮助评估实现范围。

- **AE 参考**: `AEAssist/CombatRoutine/Trigger/` 目录 — TriggerCond/ 和 TriggerAction/ 各有 30+ 和 20+ 文件
- **建议**: 在 ROADMAP.md Phase 6 中增加 AE 触发器清单的引用，标注"首批实现"和"后续补充"的分界。

---

### 15. ITriggerAction / ITriggerCond 签名与 AE 有差异

- **位置**: doc/dev-tasks/PHASE5.2_ACR_EXTENDED.md, Task 2
- **问题**: HiAuRo 定义：
  - `ITriggerAction` → `void Execute()`
  - `ITriggerCond` → `bool Check()`

  AE 定义：
  - `ITriggerAction : ITriggerBase` → `bool Handle()`
  - `ITriggerCond : ITriggerBase` → `bool Handle(ITriggerCondParams condParams = null)`

  AE 还引入了 `ITriggerBase` 基础接口和 `ITriggerCondParams` 参数传递机制。触发器条件的 `condParams` 携带了触发上下文（如哪个敌人读条、什么技能等），这会直接影响条件判断的逻辑。

- **AE 参考**: 
  - `AEAssist/CombatRoutine/Trigger/ITriggerAction.cs` — `bool Handle()`
  - `AEAssist/CombatRoutine/Trigger/ITriggerCond.cs` — `bool Handle(ITriggerCondParams condParams = null)`
- **建议**: 在文档中明确 `Execute()` vs `Handle()` 的命名选择；补充 `ITriggerCondParams` 传参机制的设计留白。

---

### 16. 缺少 SpellType 枚举

- **位置**: doc/dev-tasks/PHASE5.1_ACR_CORE.md, Task 2.5 (Spell)
- **问题**: AE 的 `SpellType` 枚举定义了 `None / RealGcd / GeneralGcd / Ability` 四种类型，用于区分技能是真实 GCD、通用 GCD、能力技等。Spell 对象上有 `IsAbility()` 扩展方法判断技能类型。HiAuRo 未定义此枚举。

- **AE 参考**: `AEAssist/CombatRoutine/SpellType.cs`
- **建议**: 在 Spell.cs 设计中增加 SpellType 枚举和 `IsAbility()` 等判断方法。

---

### 17. PHASE5.1_ACR_CORE.md 任务编号错乱

- **位置**: doc/dev-tasks/PHASE5.1_ACR_CORE.md, 任务列表段
- **问题**: 文档中有 Task 1（IRotationEntry + Rotation），然后直接跳到一个无编号的 Task 段落（"**操作**:" 开头，内容为 SlotMode/ISlotResolver/SlotResolverData），然后才是 Task 2.5（Slot/SlotAction/Spell），最后 Task 3（SpellsDefine + AurasDefine）。Task 2 的标题丢失，Task 2.5 的命名暗示中间缺少 Task 2。同时"Task 2.5"逻辑上应该是 Task 3，但 Task 3 又是 SpellsDefine 任务。

  这会导致开发人员不清楚实际的任务边界和执行顺序。

- **AE 参考**: 不适用（文档结构问题）
- **建议**: 修正 PHASE5.1_ACR_CORE.md 的任务编号：Task 1 (IRotationEntry+Rotation) → Task 2 (SlotMode/ISlotResolver/SlotResolverData) → Task 3 (Spell/SlotAction/Slot) → Task 4 (SpellsDefine/AurasDefine)。

---

### 18. EventSystem 事件与 AE 的 UseAction Hook 触发时机有偏差

- **位置**: doc/dev-tasks/PHASE4_RUNTIME.md, Task 2 (EventSystem)
- **问题**: HiAuRo 的 EventSystem 计划在 Phase 4 通过 `UseActionManager` 的 Hook 监听 `PreUseAction/PostUseAction` 和 `PreCharacterStartCast/PostCharacterCompleteCast`，然后分发给 `OnSpellCastSuccess` 等回调。

  AE 中这些回调（BeforeSpell/AfterSpell/OnSpellCastSuccess）不是通过后台 Hook 事件触发的，而是在 Slot 执行过程中**同步调用**的：
  - `BeforeSpell` → PVE_RunSlotHelper.Run() line 282，SlotAction.Run() 之前
  - `AfterSpell` → SlotAction.Run() line 82，技能执行成功后
  - `OnSpellCastSuccess` → SpellHelper.NfCnlailSI() line 276，读条判定成功后

  这两者的区别在于：Hook 事件是异步的（跨帧），而 AE 的同步调用保证了执行时序。如果 HiAuRo 走 Hook 路线，需要处理异步带来的时序问题。

- **AE 参考**: `AEAssist/CombatRoutine/Module/AILoop/PVE_RunSlotHelper.cs` line 282; `AEAssist/CombatRoutine/Module/SlotAction.cs` line 82; `AEAssist/Helper/SpellHelper.cs` line 276
- **建议**: 在 PHASE4_RUNTIME.md 中补充说明 EventSystem 是作为**底层基础设施**提供原始事件，而 Handler 的回调应由 AIRunner 在 Slot 执行过程中同步触发；或者明确说明 HiAuRo 选择了异步 Hook 方式并指出差异。

---

## 轻微问题

### 19. ARCHITECTURE.md 文件清单与 PHASE5.x 文件清单不完全一致

- **位置**: doc/ARCHITECTURE.md 项目结构段 vs doc/dev-tasks/PHASE5.x 各文件
- **问题**: ARCHITECTURE.md 列出的文件清单中：
  - 缺少 `Runtime/EventSystem.cs`（PHASE4_RUNTIME 新增）
  - 缺少 `Runtime/IAILoop.cs`、`Runtime/AILoop_Normal.cs`、`Runtime/SpellQueue.cs`、`Runtime/AIRunner.cs`（PHASE5.4_ENGINE 新增）
  - 列出了 `Spell.cs`、`SlotAction.cs`、`Slot.cs` 但 PHASE5.1 的文件清单中没有列出（只有正文提到）
  - 列出了 `BRDBattleData.cs` 但 PHASE5.4 中才出现

  ARCHITECTURE.md 作为架构蓝图应与各 dev-tasks 的文件清单保持一致。

- **AE 参考**: 不适用（文档一致性）
- **建议**: 更新 ARCHITECTURE.md 的文件清单，使其与 PHASE4/PHASE5 的 dev-tasks 对齐。

---

### 20. Data.Party 缺少 AE PartyHelper 的若干分类视图

- **位置**: doc/REQUIREMENTS.md, Phase 3 Data.Party; doc/dev-tasks/PHASE3_DATA.md, Task 2
- **问题**: HiAuRo 的 Data.Party 定义的视图为：All / Alive / Dead / Tanks / Healers / Dps / Nearby5y / Nearby10y / Nearby15y。
  AE 的 PartyHelper 提供：Party / DeadAllies / CastableParty / CastableTanks / CastableHealers / CastableDps / CastableMainTanks / CastableAlliesWithin3/10/15/20/25/30 / CastableMelees / CastableRangeds。

  差异在于：
  - AE 有 `CastableMainTanks`（开着盾姿的 T），HiAuRo 没有
  - AE 有 `CastableMelees/CastableRangeds`（近战/远程分类），HiAuRo 没有
  - AE 有 `CastableAlliesWithin20/25/30`，HiAuRo 只有 5/10/15
  - AE 区分 Dead 列表的排序（T/Healer 优先排前面）

  这些分类对于职业实现很重要（如奶妈需要区分近战/远程来分配减伤，T 需要知道 MainTank 是谁）。

- **AE 参考**: `AEAssist/Helper/PartyHelper.cs` — 13 个分类视图
- **建议**: 在 PHASE3_DATA.md 的 Task 2 中增加 CastableMainTanks、CastableMelees/CastableRangeds、Nearby20y/25y/30y 分类视图。

---

### 21. SettingMgr 缺少职业独立设置的泛型读取方式

- **位置**: doc/dev-tasks/PHASE5.3_UI.md, Task 3 (SettingMgr); doc/ARCHITECTURE.md SettingMgr 段
- **问题**: HiAuRo 的 SettingMgr 定义为 `GetSetting<T>()`（全局）和 `GetJobSetting<T>(string job)`（职业独立设置）。职业设置存储为独立 JSON 文件。
  AE 的 SettingMgr 使用 `GetSetting<T>()` 统一接口，内部通过程序集扫描自动发现所有 aHobqWRLGcWd1Y4Lvgo 子类（即设置类），自动加载/持久化为 `{SettingPath}/{TypeName}.json`。每种设置类自动对应一个文件，不需要区分"全局"和"职业"。
  这种方式更灵活——职业设置也可以是一个设置类（如 `BRDSetting : BaseSetting`），自动走同一套机制。

- **AE 参考**: `AEAssist/CombatRoutine/SettingMgr.cs` — 程序集扫描 + 统一 JSON 持久化
- **建议**: 在 SettingMgr 的设计注释中说明 HiAuRo 选择了显式区分全局/职业路径的方式（而非程序集扫描），并说明理由。

---

### 22. GeneralSettings 缺少若干 AE 配置项

- **位置**: doc/dev-tasks/PHASE5.3_UI.md, Task 3 (SettingMgr 全局设置字段)
- **问题**: HiAuRo 的全局设置字段只列了 3 个：`EnableACR`、`AttackRange`、`AoeCount`。
  AE 的 GeneralSettings 有 20+ 个字段，其中对 ACR 框架运行至关重要的包括：
  - `ActionQueueInMs` / `Ping` — GCD 队列控制（见严重问题 #5）
  - `MaxAbilityTimesInGcd` — GCD 内最大能力技数量（默认 2）
  - `OptimizeGcd` / `NoClipGCD3` — GCD 偏移优化
  - `GlobalNotWaitServerAcq` — 全局能力技不等服务器确认
  - `CastingSpellSuccessRemainTiming` — 读条成功判定剩余时间
  - `AutoStopWhenHasNotCastingStatus` — 无法施法时自动停手
  - `UpdateInterval3` — ACR 运行间隔

  这些配置直接影响运行性能和行为，至少 ActionQueueInMs、MaxAbilityTimesInGcd、OptimizeGcd 应在 MVP 中提供。

- **AE 参考**: `AEAssist/CombatRoutine/GeneralSettings.cs` — 20+ 字段
- **建议**: 在 SettingMgr 的全局设置中增加 `ActionQueueInMs`、`MaxAbilityTimesInGcd`、`OptimizeGcd` 三个配置项。其余标注为 Phase 6+ 补充。

---

### 23. PHASE5.4_ENGINE.md 中存在内容重复

- **位置**: doc/dev-tasks/PHASE5.4_ENGINE.md, Task 1
- **问题**: AIRunner 的 `Update()` 逻辑被写了两次（第 62-73 行和第 78-94 行有大量重复描述），包含略有不同的 GCD 窗口规则和流程描述。这会造成开发时的困惑——应该以哪一版为准。

- **AE 参考**: 不适用（编辑问题）
- **建议**: 合并重复内容，保留一份清晰的 AIRunner.Update() 流程描述。

---

### 24. JobApi（XXHelp.cs）命名不一致

- **位置**: doc/REQUIREMENTS.md Phase 3 Job 数据段 vs doc/AEASSIST_STUDY.md JobApi 段
- **问题**: AE 用 `JobApi` 命名职业快捷入口（如 `JobApi_Bard.cs`），HiAuRo 用 `XXHelp.cs` 命名（如 `BRDHelp.cs`）。两者概念一致，但命名不同。如果在后续文档中混用（如在 PHASE5 中用 `JobApi` 引用），会导致新开发者困惑。

  另外 AE 有 24 个 JobApi 文件（每职业 1 个），HiAuRo 目前只计划在 Phase 3 做一个 BRDHelp.cs。文档应说明未来的 XXHelp.cs 扩展计划。

- **AE 参考**: `AEAssist/JobApi/` — 24 个文件
- **建议**: 在 REQIREMENTS.md 或 ARCHITECTURE.md 中统一术语为 "XXHelp" 并说明后续 Phase 会按需扩展其他职业。

---

### 25. 缺少 Jobs 枚举和 JobsCategory 职业分类

- **位置**: doc/dev-tasks/PHASE5.1_ACR_CORE.md（未提及）
- **问题**: AE 有 `Jobs` 枚举（所有战斗职业 + 基础职业映射，如 Gladiator→Paladin）和 `JobsCategory`（按职能分类：Tank/Healer/Melee/Ranged/Caster）。
  HiAuRo 文档中完全未提及职业枚举定义，但 Rotation 中显然需要 `TargetJob` 字段来标识适配职业。BRD 打样也需要判断"当前职业是否是诗人"。

- **AE 参考**: `AEAssist/CombatRoutine/Jobs.cs`, `AEAssist/CombatRoutine/JobsCategory.cs`
- **建议**: 在 PHASE5.1_ACR_CORE.md 中增加 `Jobs.cs` 和 `JobsCategory.cs` 文件到文件清单，或明确说明使用 OmenTools 的职业枚举。

---

### 26. ROADMAP.md Phase 6 描述与 AEASSIST_STUDY.md 不对齐

- **位置**: doc/ROADMAP.md Phase 6 Plan 06-01 vs doc/AEASSIST_STUDY.md 2.3 节
- **问题**: ROADMAP.md Phase 6 Plan 06-01 描述为"补齐 Phase 5 定义的 ITriggerCond / ITriggerAction 接口"。
  但 AEASSIST_STUDY.md 明确指出 AE 的触发器系统包含 TriggerLine（触发线）、AST 节点（TreeSequence/TreeParallel/TreeSelect 等 10 种节点）、30+ 种条件、20+ 种动作。
  
  Phase 6 如果只补齐接口实现而不引入 TriggerLine 概念，则执行轴的核心能力（时间线 + 条件驱动的节点推进）无法落地。文档应对齐描述范围。

- **AE 参考**: AEASSIST_STUDY.md 2.3 节 — 完整触发器 AST 节点类型列表
- **建议**: 在 ROADMAP.md Phase 6 中增加 TriggerLine 和执行轴节点的设计概要，确保 Phase 6 的范围定义与 AEASSIST_STUDY 中的触发器系统描述一致。

---

### 27. SpellTargetLimit 类型未定义

- **位置**: doc/dev-tasks/PHASE5.1_ACR_CORE.md, Task 2.5 (Spell)
- **问题**: AE 有 `SpellTargetLimit_HPType` 和 `SpellTargetLimit_JobType` 用于限制目标选择（如只选 HP < 50% 的目标、只选特定职业的目标）。HiAuRo 未定义。
  虽然这些是高级功能，但如果 Spell 的 `GetTarget()` 逻辑需要这些限制参数，则应提前预留接口。

- **AE 参考**: `AEAssist/CombatRoutine/SpellTargetLimit_HPType.cs`, `AEAssist/CombatRoutine/SpellTargetLimit_JobType.cs`
- **建议**: 标注为 Phase 6+ 补充功能，MVP 阶段 Spell 的 GetTarget() 不包含限制型过滤。

---

## 总结

| 严重程度 | 数量 | 关键领域 |
|----------|------|----------|
| 严重 | 7 | ISlotResolver 双方法、IOpener 继承链、ISlotSequence 签名、Coroutine 系统、ActionQueueInMs、SpellTargetType/Category、IRotationUI 缺失 |
| 中等 | 11 | ITargetResolver、IHotkeyEventHandler、Rotation 扩展入口、Spell.Idle、Slot 内部状态、SpellQueue 定位、触发器清单、ITriggerCondParams、SpellType、任务编号、EventSystem 时序 |
| 轻微 | 9 | 文件清单一致性、Party 分类、SettingMgr 方式、配置项、内容重复、命名、Jobs 枚举、Phase 6 范围、SpellTargetLimit |

**最关键的两个待办**:
1. 重新审视 ISlotResolver 的 Check()/Build() 双方法设计 — 这影响所有 ACR 作者的编码方式
2. 补充 Coroutine / 异步执行机制 — 这影响 SpellQueue 和技能执行的底层可用性

