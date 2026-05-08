# HiAuRo ↔ AEAssist 对齐计划

> 基于 `AEASSIST_GAP_CHECK.md` 的差距分析，逐项制定补齐方案。
> 原则：**对齐 AE 能力，不改 HiAuRo 架构**；按优先级分 4 批执行。

---

## P0 — 立即补齐 ✅ 全部完成

所有 P0 项已在 Phase 6 实现：

| 项 | 状态 |
|:---|:---:|
| P0-2: ITargetResolver 5 种实现 | ✅ 已实现 |
| P0-3: HotkeyResolver 4 种实现 (技能/吃药/极限技/疾跑) | ✅ 已实现 |
| P0-4: HotkeyConfig 字段扩充 (SpellId/Description) | ✅ 已实现 |
| P0-1: SpellTargetLimit (HP/职业过滤) | ✅ 已实现 — SpellTargetLimit.cs + SpellTargetLimit_HP.cs + SpellTargetLimit_Job.cs 存在, Spell.cs GetTarget() 已集成过滤 |

---

### P0-1: SpellTargetLimit（技能目标过滤）

**差距**：AE 有 HP 阈值过滤（`SpellTargetLimit_HPType`）和职业过滤（`SpellTargetLimit_JobType`），HiAuRo 的 `Spell.GetTarget()` 不含任何限制型过滤。

**方案**：

```
新建: ACR/SpellTargetLimit.cs           ← 过滤条件枚举 + 数据结构
新建: ACR/SpellTargetLimitHP.cs         ← HP 阈值过滤（低于/高于/百分比）
新建: ACR/SpellTargetLimitJob.cs        ← 职业角色过滤（仅T/仅近战等）
修改: ACR/Spell.cs                      ← GetTarget() 中接入过滤逻辑
```

**Spell 新增字段**：
```csharp
public SpellTargetLimit[]? TargetLimits { get; init; }
```

**SpellTargetLimit 设计**：
```csharp
public enum SpellTargetLimitType { HP, JobRole, IsCasting, HasEffect }

public abstract class SpellTargetLimit(TargetLimitType type)
{
    public bool Filter(IGameObject target);
}

// HP 过滤: SpellTargetLimitHP = new(Mode.Below, 0.2f) → 只打 <20% HP
// 职业过滤: SpellTargetLimitJob = new(JobRole.Melee) → 只选近战
```

**验收**：ACR 作者可在 Spell 构造时附加 TargetLimits，`Spell.GetTarget()` 自动过滤。

---

### P0-2: ITargetResolver 具体实现

**差距**：只有空接口，缺 AE 那样的多种内置目标选择器。

**方案**：

```
新建: ACR/TargetResolvers/TargetResolver_最近敌人.cs
新建: ACR/TargetResolvers/TargetResolver_读条敌人.cs
新建: ACR/TargetResolvers/TargetResolver_最低HP敌人.cs
新建: ACR/TargetResolvers/TargetResolver_按DataId.cs
新建: ACR/TargetResolvers/TargetResolver_最佳AOE位置.cs
```

**接口对照**：
```csharp
// 保持现有接口不变
public interface ITargetResolver
{
    bool ResolveTarget(out IBattleChara agent);
}

// 实现例:
public sealed class TargetResolver_最近敌人(bool ignoreMoving = false) : ITargetResolver
{
    public bool ResolveTarget(out IBattleChara agent) { ... }
}

public sealed class TargetResolver_读条敌人(uint spellId) : ITargetResolver { ... }
public sealed class TargetResolver_最低HP敌人(float hpThreshold = 1f) : ITargetResolver { ... }
public sealed class TargetResolver_按DataId(uint dataId) : ITargetResolver { ... }
```

**验收**：ACR 作者可通过 `Rotation.AddTargetResolver(...)` 注册，AIRunner 在无目标时从 Resolvers 列表取第一个成功的。

---

### P0-3: HotkeyResolver 内置实现

**差距**：`IHotkeyResolver` 接口存在但无内置实现。AE 提供了 LB/NormalSpell/Potion/General/疾跑 5 种。

**方案**：

```
新建: ACR/HotkeyResolvers/HotkeyResolver_技能.cs        ← 按下释放指定技能
新建: ACR/HotkeyResolvers/HotkeyResolver_极限技.cs       ← LB 释放
新建: ACR/HotkeyResolvers/HotkeyResolver_吃药.cs         ← 爆发药
新建: ACR/HotkeyResolvers/HotkeyResolver_疾跑.cs         ← 疾跑
```

```csharp
public sealed class HotkeyResolver_技能(string id, string label, uint spellId, string defaultKey)
    : IHotkeyResolver
{
    public string Id => id;
    public string Label => label;
    public string DefaultKey => defaultKey;
    public void Execute() =>
        UseActionManager.Instance().UseAction(ActionType.Action, spellId, ...);
}
```

**验收**：`/hi HotkeyResolver_技能 add Pot_爆发药 39727 F1` 可注册热键。

---

### P0-4: HotkeyConfig 字段扩充

**差距**：HiAuRo 的 `HotkeyConfig` 缺 `SpellId` 和 `Description`，无法直绑技能。

**方案**：

```
修改: ACR/HotkeyConfig.cs
```

```csharp
public sealed class HotkeyConfig
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string Key { get; set; } = "";
    public bool Enabled { get; set; } = true;
    // ↓ P0 新增
    public uint SpellId { get; init; }        // 绑定的技能 ID（0=无）
    public string Description { get; init; } = ""; // 描述（悬浮提示用）
}
```

> 注意：向后兼容，新字段有默认值。

---

## P1 — Phase 7 前补齐（Medium，增加副本覆盖）✅ P1 全部完成

---

### P1-1: TriggerCond 补齐 ✅ 已完成（18/18）

**状态**：Phase 6 已实现全部 18 种计划 TriggerCond，超前完成 P1 目标。

**按依赖和实现难易度排列**：

| 序号 | 文件 | AE 对应 | 依赖 |
|:---|:---|:---|:---|
| 1 | `TriggerCond_目标图标.cs` | ActorControlTargetIcon | 需 HeadMarker 读取 |
| 2 | `TriggerCond_连线.cs` | ActorControlTether | 需 Tether 读取 |
| 3 | `TriggerCond_检查目标图标.cs` | CheckTargetIcon | 需 Aura 判断 |
| 4 | `TriggerCond_上次技能.cs` | CheckLastSpell | EventSystem 已有 |
| 5 | `TriggerCond_技能冷却.cs` | CheckSpellCd | CooldownHelper 已有 |
| 6 | `TriggerCond_收到技能效果.cs` | ReceviceAbilityEffect | 需 Hook ActionEffect |
| 7 | `TriggerCond_地图特效.cs` | MapEffect | 需 MapEffect 读取 |
| 8 | `TriggerCond_游戏日志.cs` | GameLog | 需 ChatLog Hook |
| 9 | `TriggerCond_天气变化.cs` | OnWeatherIdChanged | 需 Weather 读取 |
| 10 | `TriggerCond_单位可选中.cs` | AfterUnitIsTargetable | Data.Objects 已有 |
| 11 | `TriggerCond_单位移除.cs` | AfterUnitRemove | Data.Objects 已有 |
| 12 | `TriggerCond_等待目标.cs` | WaitTarget | Data.Objects 已有 |
| 13 | `TriggerCond_倒计时开始.cs` | BeforeBattleTime | CountDown IPC 已有 |

**共需新建 13 个文件**，放在 `Execution/Triggers/Cond/`。

**新增 CondParams 类**（与条件一一对应）：

| Params 文件 | 字段 |
|:---|:---|
| `CondParams_目标图标.cs` | `uint IconId` |
| `CondParams_连线.cs` | `uint TetherId` |
| `CondParams_检查目标图标.cs` | `uint IconId, bool OnSelf` |
| `CondParams_上次技能.cs` | `uint SpellId` |
| `CondParams_技能冷却.cs` | `uint SpellId, float RemainingSec` |
| `CondParams_收到技能效果.cs` | `uint SpellId, ReceiveAbilityLimitType` |
| `CondParams_地图特效.cs` | `uint EffectId` |
| `CondParams_游戏日志.cs` | `string MessagePattern, bool Regex` |
| `CondParams_天气变化.cs` | `byte WeatherId` |
| `CondParams_单位可选中.cs` | `uint DataId` |
| `CondParams_单位移除.cs` | `uint DataId` |
| `CondParams_等待目标.cs` | `uint DataId, int TimeoutMs` |
| `CondParams_倒计时开始.cs` | `float TimeLeftSec` |

**验收**：每个条件独立可实例化并正确检测游戏状态。

---

### P1-2: TriggerAction 补齐 ✅ 已完成（10/10）

**状态**：Phase 6 已实现全部 10 种计划 TriggerAction，超前完成 P1 目标。

**已完成**：技能队列、高优Slot、锁定技能、设置Rotation、发送命令、发送按键 — 全部 6 种计划实现均已完成。与 P0 已有的 4 种合计 10 种。

| 序号 | 文件 | AE 对应 | 依赖 |
|:---|:---|:---|:---|
| 1 | `TriggerAction_技能队列.cs` | SpellQueue | SpellQueue 已有 |
| 2 | `TriggerAction_高优Slot.cs` | HighPrioritySlot | AIRunner 已有 |
| 3 | `TriggerAction_锁定技能.cs` | LockSpell | 需 Lock 状态位 |
| 4 | `TriggerAction_设置Rotation.cs` | SetRotation | 需 RotationManager |
| 5 | `TriggerAction_发送命令.cs` | SendCommand | CommandMgr 已有 |
| 6 | `TriggerAction_发送按键.cs` | SendKey | 需 KeyPress 注入 |

无需新建 CondParams（Action 不带 Params）。

**方案**：
```
新建: Execution/Triggers/Action/TriggerAction_技能队列.cs
新建: Execution/Triggers/Action/TriggerAction_高优Slot.cs
新建: Execution/Triggers/Action/TriggerAction_锁定技能.cs
新建: Execution/Triggers/Action/TriggerAction_设置Rotation.cs
新建: Execution/Triggers/Action/TriggerAction_发送命令.cs
新建: Execution/Triggers/Action/TriggerAction_发送按键.cs
```

**验收**：所有 6 种可独立创建并执行。

---

### P1-3: ISlotSequence API 对齐 ✅ 已完成

**差距**：AE 用 `List<SlotResolverData> Resolvers`，HiAuRo 用 `List<Action<Slot>> Sequence`。需要保留 Action<Slot> 模式（更灵活）的同时支持 SlotResolverData 模式（AE 兼容）。

**方案**：在 ISlotSequence 接口并行增加 `Resolvers` 属性，不改现有 `Sequence`。

```
修改: ACR/ISlotSequence.cs
```

```csharp
public interface ISlotSequence
{
    /// HiAuRo 原生方式：Action<Slot> 委托构建序列
    List<Action<Slot>> Sequence { get; }
    
    /// AE 兼容方式：SlotResolverData 列表（可选实现，二选一）
    List<SlotResolverData>? Resolvers => null;  // default 实现
    
    int StartCheck();
    int StopCheck(int index);
}
```

**验收**：AE 的 ACR 作者迁移时可直接用 `Resolvers` 方式，HiAuRo 原生用 `Sequence` 方式，AILoop_Normal 两种都支持。

---

### P1-4: AIRunner 支持 TargetResolvers 自动调度 ✅ 已完成

**状态**：`AIRunner.TryResolveTarget()` 已实现，无目标时从 `Rotation.TargetResolvers` 列表取第一个成功的。

**方案**：

```
修改: Runtime/AIRunner.cs
```

在 `OnNoTarget()` 之后插入目标选择逻辑：
```csharp
if (Data.Target.Current == null)
{
    // 尝试通过 TargetResolvers 自动选择目标
    if (CurrentRotation?.TargetResolvers != null)
    {
        foreach (var resolver in CurrentRotation.TargetResolvers)
        {
            if (resolver.ResolveTarget(out var target))
            {
                TargetManager.Target = target;
                break;
            }
        }
    }
    // 仍然无目标 → 触发 OnNoTarget
    if (Data.Target.Current == null)
    {
        CurrentRotation?.EventHandler?.OnNoTarget();
        return;
    }
}
```

**验收**：ACR 注册 `TargetResolver_最近敌人` 后，进战无目标时自动选中最近敌人。

---

## P2 — Phase 7 执行（树求值 + 序列化）✅ P2 全部完成 — AST引擎(async Task) + ExecutionJson序列化已在Phase 6实现, 超出计划

---

### P2-1: ExecutionNode 树求值引擎

**差距**：10 种节点类型枚举已定义，但 NodeProgressor 只处理扁平 ExecutionEntry 列表。

**方案**：在 `NodeProgressor` 中新增 `EvaluateTree(ExecutionNode)` 递归求值。

```
修改: Execution/NodeProgressor.cs
```

**节点求值语义**（对齐 AE）：

| 节点 | 求值规则 |
|:---|:---|
| `Sequence` | 顺序执行子节点，任一失败则整体失败 |
| `Parallel` | 同时执行所有子节点，全部成功才成功 |
| `Select` | 先检查 Condition，为 true 执行子节点，为 false 执行 ElseBranch |
| `Loop` | 循环执行子节点 LoopCount 次（-1 = 无限） |
| `Delay` | 等待 DelayMs 毫秒后执行子节点 |
| `Cond` | 调用 ITriggerCond.Handle() → 返回成功/失败 |
| `Action` | 调用 ITriggerAction.Handle() → 返回成功/失败 |
| `Script` | 执行 ScriptCode（C# 脚本字符串） |
| `ClearTarget` | 清除强制目标 |
| `ClearWait` | 清除等待状态 |

```csharp
// NodeProgressor 新增：
public NodeEvalResult EvaluateTree(ExecutionNode node)
{
    switch (node.Type)
    {
        case ExecutionNodeType.Sequence:
            foreach (var child in node.Children)
            {
                var result = EvaluateTree(child);
                if (result != NodeEvalResult.Success)
                    return result;
            }
            return NodeEvalResult.Success;
        // ... 其余 9 种
    }
}
```

**验收**：可创建嵌套树并在游戏中逐帧求值，调试信息显示当前节点路径。

---

### P2-2: TriggerLine JSON 序列化/反序列化

**差距**：TriggerLine 当前只能代码构建，无法持久化。

**方案**：

```
新建: Execution/TriggerLineSerializer.cs
```

**JSON Schema 示例**（单条 TriggerLine）：
```json
{
  "id": "p8s_p2_high_concept",
  "name": "P8S 本体 至高概念",
  "loop": false,
  "enabled": true,
  "entries": [
    {
      "id": "wait_5s",
      "condition": { "type": "经过时间", "timeMs": 5000 },
      "action": { "type": "释放技能", "spellId": 7408, "targetType": "Self" },
      "delayMs": 0
    }
  ]
}
```

**方案**：
- 每个 TriggerCond/Action 需要自己的 JSON 序列化键名
- 序列化器维护类型注册表：typeName → Type
- 与 Phase 9 编辑器共用格式

**新增接口**：
```csharp
// Execution/ITriggerSerializer.cs
public interface ITriggerSerializer
{
    string TriggerTypeName { get; }  // "经过时间" / "释放技能"
    object Serialize(ITriggerBase trigger);
    ITriggerBase Deserialize(JsonElement json);
}
```

**验收**：可 JSON `{ "type": "经过时间", "timeMs": 5000 }` → `new TriggerCond_经过时间(5000)` 双向转换。

---

## P3 — 按需补齐（Phase 7+/后续）🟡 P3 部分完成 (1/5 完成, 2/5 差距, 2/5 未做)

---

### P3-1: 剩余 TriggerCond（18 → 28）

补完 AE 全部 28 种条件。

| 序号 | 文件 | 说明 |
|:---|:---|:---|
| 14 | `TriggerCond_ACT监听.cs` | ACT 战斗日志监控 |
| 15 | `TriggerCond_Omega循环.cs` | 欧米茄特殊逻辑 |
| 16 | `TriggerCond_检查职能.cs` | 检查自己职能（T/H/D） |
| 17 | `TriggerCond_最近连线.cs` | 最近 N 秒内有过连线 |
| 18 | `TriggerCond_角色类型.cs` | 检查角色类型（玩家/NPC/宠物） |
| 其余 | 视需求补充 | |

---

### P3-2: 剩余 TriggerAction（10 → 14）

| 序号 | 文件 | 说明 |
|:---|:---|:---|
| 7 | `TriggerAction_移动.cs` | 移动到指定坐标 |
| 8 | `TriggerAction_简易TP.cs` | 简易传送 |
| 9 | `TriggerAction_施法中TP.cs` | 读条中传送 |
| 10 | `TriggerAction_变量操作.cs` | 脚本变量读写 |

---

### P3-3: JobApi 覆盖 21 职业 ✅ 已完成 — HiAuRo.Helper 独立仓库, 21职业全Helper (AST/BLM/BRD/.../WHM)

**差距**：仅 BRDHelp.cs。需要为其余 20 职业创建 XXHelp.cs。

**方案**：为每个战斗职业在 `Data/Jobs/` 创建 `XXHelp.cs`，提供职业特有状态一键读取。

```
新建: Data/Jobs/PLDHelp.cs, WARHelp.cs, DRKHelp.cs, GNBHelp.cs
新建: Data/Jobs/WHMHelp.cs, SCHHelp.cs, ASTHelp.cs, SGEHelp.cs
新建: Data/Jobs/MNKHelp.cs, DRGHelp.cs, NINHelp.cs, SAMHelp.cs, RPRHelp.cs, VPRHelp.cs
新建: Data/Jobs/BLMHelp.cs, SMNHelp.cs, RDMHelp.cs, PCTHelp.cs
新建: Data/Jobs/MCHHelp.cs, DNCHelp.cs  (BRDHelp 🟢)
```

每个 Help 文件参照 BRDHelp 的模式：

```csharp
// 例: BLMHelp.cs
public static class BLMHelp
{
    // 火状态
    public static bool IsInAstralFire => ...;
    public static int AstralFireStacks => ...;
    // 冰状态
    public static bool IsInUmbralIce => ...;
    public static int UmbralIceStacks => ...;
    public static int UmbralHearts => ...;
    // 资源
    public static int PolyglotStacks => ...;
    public static long EnochianTimer => ...;
    public static bool IsParadoxReady => ...;
    public static int AstralSoulStacks => ...;
}
```

**验收**：`BLMHelp.IsInAstralFire` → true/false。

---

### P3-4: AILoop_PVP + AILoop_Simulate

**差距**：只有 PvE 循环。

**方案**：
```
新建: Runtime/AILoop_PVP.cs     ← PvP 循环（参考 AE AILoop_PVP）
新建: Runtime/AILoop_Simulate.cs ← 模拟/训练循环（可选）
```

---

### P3-5: TriggerLineHelper

**差距**：缺 AE 的触发线工具类（JSON ↔ 触发线转换、触发线验证）。

**方案**：
```
新建: Execution/TriggerLineHelper.cs
```

提供：
- `FromJson(string json)` → `TriggerLine`
- `ToJson(TriggerLine line)` → `string`
- `Validate(TriggerLine line)` → `List<string>` errors

---

## 执行路线图

```
当前: Phase 6 完成 ✅
  │
  ├── P0 ✅ (Phase 6.x, 1-2周)
  │   ├── SpellTargetLimit ✅
  │   ├── ITargetResolver × 5 ✅
  │   ├── HotkeyResolver × 4 ✅
  │   └── HotkeyConfig 扩充 ✅
  │
  ├── P1 ✅ (Phase 6.x, 2-3周)
  │   ├── TriggerCond × 13 ✅
  │   ├── TriggerAction × 6 ✅
  │   ├── ISlotSequence 双模式 ✅
  │   └── AIRunner TargetResolver 接入 ✅
  │
  ├── P2 ✅ (Phase 6.x)
  │   ├── ExecutionNode 树求值 ✅ (AST async Task)
  │   └── TriggerLine JSON 序列化 ✅ (ExecutionJson)
  │
  └── Phase 7/8
      └── P3 按需 (1/5 完成, 2/5 差距, 2/5 未做)
          ├── JobApi × 20 ✅ (HiAuRo.Helper)
          ├── PvP/Simulate AI ⏳
          └── 其余 Trigger 类型 ⏳
```

---

## 文件统计

| 阶段 | 新建 | 修改 | 总行数(估) |
|:---|:---|:---|:---|
| P0 | 13 | 3 | ~600 |
| P1 | 19 | 2 | ~1200 |
| P2 | 1 + N | 1 | ~800 |
| P3 | 20+ | 2 | ~3000+ |

---

*Updated: 2026-05-08*
*对齐目标: AEAssist 1686 文件能力 → HiAuRo 最小实现原则*
