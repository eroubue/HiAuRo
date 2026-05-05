# AEAssist ↔ HiAuRo 差异检查报告

> 逐系统对比，标注差异等级（🔴 缺失 / 🟡 简化 / 🟢 对齐）

---

## 一、总体对照

| AEAssist 子系统 | 文件数 | HiAuRo 对应 | 等级 |
|:---|:---|:---|:---:|
| CombatRoutine/ 核心接口 | ~30 | ACR/ 目录 (37 文件) | 🟢 |
| Trigger/ 触发器引擎 | 107 | Execution/Triggers/ (28 文件, 18 Cond + 10 Action) | 🟡 |
| Helper/ 工具类 | 46 | ACR/ 15+ Helper + Data/ 5 分区 | 🟡 |
| MemoryApi/ 内存层 | 50 | OmenTools DService（设计决策，不做） | 🟢 |
| JobApi/ 职业快捷入口 | 24 | Data/Jobs/BRDHelp.cs (1 个) | 🟡 |
| GUI/ + Tree/ 可视化编辑器 | 63 | Phase 9（设计决策） | 🟢 |
| Module/AI 循环核心 | ~10 | Runtime/ (9 文件) | 🟡 |
| Module/Opener 起手 | 2 | ACR/OpenerMgr.cs + IOpener.cs | 🟡 |
| Module/Hotkey 热键 | 4 | ACR/HotkeyHelper.cs + 4 个 HotkeyResolver | 🟢 |
| Module/Target 目标选择 | 5 | ACR/TargetResolvers/ (5 种实现) | 🟢 |
| View/UI 面板 | ~20 | UI/Web 前端 (Phase 5.3 自研) | 🟢 |

---

## 二、逐系统详解

### 2.1 触发器系统（最关键差异）

#### TriggerCond — 触发条件

| AEAssist (28 种) | HiAuRo (18 种) | 状态 |
|:---|:---|:---:|
| TriggerCondAfterBattleStart | TriggerCond_经过时间 | 🟢 |
| TriggerCondBeforeBattleTime | TriggerCond_倒计时开始 | 🟢 |
| TriggerCondAfterSpell | TriggerCond_技能后 | 🟢 |
| TriggerCondEnemyCastSpell | TriggerCond_敌人读条 | 🟢 |
| TriggerCondActorDeath | TriggerCond_Actor死亡 | 🟢 |
| TriggerCondActorControlTargetIcon | TriggerCond_检查目标图标 | 🟢 |
| TriggerCondActorControlTether | TriggerCond_连线 | 🟢 |
| TriggerCondCheckTargetIcon | TriggerCond_检查目标图标 | 🟢 |
| TriggerCondCheckLastSpell | TriggerCond_上次技能 | 🟢 |
| TriggerCondCheckSpellCd | TriggerCond_技能冷却 | 🟢 |
| TriggerCondReceviceAbilityEffect | TriggerCond_收到技能效果 | 🟢 |
| TriggerCondMapEffect | TriggerCond_地图特效 | 🟢 |
| TriggerCondGameLog | TriggerCond_游戏日志 | 🟢 |
| TriggerCondOnWeatherIdChanged | TriggerCond_天气变化 | 🟢 |
| TriggerCondAfterUnitIsTargetable | TriggerCond_单位可选中 | 🟢 |
| TriggerCondAfterUnitRemove | TriggerCond_单位移除 | 🟢 |
| TriggerCondWaitTarget | TriggerCond_等待目标 | 🟢 |
| TriggerCondCheckPartyRole | ❌ 无 | 🔴 |
| TriggerCondMonitorACT | ❌ 无 | 🔴 |
| TriggerCondCheckOmegaLoop | ❌ 无 | 🔴 |
| TriggerCondVariable | ❌ 无 | 🔴 |
| TriggerCond_CheckCharacterType | ❌ 无 | 🔴 |
| TriggerCondCheckRecentlyTether | ❌ 无 | 🔴 |
| Countdown timer | TriggerCond_倒计时 | 🟢 |

**Phase 6 实现了 18 种触发条件，已覆盖 AE 核心子集。剩余 10+ 种待后续补充。**

#### TriggerCondParams — 条件参数

| AEAssist (21 种) | HiAuRo (5 种) | 状态 |
|:---|:---|:---:|
| EnemyCastSpellCondParams | TriggerCondParams_敌人读条 | 🟢 |
| BattleStartCondParams | TriggerCondParams_经过时间 | 🟢 |
| AfterSpellCondParams | TriggerCondParams_技能后 | 🟢 |
| ActorDeathParams | TriggerCondParams_Actor死亡 | 🟢 |
| ❌ AE 用 BeforeBattleTimeCondParams 等 | TriggerCondParams_倒计时 | 🟡 |
| ReceviceAbilityEffectCondParams | ❌ 无 | 🔴 |
| NpcYellCondParams | ❌ 无 | 🔴 |
| VFXCreatCondParams | ❌ 无 | 🔴 |
| TetherCondParams | ❌ 无 | 🔴 |
| GameLogCondParams | ❌ 无 | 🔴 |
| WeatherChangedCondParams | ❌ 无 | 🔴 |
| 其余 10+ 种 | ❌ 无 | 🔴 |

#### TriggerAction — 触发动作

| AEAssist (17 种) | HiAuRo (10 种) | 状态 |
|:---|:---|:---:|
| TriggerActionCastSpell | TriggerAction_释放技能 | 🟢 |
| TriggerActionSwitchStop | TriggerAction_切换停手 | 🟢 |
| TriggerActionSelectenemy | TriggerAction_切换目标 | 🟢 |
| TriggerActionUsePotion | TriggerAction_吃药 | 🟢 |
| TriggerActionHighPrioritySlot | TriggerAction_高优Slot | 🟢 |
| TriggerActionSpellQueue | TriggerAction_技能队列 | 🟢 |
| TriggerActionLockSpell | TriggerAction_锁定技能 | 🟢 |
| TriggerActionSetRotation | TriggerAction_设置Rotation | 🟢 |
| TriggerActionReplayOpener | ❌ 无 | 🔴 |
| TriggerActionSwitchPull | ❌ 无 | 🔴 |
| TriggerAction_MoveTo | ❌ 无 | 🔴 |
| TriggerAction_SimpleTP | ❌ 无 | 🔴 |
| TriggerAction_SendCommand | TriggerAction_发送命令 | 🟢 |
| TriggerAction_SendKey | TriggerAction_发送按键 | 🟢 |
| TriggerActionAddVariable | ❌ 无 | 🔴 |
| TriggerAction_OnCastingTP | ❌ 无 | 🔴 |
| TriggerAction_HackBoxSetFeatureEnabled | ❌ 设计排除 | 🟢 |

**Phase 6 实现了 10 种触发动作，已覆盖 AE 核心子集。剩余 7 种（ReplayOpener/SwitchPull/MoveTo/SimpleTP/AddVariable/OnCastingTP）。**

#### 触发树节点（AST）

| AEAssist (22 种) | HiAuRo (10 种枚举，树未使用) | 状态 |
|:---|:---|:---:|
| TreeSequence | ExecutionNodeType.Sequence | 🟡 枚举已定义，引擎未实现 |
| TreeParallel | ExecutionNodeType.Parallel | 🟡 同上 |
| TreeSelect | ExecutionNodeType.Select | 🟡 同上 |
| TreeLoop | ExecutionNodeType.Loop | 🟡 同上 |
| TreeDelayNode | ExecutionNodeType.Delay | 🟡 同上 |
| TreeCondNode | ExecutionNodeType.Cond | 🟡 同上 |
| TreeActionNode | ExecutionNodeType.Action | 🟡 同上 |
| TreeScriptNode | ExecutionNodeType.Script | 🟡 同上 |
| TreeClearTarget/ClearTargetNode | ExecutionNodeType.ClearTarget | 🟡 同上 |
| TreeClearWaitNode | ExecutionNodeType.ClearWait | 🟡 同上 |
| TreeExecuteAnotherTree | ❌ 无定义 | 🔴 |
| TreePrintDebugInfoNode | ❌ 无定义 | 🔴 |
| TreeDebugHitCond | ❌ 无定义 | 🔴 |
| TreeDebugRandomDelay | ❌ 无定义 | 🔴 |
| TreeDebugRandomHitCond | ❌ 无定义 | 🔴 |
| Env / ScriptEnv | ❌ 无 | 🔴 |
| TriggerNodeTimeLine | ❌ 无 | 🔴 |

> **关键发现**：ExecutionNode.cs 定义了 10 种节点类型枚举和完整数据类，但**当前 NodeProgressor 只处理扁平 ExecutionEntry 列表**（Condition + Action），不做树形求值。TreeSequence/Select/Parallel/Loop 的全部求值逻辑未实现。AE 的 TriggerMgr 有完整的递归树求值引擎。

---

### 2.2 起手系统

| AEAssist | HiAuRo | 差异 |
|:---|:---|:---|
| IOpener : ISlotSequence, IScript | IOpener : ISlotSequence | **IOpener 不继承 IScript** |
| ISlotSequence: `List<SlotResolverData> Resolvers` | ISlotSequence: `List<Action<Slot>> Sequence` | **API 不同**：AE 用 SlotResolverData 列表（每个含 ISlotResolver + SlotMode），HiAuRo 用 Action<Slot> 委托列表 |
| OpenerMgr.StartCheck() 在 IOpener 上 | OpenerMgr.StartCheck() 在 ISlotSequence 上 | 🟢 一致 |
| OpenerMgr 有 PriorityEventMode 字段 | HiAuRo OpenerMgr 无 | 🟡 |
| AE CountDownHandler 绑定到 TriggerLineData | HiAuRo 独立 IPC 驱动的 CountDownHandler | 🟢（已补齐） |

---

### 2.3 ISlotSequence 接口差异（重要）

| AEAssist | HiAuRo |
|:---|:---|
| `List<SlotResolverData> Resolvers { get; }` | `List<Action<Slot>> Sequence { get; }` |
| 序列是 SlotResolverData 的**有序列表** | 序列是 Action\<Slot\> 委托的**列表** |
| 每个 ResolverData 自带 SlotMode | 每个 Action 只是构建 Slot 的 lambda |
| AI 引擎按顺序执行并判断 GCD/oGCD 窗口 | 调用者自己控制执行节奏 |

**差异影响**：AE 的 ISlotSequence 与 SlotResolverData 统一，序列中每个步骤可指定 GCD/oGCD。HiAuRo 的序列是裸 Action 列表，无法标记步骤的 SlotMode，灵活性降低。对于需要 GCD 约束的序列（如诗人歌曲切换），需要 ACR 作者在 Action 内部自己控制。

---

### 2.4 Spell 类差异

| AEAssist Spell 字段 | HiAuRo Spell 字段 | 差异 |
|:---|:---|:---|
| Id, Name, Type, TargetType, SpellCategory | Id, Name, Type, TargetType, SpellCategory | 🟢 |
| **SpellTargetLimit_HPType** | ❌ 无 | 🔴 HP 阈值过滤 |
| **SpellTargetLimit_JobType** | ❌ 无 | 🔴 职业过滤 |
| **SpellTargetType.MountedOnly** | ❌ 无 | 🟡 |
| **Ignore GCD** | DontUseGcdOpt | 🟡 功能类似 |
| ❌ 无 | WaitServerAcq | 🟡 HiAuRo 独有 |
| ❌ 无 | GetDynamicTarget | 🟡 HiAuRo 独有 |

**SpellTargetLimit** 是 AE 的 Spell.GetTarget() 中重要的过滤层：
- `SpellTargetLimit_HPType`：按血量百分比过滤（如只打 <20% HP 的目标）
- `SpellTargetLimit_JobType`：按职业过滤（如舞伴只选近战）

HiAuRo 的 `Spell.GetTarget()` 不含限制型过滤，留到 **Phase 6+ 补充**（PHASE5.1 明确标注）。

---

### 2.5 目标选择系统

| AEAssist TargetMgr/TargetSelector | HiAuRo | 差异 |
|:---|:---|:---|
| TargetMgr.cs — 5 文件子系统 | ITargetResolver.cs — 1 个接口 | 🔴 |
| TargetSelectorModule.cs — 可插拔模块 | ❌ 无 | 🔴 |
| TargetStat.cs — 目标排序/权重 | ❌ 无 | 🔴 |
| PositionalState.cs — 身位状态 | TargetHelper.IsBehind/IsFlanking | 🟡 |
| UnitData.cs — 单位数据容器 | ❌ 无 | 🟡 |
| ITargetResolver: `bool ResolveTarget(out IBattleChara)` | 同 | 🟢 |
| **多种 TargetResolver 实现** | ❌ 只有接口 | 🔴 |

**HiAuRo 的 ITargetResolver 只有接口定义**，缺少 AE 那样的多个具体实现（优先 HP 最低、优先读条、优先最近等）。

---

### 2.6 AI 循环

| AEAssist | HiAuRo | 差异 |
|:---|:---|:---|
| IAILoop → AILoop_Normal | 同 | 🟢 |
| IAILoop → AILoop_PVP | ❌ 无 | 🟡 (Phase 7+) |
| IAILoop → AILoop_Simulate | ❌ 无 | 🟡 |
| PVE_RunSlotHelper | ❌ 无（内联在 SlotExecutor） | 🟡 |
| AI.cs（每帧 check 优先级 → build → execute） | AIRunner.cs + AILoop_Normal | 🟢 |
| **CanInterrupt()** 判断 | OpenerMgr.CanInterrupt() | 🟢 |
| **Check() 不管窗口** | 同 | 🟢 |

---

### 2.7 热键系统

| AEAssist | HiAuRo | 差异 |
|:---|:---|:---|
| HotkeyManager.cs — 集中管理 | HotkeyHelper.cs — 集中管理 | 🟢 |
| HotkeyEventAttribute — Attribute 注册 | ❌ 无（手动 Register） | 🟡 |
| IHotkeyEventHandler — `Run(HotkeyConfig)` 返回 `bool` | 同 | 🟢 (已补齐) |
| IHotkeyResolver — Id/Label/DefaultKey/Execute | 同 | 🟢 |
| HotkeyResolver 实现（LB/NormalSpell/Potion/General/疾跑） | ❌ 无 | 🔴 |
| HotkeyWindow.cs — UI 面板 | Phase 5.3 Web UI | 🟢 |
| QtWindow.cs — QT 开关面板 | Phase 5.3 Web UI | 🟢 |
| HotkeyConfig 含 SpellId/Description | HotkeyConfig 含 Id/Label/Key/Enabled | 🟡 (字段不同) |

**注意**：HotkeyConfig 字段差异较大。AE 的 HotkeyConfig 包含 `SpellId` 和 `Description`，HiAuRo 的简化到 Id/Label/Key/Enabled。如果后续需要支持 QT 开关（如 `QT_AOE = F1`），需要扩充。

---

### 2.8 IRotationEntry / IRotationEventHandler

| AEAssist | HiAuRo | 差异 |
|:---|:---|:---|
| Build(settingFolder) → Rotation | 同 | 🟢 |
| **AE 有 `TargetJobs` 属性** | 同（已在 IRotationEntry） | 🟢 |
| **AE 有 `GetRotationUI()`** | 同（返回 IRotationUI） | 🟢 |
| **AE 有 `OnDrawSetting()`** | 同 | 🟢 |
| AE 有 `UseCustomUi` | 同 | 🟢 |
| AE 有事件处理器分两个接口 | HiAuRo 合并为一个 IRotationEventHandler | 🟢 |

**EventHandler 回调对齐**：

| AE Callback | HiAuRo Callback | 状态 |
|:---|:---|:---:|
| OnPreCombat | OnPreCombat | 🟢 |
| OnResetBattle | OnResetBattle | 🟢 |
| OnNoTarget | OnNoTarget | 🟢 |
| OnBattleUpdate(int) | OnBattleUpdate(int) | 🟢 |
| OnSpellCastSuccess(Slot, Spell) | OnSpellCastSuccess(Slot, Spell) | 🟢 |
| BeforeSpell(Slot, Spell) | BeforeSpell(Slot, Spell) | 🟢 |
| AfterSpell(Slot, Spell) | AfterSpell(Slot, Spell) | 🟢 |
| OnEnterRotation | OnEnterRotation | 🟢 |
| OnExitRotation | OnExitRotation | 🟢 |
| OnTerritoryChanged | OnTerritoryChanged | 🟢 |

全部 10 个回调对齐。

---

### 2.9 JobApi 职业快捷入口

| AEAssist (22 个，全职业) | HiAuRo (1 个) |
|:---|:---|
| JobApi_Astrologian | ❌ |
| JobApi_Bard | BRDHelp.cs ✅ |
| JobApi_BlackMage | ❌ |
| JobApi_BlueMage | ❌ |
| JobApi_Dancer | ❌ |
| JobApi_DarkKnight | ❌ |
| JobApi_Dragoon | ❌ |
| JobApi_GunBreaker | ❌ |
| JobApi_Machinist | ❌ |
| JobApi_Monk | ❌ |
| JobApi_Ninja | ❌ |
| JobApi_Paladin | ❌ |
| JobApi_Pictomancer | ❌ |
| JobApi_Reaper | ❌ |
| JobApi_RedMage | ❌ |
| JobApi_Sage | ❌ |
| JobApi_Samurai | ❌ |
| JobApi_Scholar | ❌ |
| JobApi_Summoner | ❌ |
| JobApi_Viper | ❌ |
| JobApi_Warrior | ❌ |
| JobApi_WhiteMage | ❌ |

AE 的 JobApi 提供了职业特有状态的一站式读取（如黑魔的火/冰状态、层数、通晓层数、天语计时器等）。HiAuRo 只有 BRD 的快速入口。其余 20 个职业完全无覆盖。

---

### 2.10 Helper 工具类

| AEAssist (46 个) | HiAuRo (5 个 Helper + Data 层) | 对比 |
|:---|:---|:---|
| GCDHelper | GCDHelper | 🟢 |
| SpellHelper | SpellHelper | 🟢 |
| TargetHelper | TargetHelper | 🟢 |
| PartyHelper | Data.Party | 🟢 (Data 层替代) |
| MoveHelper | Data.Self.IsMoving | 🟡 (仅 IsMoving) |
| HotkeyHelper | HotkeyHelper | 🟢 |
| ChatHelper | ❌ | 🟡 |
| ItemHelper | ❌ | 🟡 |
| JobHelper | ❌ | 🟡 |
| LogHelper | DService.Log | 🟢 |
| MathHelper | ❌ | 🟡 |
| TimeHelper | ❌ | 🟡 |
| RandomHelper | ❌ | 🟡 |
| TriggerLineHelper | ❌ | 🔴 |
| SpellExtension | SpellExtensions (IsAbility) | 🟡 |
| MacroHelper | ❌ | 🟡 |
| 其余 31 个 | ❌ | 🟡 |

AE 的 Hear 层范围远超 HiAuRo。HiAuRo 的设计是用 Data 层（Party + Objects 分区）替代 AE 的多个 Helper。但缺失 TriggerLineHelper（触发线坐标转换/JSON 序列化）、ChatHelper、ItemHelper、JobHelper 等。

---

### 2.11 MemoryApi 内存层

| AEAssist (50 个) | HiAuRo | 状态 |
|:---|:---|:---|
| 全部 MemoryApi/MemApi* | OmenTools DService | 🟢 **设计决策：不用 AE 的内存层** |

HiAuRo 通过 OmenTools 统一访问游戏数据，不做自己的内存 API。这是正确的架构分层。AE 的 MemoryApi 中一些值得注意的能力：
- `MemApiMove/MoveControl` — 自动移动。HiAuRo **不做自动跑位**
- `MemApiHack` — 动画锁/施法保护。HiAuRo 不需要
- `MemApiMapEffect` — 地图特效读取（如副本机制标记）。这是 TriggerCond_MapEffect 的实现基础

---

### 2.12 其他子系统

| AEAssist 子系统 | HiAuRo 对应 | 状态 |
|:---|:---|:---|
| **Avoid/ 碰撞规避**（GJK 算法） | ❌ | 🟢 设计排除 |
| **GUI/Tree/ 可视化树编辑器** | Phase 9 Web 编辑器 | 🟢 Phase 9 |
| **TriggerlineEditor/** | Phase 9 Web 编辑器 | 🟢 Phase 9 |
| **FFLogs/ 战斗记录集成** | ❌ | 🟢 后续 |
| **CactbotTimeline 导入** | ❌ | 🟢 后续 |
| **CloudACR/ 云端 ACR** | ❌ | 🟢 后续 |
| **DynamicComplie/ 运行时编译** | ❌ | 🟡 ACRLoader 用 ALC 加载 DLL |
| **Verify/ 许可证验证** | ❌ | 🟢 设计排除 |
| **Network/ 多人协同** | ❌ | 🟢 设计排除 |
| **Internal/ 内部工具**（DamageAnalyse 等） | ❌ | 🟢 Phase 8+ |

---

## 三、汇总：Phase 6 补齐后仍存的差距

### 🔴 严重缺失（ACR 作者当前无法使用的能力）

| 差距 | 影响 | 建议阶段 |
|:---|:---|:---|
| TriggerCond 只有 5/28 种 | 高难副本触发器大量场景无法覆盖 | Phase 6+ 按需补 |
| TriggerAction 只有 4/17 种 | 缺少 SpellQueue、LockSpell、MoveTo、SendKey 等关键动作 | Phase 6+ 按需补 |
| SpellTargetLimit 缺失 | 无法按 HP/职业过滤技能目标 | Phase 6+ |
| ITargetResolver 无具体实现 | ACR 无法使用内置目标选择器 | Phase 6+ |
| ExecutionNode 树求值未实现 | TriggerLine 只能做线性顺序，无法条件分支/并行 | Phase 7 |
| ISlotSequence API 不同 | AE 的 ACR 需要适配（Action<Slot> vs SlotResolverData） | Phase 6+（文档指引） |

### 🟡 中度差距（功能简化但可运作）

| 差距 | 影响 |
|:---|:---|
| JobApi 只有 BRD | 其余 20 职业 ACR 作者需自己写 Helper |
| 无 PvP/Simulate AI 循环 | 仅支持 PvE 主线 |
| HotkeyResolver 无内置实现 | 热键绑定需要 ACR 作者自己实现 Resolver |
| 无 TriggerLineHelper | 触发线创建无工具辅助 |
| HotkeyConfig 字段少 | 无法通过配置直接绑定技能 ID |
| ChatHelper / ItemHelper 缺失 | ACR 作者需自行调用 DService |

---

## 四、建议优先级

基于"让 ACR 作者能写出完整副本触发器"的目标：

1. **P0 — 立即补**：SpellTargetLimit（HP 过滤/职业过滤）、ITargetResolver 具体实现
2. **P1 — Phase 7 前**：TriggerAction 补齐 SpellQueue/LockSpell/SetRotation、TriggerCond 补齐图标/连线/特效
3. **P2 — Phase 7+**：ExecutionNode 树求值引擎、TriggerLineHelper/序列化
4. **P3 — 按需**：更多 JobApi、PvP AI 循环、其余 Trigger 类型

---

*Created: 2026-05-04*
*基于 AEAssist 1686 文件 vs HiAuRo 63 文件对比*
