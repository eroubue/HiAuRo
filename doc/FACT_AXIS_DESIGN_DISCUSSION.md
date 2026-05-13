# 事实轴 + 智能层：HiAuRo 的差异化设计探讨

> **状态**：Phase 7-8 核心功能已完成 ✅ | 文档版本：2026-05  
> **目标读者**：对 FFXIV PvE 自动化有兴趣的开发者、ACR 作者、副本研究者  
> **想表达的**：不仅是"做到了什么"，更是"为什么这么做""还想做什么"——**欢迎一起讨论**

---

## 目录

1. [背景与动机](#1-背景与动机)
2. [核心理念：三层分离](#2-核心理念三层分离)
3. [当前进度总览](#3-当前进度总览)
4. [已实现：事实轴 Fact Timeline](#4-已实现事实轴-fact-timeline)
5. [已实现：决策引擎 Decision Engine](#5-已实现决策引擎-decision-engine)
6. [已实现：智能引擎 Intelligence Engine](#6-已实现智能引擎-intelligence-engine)
7. [运行时集成全景](#7-运行时集成全景)
8. [欲实现：远景规划](#8-欲实现远景规划)
9. [开放讨论话题](#9-开放讨论话题)
10. [如何参与](#10-如何参与)

---

## 1. 背景与动机

### 现有工具的局限

FFXIV PvE 自动化领域已有不少成熟工具，但普遍存在两类问题：

**问题一：ACT 触发器 / BossMod — "只知道时间"**

这些工具本质是**时间轴回调**——"战斗第 8 秒，执行动作 X"。它们能告诉 ACR"该放减伤了"，但：
- 不会根据队伍组成自动分配（谁放？放什么？）
- 不会根据实际情况调整（时间漂移了？有人死了？）
- 输出的是"提示/建议"，而非明确的执行指令

**问题二：现有的时间线方案 — "重机制，轻决策"**

部分方案把大量精力花在"预测 Boss 下个技能"上，但对"队伍怎么用技能应对"缺乏系统化设计。减伤分配往往是硬编码的——"时间 T，指定职业放技能 X"。

### 我们想做什么

HiAuRo 的事实轴 + 智能层试图把问题拆成两层：

1. **事实轴（Fact Axis）回答"打到哪了"** — 一个被动的、只描述 Boss 行为的观测器
2. **智能层（Intelligence + Decision Layer）回答"怎么应对"** — 读事实、看队伍、做分配

这跟执行轴（Execution Axis）的思路完全不同：

| | 执行轴 (Phase 6) | 事实轴 + 智能层 (Phase 7-8) |
|---|---|---|
| **哲学** | 自上而下：脚本告诉 ACR 怎么做 | 自下而上：时间线描述事实，引擎自主决策 |
| **时间线角色** | 触发器 + 条件 → 动作 | 纯声明：时间 + 同步 + 需求描述 |
| **决策权** | 时间线编写者预定义一切 | 引擎根据实时队伍状态分配 |
| **典型用途** | 固定副本的精确操作控制 | 团队协作 + 自适应分配 |
| **相互排斥** | 是（两种模式互斥） | 是 |

---

## 2. 核心理念：三层分离

```
┌──────────────────────────────────────────────────┐
│                   事实层 (Fact Axis)                │
│  "Boss 在做什么？"                                    │
│  ────────────────────────────────                   │
│  时间线 JSON → 阶段推进 → 时钟校准 → Sync 匹配       │
│  输出: FactState（当前阶段/时间/事件/变量）           │
└──────────────────────┬───────────────────────────┘
                       │ 需求声明 (减伤30%, 治疗800)
                       ▼
┌──────────────────────────────────────────────────┐
│                决策层 (Decision Layer)              │
│  "队伍应该怎么应对？"                                  │
│  ────────────────────────────────                   │
│  读队伍组成 → 查技能注册表 → 贪心分配 → 强制执行     │
│  输出: DecisionOutput（分配列表 + 技能ID列表）        │
└──────────────────────┬───────────────────────────┘
                       │ 强制技能 / 策略开关
                       ▼
┌──────────────────────────────────────────────────┐
│                 智能层 (Intelligence Engine)         │
│  "角色应该站在哪？"                                    │
│  ────────────────────────────────                   │
│  事实事件触发 → 释放 MovementDemand → 站位/TP 指令   │
│  输出: ActiveDemands（MoveTo / TP / Hold）           │
└──────────────────────┬───────────────────────────┘
                       │ 移动指令
                       ▼
┌──────────────────────────────────────────────────┐
│                  ACR 执行层                         │
│  "我这个职业具体放什么技能"                             │
│  ────────────────────────────────                   │
│  接收强制技能 + 正常循环 + 移动指令                   │
└──────────────────────────────────────────────────┘
```

**关键设计原则**：

- **事实轴只描述"发生了什么"，不描述"该做什么"** — 一个 JSON 可以同时被减伤分配引擎和治疗分配引擎消费
- **时间线编写者和 ACR 作者不需要互相了解对方的工作** — 时间线写"此处需要 30% 减伤"，引擎自己决定用谁的策动还是行吟
- **决策引擎是开放的** — 当前是贪心算法，未来可以替换为优化求解器，不影响时间线格式

---

## 3. 当前进度总览

| 功能模块 | 状态 | 关键交付 |
|---------|------|---------|
| 事实轴核心引擎 | ✅ 已完成 | `FactTimeline.cs` (507行)：双时钟 + Sync 校准 + 分支切换 |
| 时间线 JSON 格式 | ✅ 已完成 | 嵌套阶段 → 事件 → 切换点 → 分支，含 `需求动作` |
| Sync 事件匹配 | ✅ 已完成 | `startsUsing`（读条开始）+ `ability`（技能效果） |
| 分支条件系统 | ✅ 部分 | `VariableCondition`（变量条件）已实现 |
| 决策引擎 | ✅ 已完成 | `DecisionEngine.cs` (206行)：贪心分配 + 冷却排序 |
| 技能注册表 | ✅ 框架 | `DecisionSkillRegistry`：内置 BRD/MNK/WHM |
| 智能引擎 | ✅ 基础 | 需求缓冲 + 事实事件触发释放 |
| 全职业技能数据 | ⏳ 社区建设中 | 见 [HiAuRo.Helper](https://github.com/denghaoxuan991876906/HiAuRo.Helper) |
| 移动执行器 | ⏳ 未实现 | 站位/TP 指令尚无执行下游 |
| 冷却预留系统 | ⏳ 未实现 | 当前只看即时 CD，未来考虑"预约" |
| 多职业协调器 | 📋 规划中 | COOP-01：跨职业统一调度 |
| 可视化编辑器 | ✅ 已完成 | `fact-editor.html`：纯前端编辑时间线 JSON |

---

## 4. 已实现：事实轴 Fact Timeline

### 4.1 核心机制

事实轴的核心是一个**双时钟 + Sync 校准**的状态机：

```
战斗开始
  │
  ├─ _timebase = 当前 Unix 毫秒（战斗锚点）
  │  FightNow = (DateTimeOffset.UtcNow - _timebase) / 1000.0
  │
  ├─ 加载阶段 (Phase) → 按预期时间推进事件
  │
  ├─ 遇到有 startSync 的事件：
  │    暂停时间推进，等待游戏事件匹配
  │    └─ 匹配成功 → SyncTo(targetTime) 调整 _timebase → 校准消除漂移
  │
  ├─ 遇到有 duration 的事件：
  │    等待 endSync 匹配（确认事件结束）
  │
  └─ 遇到阶段切换点 (FactPhaseSwitch)：
       Sync 触发 → 评估分支条件 → 替换事件列表 → 进入新阶段
```

**双时钟**：`PhaseTime`（阶段内秒数）和 `TotalTime`（战斗总秒数），分别用于 Debug 面板和状态输出。

**Sync 校准的意义**：网络延迟、开怪时机差异会导致时间漂移。通过匹配游戏事件（"Boss 确实开始读技能 X 了"），每次 Sync 命中都会校准时钟基准，确保 `FightNow` 趋近真实。

### 4.2 JSON 格式示例（极朱雀诗魂战）

```json
{
  "name": "极朱雀诗魂战",
  "territoryId": 297,
  "author": "HiAuRo 示例",
  "phases": [{
    "id": "p1",
    "name": "P1 开场",
    "events": [
      {
        "id": "pull",
        "name": "开战",
        "time": 0,
        "actions": [{ "type": "logMessage", "message": "极朱雀 战斗开始" }]
      },
      {
        "id": "raidwide",
        "name": "第一次全场AOE",
        "time": 8.0,
        "duration": 3.0,
        "startSync": {
          "type": "startsUsing",
          "abilityIds": [10589],
          "windowBefore": 10.0,
          "windowAfter": 5.0
        },
        "endSync": {
          "type": "ability",
          "abilityIds": [10589],
          "windowBefore": 2.5,
          "windowAfter": 2.5
        },
        "actions": [
          { "type": "demand", "需求减伤": 30, "需求治疗": 800 },
          { "type": "skillSuggestion", "skillId": 7561, "label": "策动 (10%减伤)", "priority": "high" }
        ]
      }
    ],
    "switch": {
      "sync": { "type": "startsUsing", "abilityIds": [10591] },
      "actions": [{ "type": "setVariable", "variableName": "phase1Completed", "value": true }],
      "branches": [{
        "name": "默认P2",
        "events": [ /* P2 事件列表 */ ],
        "switch": { /* P2 → P3 切换点 */ }
      }]
    }
  }]
}
```

### 4.3 JSON 结构速查

```
FactTimelineData
├── name, territoryId, author
└── phases: FactPhase[]
    ├── id, name
    ├── events: FactEvent[]
    │   ├── id, name, time, duration?
    │   ├── startSync? / endSync? : FactSyncDef
    │   │   ├── type: "startsUsing" | "ability" | "inCombat"
    │   │   ├── abilityIds: uint[]
    │   │   ├── windowBefore / windowAfter: 秒
    │   │   └── jump? / forceJump? : 跳转校准
    │   └── actions: FactAction[]
    │       ├── setVariable / toggleVariable / skillSuggestion
    │       ├── logMessage
    │       └── demand (需求减伤 + 需求治疗)
    └── switch?: FactPhaseSwitch
        ├── sync: FactSyncDef
        ├── actions: FactAction[]
        └── branches: FactSwitchBranch[]
            ├── condition?: FactCondition (VariableCondition)
            ├── events: FactEvent[]
            └── switch?: FactPhaseSwitch (递归嵌套)
```

### 4.4 运行时状态输出

每帧通过 `FactTimeline.State` 暴露以下实时信息：

```csharp
FactState {
    IsRunning           // 是否运行中
    PhaseName           // 当前阶段名
    PhaseTime           // 阶段内秒数
    TotalTime           // 校准后的战斗总秒数
    CurrentEvent        // 当前正在等待 Sync 的事件 (null=无)
    Status              // "running" | "waiting_sync" | "switching"
    NextEventTime       // 下个未到达事件的预期秒数
    Suggestions         // 即将到达事件的技能提示
    Variables           // 所有布尔变量的快照
    LastSyncInfo        // 最近一次 Sync 的调试信息
}
```

---

## 5. 已实现：决策引擎 Decision Engine

### 5.1 核心流程

```
事实事件触发 "demand" 动作 (需求减伤=30, 需求治疗=800)
  │
  ▼
DecisionEngine.计算(30, 800)
  │
  ├─ ① 扫描队伍：DService.PartyList → 获得 (Jobs, IsInParty) 列表
  │
  ├─ ② 减伤分配：
  │    ├─ 查 DecisionSkillRegistry.团队减伤表 [job]
  │    ├─ 过滤：冷却剩余 <= 0 的技能
  │    ├─ 排序：冷却短的优先（保守策略）
  │    └─ 贪心：依次累加至 >= 需求
  │
  ├─ ③ 治疗分配：
  │    └─ 同上逻辑，查 团队治疗表
  │
  └─ ④ 输出 DecisionOutput
       ├─ 减伤分配[] / 治疗分配[]（详细分配记录）
       ├─ 减伤合计 / 治疗合计
       ├─ 不足？（未满足需求）
       └─ 执行技能IDs[]（直接发给 AIRunner 强制释放）
```

**贪心策略的考量**：
- 按冷却升序排列（冷却短的道具先分配），确保高频技能优先消耗
- 允许超额分配（已分配 >= 需求就停），不追求精确匹配
- 当前不区分减伤类型（魔法/物理/全能），未来可扩展

### 5.2 技能注册表

```csharp
// 当前内置的技能数据
DecisionSkillRegistry.注册(Jobs.BRD,
    teamMit: [ 策动(7561, 10%, 90s), 行吟(7559, 10%, 90s) ],
    teamHeal: [ 光阴神的礼赞凯歌(7560, 200pot, 90s) ]
);

DecisionSkillRegistry.注册(Jobs.MNK,
    teamMit: [ 牵制(7549, 10%, 90s) ],
    personalMit: [ 内丹(3547, 0%, 120s) ]  // ← 已注册但尚未被引擎消费
);

DecisionSkillRegistry.注册(Jobs.WHM,
    teamMit: [ 节制(7433, 10%, 120s) ],
    teamHeal: [ 医济(124, 600pot, 60s), 全大赦(7434, 800pot, 60s) ]
);
```

**待完善的**：其余 18 个战斗职业的技能数据尚未注册。这部分社区贡献的空间很大——具体见 [HiAuRo.Helper](https://github.com/denghaoxuan991876906/HiAuRo.Helper) 仓库。

### 5.3 决策输出示例

```
需求: 减伤 30% + 治疗 800

队伍: BRD / MNK / DRG / WHM / ...

分配:
  减伤:
    策动 (BRD)        → 10%
    行吟 (BRD)        → 10%
    牵制 (MNK)        → 10%
    合计: 30% ✓

  治疗:
    医济 (WHM)        → 600pot
    光阴神的礼赞凯歌 (BRD) → 200pot
    合计: 800pot ✓

不足: false

执行技能IDs: [7561, 7559, 7549, 124, 7560]
```

---

## 6. 已实现：智能引擎 Intelligence Engine

### 6.1 定位

智能引擎是事实轴和移动系统之间的桥梁。它不直接控制角色，而是**释放需求**——告诉下游"现在需要站位/移动了"。

### 6.2 工作流程

```
外部来源（脚本 / IPC）写入 MovementDemand 到 DemandBuffer
           │  (线程安全 ConcurrentQueue)
           ▼
IntelligenceEngine.Update(timeline)
  │
  ├─ 读取事实轴的 State.CurrentEvent（当前等待 Sync 的事件）
  ├─ 从未释放过此事件？
  │    ├─ 从 DemandBuffer 按 FactNodeId 分组取需求
  │    ├─ 匹配成功 → 移动到 ActiveDemands
  │    └─ 从 DemandBuffer 清除
  └─ ActiveDemands 暴露给下游（未来的移动执行器）
```

### 6.3 MovementDemand 数据结构

```csharp
MovementDemand {
    string Id              // 唯一标识（短 GUID）
    string FactNodeId      // 关联的事实事件 ID（如 "raidwide"）
    DemandType Type        // MoveTo | TP | Hold
    Vector3? TargetPos     // 目标位置
    float? TargetHeading   // 目标朝向
    string TargetRole      // 目标职能 ("All" | "Tank" | "Healer" | "Dps")
    string Source          // 来源标签（调试用）
}
```

**现状**：需求释放逻辑已完成，但**尚无移动执行器消费 ActiveDemands**。未来需要实现路径规划、导航网格查询、角色移动控制。

---

## 7. 运行时集成全景

### 7.1 AIRunner 调度流程

```
AIRunner.Update() 每帧
  │
  ├─ ① 战斗状态检查 (Idle / Zoning → 跳过)
  ├─ ② Objects + Party 刷新
  ├─ ③ 目标自动选择 (TargetResolvers)
  ├─ ④ BattleTime 累加 + OnBattleUpdate 回调
  ├─ ⑤ CanPauseACRCheck
  │
  ├─ ⑥ 轴模式调度（互斥）:
  │   ├─ Mode == ExecutionAxis → ExecutionAxis.Update()
  │   │   └─ 可能产出: ForceSpell / ForceTarget / PauseAcr
  │   │
  │   └─ Mode == FactAxis → UpdateFactAxis():
  │       ├─ Start/Stop FactTimeline (按 InCombat 状态)
  │       ├─ FactTimeline.Update(battleTimeMs) → 推进时间 + Sync 匹配
  │       ├─ UpdateDecisions():
  │       │   └─ 从当前事件的 demand 动作提取 需求减伤/需求治疗
  │       │   └─ DecisionEngine.计算() → 强制执行分配的技能
  │       └─ IntelligenceEngine.Update(timeline) → 释放匹配的 MovementDemand
  │
  ├─ ⑦ AssistAxis.Update() — 始终运行（独立于模式）
  ├─ ⑧ Opener 序列
  ├─ ⑨ SpellQueue 待处理
  └─ ⑩ AILoop.GetNextSlot() — 正常 ACR 循环
```

### 7.2 模式互斥

```csharp
// ModeSwitch.cs
Mode { None, ExecutionAxis, FactAxis }

SetMode(newMode):
    先 Stop 旧模式 → 销毁资源
    → 设置新模式 → Start 初始资源
```

事实轴和执行轴**不能同时运行**。这是因为：
- 两者对待 ACR 的方式不同：执行轴是"强制指定技能"，事实轴是"需求声明 + 引擎分配"
- 同时运行会产生指令冲突（谁说了算？）
- 用户需要在开战前选择一种模式

### 7.3 与 ACR 的交互

在 FactAxis 模式下：
1. 决策引擎产出的 `执行技能IDs[]` 被 AIRunner 包装成 `Slot`，直接通过 `SlotExecutor.ExecuteSlot()` 执行
2. 这些**强制技能跳过 ACR 正常循环**——本帧不再调用 `AILoop.GetNextSlot()`
3. ACR 作者可以在 `Rotation` 中挂载 `CanUseHighPrioritySlotCheck` 来拒绝某些强制技能（例如读条中不允许插入）

---

## 8. 欲实现：远景规划

### 8.1 全职业技能注册 (HiAuRo.Helper)

当前决策引擎仅内置了 BRD/MNK/WHM 三个职业的技能数据，其余 18 个职业需要社区协作补充。

已有独立仓库 [HiAuRo.Helper](https://github.com/denghaoxuan991876906/HiAuRo.Helper) 提供 21 个职业（含青魔法师）的 Helper，但需要进一步与 DecisionSkillRegistry 的注册对接。

### 8.2 冷却预留系统 (CD Reservation)

**当前**：决策引擎只检查 `GetCooldownRemaining() > 0`（技能 CD 好了就用）。

**期望**：建立"技能预约"机制。例如：

```
T+8s: 第一次全场AOE — 需要减伤30%（分配了策动）
T+60s: 第二次全场AOE — 也需要减伤30%
```

如果策动的冷却时间是 90 秒，在 T+8s 用掉后，T+60s 时无法再用。引擎需要能**前瞻**：在分配 T+8s 的减伤时，就为 T+60s 留好备用技能。

**讨论点**：
- 预留粒度：按时间窗？按事件 ID？
- 动态解约：如果有人提前用了被预留的技能怎么办？
- 优先级：爆发期的减伤需求 > 普通期的减伤需求？

### 8.3 更多 Sync 类型

当前仅实现了 `startsUsing` 和 `ability` 两种 Sync。未来可以扩展：

| Sync 类型 | 匹配条件 | 典型用途 |
|----------|---------|---------|
| `startsUsing` | Boss 开始读条技能 X | 死刑预警 (**已实现**) |
| `ability` | Boss 成功放出技能 X | AOE 伤害确认 (**已实现**) |
| `inCombat` | 进入战斗 | 开怪触发 (**已定义，未实现**) |
| `hpThreshold` | Boss HP 低于阈值 | 转阶段判断 |
| `statusGain` | Boss 获得 Buff | 机制开启 |
| `castInterrupt` | 读条被打断 | 拉线/踢球等交互 |
| `partyDeath` | 队友死亡 | 容错分支 |

### 8.4 更丰富的条件系统

当前只有 `VariableCondition`（布尔变量检查）。可扩展：

```csharp
// 设想中的条件类型
public class HpThresholdCondition : FactCondition { /* Boss/自身 HP 百分比 */ }
public class TimeElapsedCondition : FactCondition { /* 本阶段已过 N 秒 */ }
public class PartyCountCondition : FactCondition { /* 存活人数 */ }
public class AndCondition : FactCondition { /* 条件组合 */ }
public class OrCondition : FactCondition { /* 条件组合 */ }
```

### 8.5 移动执行器

Intelligence Engine 已经能释放 `MovementDemand`，但缺少执行下游（"MoveTo (x, y, z)" → 角色真的走过去）。

需要考虑：
- 路径规划（导航网格 / NavMesh）
- 移动控制（是否干涉 ACR？是否仅在脱战/非 GCD 期间移动？）
- 传送（`TP` 类型）的实现机制
- 冲突处理（ACR 让你站这，移动指令让你站那）

### 8.6 多职业协作协调器 (COOP-01)

当前决策是**每台机器独立运行**——你自己的 HiAuRo 决定用什么技能，不和队友的 HiAuRo 通信。

**COOP-01 的设想**：
- 通过 IPC（Dalamud 插件间通信）或 WebSocket 共享决策信息
- 协调器知道"全队有什么可用的减伤"，统一分配
- 避免多个队友同时交大减伤（叠盾溢出）
- 优先级分配：短 CD 先交，长 CD 留关键节点

### 8.7 自适应兜底策略 (AI-04)

当事实轴因为意外（有人提前触发、团灭、拉脱）而偏离预期时，需要：
- 检测到严重偏离 → 自动回退到无时间轴模式
- 或尝试重新校准（跳转到最近的 Sync 点）
- 或切换到备选时间线（比如"低输出策略"）

---

## 9. 开放讨论话题

以下是我们在设计过程中遇到的具体问题，欢迎任何感兴趣的开发者一起探讨：

### 话题 1：冷却预留系统怎么设计？

> 决策引擎当前只看"技能 CD 好了吗"，没有前瞻。如果要为未来事件预留技能，数据结构应该怎么设计？预留时机如何确定？预留的"放弃"条件是什么？

### 话题 2：Sync 类型还需要哪些？

> 当前 `startsUsing` + `ability` 已经覆盖大部分 PvE 场景，但边缘案例（如绝本的特殊机制、单人副本 Boss）是否还需要新的 Sync 类型？`inCombat` 目前定义了但未实现——它真的有用吗？还是 `time=0` 已经够了？

### 话题 3：决策引擎的"最优解"问题

> 当前贪心算法（冷却短的优先）简单但未必最优。有没有场景需要精确求解（最小化技能浪费、最大化覆盖率）？如果要换线性规划/DP，性能开销怎么控制？（毕竟每帧都要跑）

### 话题 4：治疗分配是否应该区分 HoT 和直疗？

> 当前治疗分配只看"恢复力"，不区分 HoT 和直疗。但实际上，HoT 更适合持续掉血阶段，直疗更适合瞬间伤害。是否需要在 `demand` 中加入治疗类型偏好？还是让决策引擎自动匹配？

### 话题 5：移动需求和 ACR 循环如何协调？

> 当智能引擎释放 `MoveTo` 需求时，ACR 的正常循环是否应该暂停？还是并行？如果需要面向 Boss 放技能但指令要求移动，如何仲裁？

### 话题 6：单/多人决策的边界在哪里？

> 单人 HiAuRo 的决策引擎已经在工作。如果要升级为 COOP（多人协调），需要在哪一环做适配？目前 `DecisionEngine.计算()` 的输入是"自身看到的队伍"，如果输入变成"全队共享的技能状态"，算法本身还需要变吗？

---

**讨论方式**：欢迎在 [GitHub Discussions](https://github.com/anomalyco/opencode) 或提交 Issue / PR 参与讨论。中文/英文均可。

---

## 10. 如何参与

### 时间线贡献

如果你熟悉某个副本的 Boss 时间轴，可以编写 JSON 时间线文件，放在 `FactTimelines/{territoryId}.json`。

### 技能数据贡献

如果你熟悉某个战斗职业，可以为 [HiAuRo.Helper](https://github.com/denghaoxuan991876906/HiAuRo.Helper) 补充 `DecisionSkillRegistry` 的技能注册数据：
- 团队减伤技能（名称、ID、减伤%、CD 时间、类型）
- 单人减伤技能
- 团队治疗技能（恢复力、是否 HoT、范围）
- 建议以 PR 方式提交

### 核心开发

如果你想参与 Fact Axis / Decision / Intelligence 核心代码的开发（新增 Sync 类型、优化分配算法、实现移动执行器等），请先查看 [ROADMAP.md](./ROADMAP.md) 了解全局规划，然后在 Discussion 中讨论方案。

### 相关仓库

| 仓库 | 说明 |
|------|------|
| HiAuRo (本仓库) | 框架主体，Fact Axis + Decision + Intelligence |
| [HiAuRo.Helper](https://github.com/denghaoxuan991876906/HiAuRo.Helper) | 全职业技能数据辅助库 |
| [AEAssist](https://github.com/denghaoxuan991876906/AEAssist) | AEAssist (ACR 框架参考) |
| [Oblivion](https://github.com/denghaoxuan991876906/Oblivion) | BLM ACR 示例 |

---

> **最后的话**：事实轴和智能层的设计还很年轻。当前跑通了"JSON 时间线 → Sync 校准 → 需求声明 → 贪心分配 → 强制执行"的完整链路，但很多细节等待打磨。如果你对"AI 辅助团队协作"这个方向有兴趣——无论是时间线编写、技能数据整理、算法设计还是奇思妙想——欢迎加入讨论。
