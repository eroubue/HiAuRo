# 事实轴全链路加固设计

> 状态：设计完成 | 日期：2026-05-16
> 目标：补齐事实轴流程缺口，ACR + 辅助轴 + 事实轴 + 决策层 + 智能层 全链路可测试跑通

---

## 1. 问题诊断

深度代码扫描后，确认 3 个 Bug + 3 项缺失能力 + 2 项基础设施缺位。

### 1.1 当前链路

```
战斗开始 → FactTimeline.Update() → Sync校准 → CurrentEvent(需求动作)
  → DecisionEngine.计算() → 贪心分配 → SlotExecutor.ExecuteSlot()
  → IntelligenceEngine.Update() → 释放MovementDemand → ❌无消费者
```

### 1.2 已确认缺口

| # | 类型 | 问题 | 位置 |
|---|------|------|------|
| B1 | Bug | DecisionEngine 构造的 Spell 对象缺属性 | AIRunner.cs:412 |
| B2 | Bug | `IntelligenceEngine.Reset()` 从未被调用 | AIRunner.cs:302 |
| B3 | Bug | `UpdateDecisions()` 每帧重复处理同一 demand event | AIRunner.cs:385-419 |
| QT | 缺失 | 事实轴无法调控 ACR 输出节奏（QT） | 新功能 |
| MV | 缺失 | MovementDemand 无执行下游 | 新模块 |
| E1 | 缺失 | FactAxis 功能无独立开关，全有或全无 | 新功能 |
| E2 | 缺失 | 切图时仅执行轴自动切换，事实轴需手动 | ModeSwitch.cs |

---

## 2. Bug 修复设计

### 2.1 B1 — FactSpellTable 独立技能执行数据表

**问题**：`SlotExecutor` 依赖 Spell 的 `TargetType`/`SpellCategory`/`Type` 属性，当前 `UpdateDecisions` 构造的 `new Spell { Id = skillId }` 缺这些。

**方案**：新增 `FactAxis/FactSpellTable.cs`，独立于 `DecisionSkillRegistry`。

```csharp
public static class FactSpellTable
{
    private static readonly Dictionary<uint, SpellExecutionInfo> _table = new();

    public static void 注册(uint id, string name,
        SpellTargetType target = SpellTargetType.Self,
        SpellCategory category = SpellCategory.Default,
        SpellType type = SpellType.Ability)
    {
        _table[id] = new SpellExecutionInfo { Id = id, Name = name,
            TargetType = target, SpellCategory = category, Type = type };
    }

    public static Spell? 构造Spell(uint id)
    {
        if (!_table.TryGetValue(id, out var info)) return null;
        return new Spell { Id = info.Id, Name = info.Name,
            TargetType = info.TargetType, SpellCategory = info.SpellCategory, Type = info.Type };
    }
}

public sealed record SpellExecutionInfo
{
    public uint Id { get; init; }
    public string Name { get; init; } = "";
    public SpellTargetType TargetType { get; init; } = SpellTargetType.Self;
    public SpellCategory SpellCategory { get; init; } = SpellCategory.Default;
    public SpellType Type { get; init; } = SpellType.Ability;
}
```

**开发者示例**（与内置 DecisionSkillRegistry 数据对应）：

```csharp
// BRD
FactSpellTable.注册(7561, "策动");
FactSpellTable.注册(7559, "行吟");
FactSpellTable.注册(7560, "光阴神的礼赞凯歌");
// MNK
FactSpellTable.注册(7549, "牵制");
FactSpellTable.注册(3547, "内丹");
// WHM
FactSpellTable.注册(7433, "节制");
FactSpellTable.注册(124, "医济");
FactSpellTable.注册(7434, "全大赦");
```

**两个表的关系**：
- `DecisionSkillRegistry` — 存"技能有什么用"（减伤%/恢复力/冷却秒/持续秒）
- `FactSpellTable` — 存"技能怎么放"（TargetType/类别/类型）
- 开发者需同时填充两个表

**执行处修改** (`AIRunner.UpdateDecisions`)：
```csharp
// 旧
slot.Add(new Spell { Id = skillId, Name = "决策技能", Type = SpellType.Ability, TargetType = SpellTargetType.Self });

// 新
var spell = FactSpellTable.构造Spell(skillId);
if (spell != null) slot.Add(spell);
```

---

### 2.2 B2 — IntelligenceEngine.Reset() 漏调用

**问题**：`IntelligenceEngine.Reset()` 清空 `ActiveDemands` 和 `_releasedFactNodeIds`，但全文零处调用。

**修复**：`AIRunner.Reset()` 末尾追加：

```csharp
public void Reset()
{
    // ... 现有代码 ...
    IntelligenceEngine.Instance.Reset();
    MovementExecutor.Instance.Reset();  // 新增
}
```

---

### 2.3 B3 — UpdateDecisions 需求分治 + 去重

**问题**：
1. `需求动作` 把减伤和治疗绑在一起，触发逻辑不同
2. CurrentEvent 不变时每帧重复调用 `DecisionEngine.计算()` + `SlotExecutor.ExecuteSlot()`
3. 无去重

**方案**：

#### 2.3.1 JSON 拆分

```json
// 旧（废弃，保留兼容）
{ "type": "demand", "需求减伤": 30, "需求治疗": 800 }

// 新
{ "type": "需求减伤", "value": 30 }
{ "type": "需求治疗", "value": 800 }
```

#### 2.3.2 数据模型 (`FactNode.cs`)

```csharp
// 拆分原有 需求动作 为两个独立类型
public class 需求减伤动作 : FactAction
{
    public int Value { get; set; }
    // Execute: 空，由 DecisionEngine 消费
}

public class 需求治疗动作 : FactAction
{
    public int Value { get; set; }
    // Execute: 空，由 DecisionEngine 消费
}
```

原有 `需求动作` 保留，标记 `[Obsolete]`，内部逻辑桥接到新类型。

新增 `站位需求动作`（用于 MovementExecutor）：

```csharp
public class 站位需求动作 : FactAction
{
    public double Deadline { get; set; }   // 最晚到达时刻（战斗秒数）
    public string Role { get; set; } = "All";  // "All" | "Tank" | "Healer" | "Dps"
}
```

#### 2.3.3 事件 ID 唯一性

JS 编辑器（`fact-editor.js`）创建事件时自动生成 UUID 作为 `id`，对作者透明。作者仍通过 `name` 字段识别事件。运行时直接拿 UUID 去重。

#### 2.3.4 处理时机

| 需求类型 | 触发时机 | 执行方式 |
|---------|---------|---------|
| `需求治疗` | 事件到达时立即 | `DecisionEngine.计算治疗()` → `SlotExecutor.ExecuteSlot()` |
| `需求减伤` | 事件到达时评估，窗口内释放 | `DecisionEngine.计算减伤()` → 记录 `PendingMitigation` → 每帧检查窗口 |

#### 2.3.5 减伤窗口计算

```
伤害时刻 = event.time + event.duration
释放窗口 = [伤害时刻 - 技能持续秒, 伤害时刻]
```

```csharp
record PendingMitigation(string EventId, uint SkillId, long WindowStartMs, long WindowEndMs, bool Executed);
```

每帧检查：`_battleTimeMs` 进入 `[WindowStart, WindowEnd]` 且 `!Executed` → `SlotExecutor.ExecuteSlot()` → 标记完成。

#### 2.3.6 去重策略

| 需求类型 | 去重方式 |
|---------|---------|
| `需求治疗` | `_processedHealEventIds: HashSet<string>`，事件 ID 处理后加入 |
| `需求减伤` | `_processedMitEventIds: HashSet<string>` 确保同一事件只分配一次；`Executed` 标记确保每技能只释放一次 |

---

## 3. QT 调控 ACR 输出节奏

### 3.1 设计

事实轴事件触发时，可设置/切换 QTHelper 中的 QT 状态，ACR 下一帧 `Check()` 即响应。

### 3.2 新增 Action 类型

```csharp
// FactNode.cs
public class 设置QT动作 : FactAction
{
    public string QtId { get; set; }      // QT ID（如 "__builtin_burst", "astralFire"）
    public bool Value { get; set; }        // 目标值
    public double Offset { get; set; }     // 偏移秒数（负=提前，正=延后，默认0=事件到达时）
}

public class 切换QT动作 : FactAction
{
    public string QtId { get; set; }
    public double Offset { get; set; }
}
```

### 3.3 JSON 示例

```json
{
  "type": "设置QT",
  "qtId": "__builtin_hold",
  "value": true
}

{
  "type": "设置QT",
  "qtId": "__builtin_burst",
  "value": true,
  "offset": -3.0
}

{
  "type": "设置QT",
  "qtId": "astralFire",
  "value": false
}
```

### 3.4 执行

事件到达时，将 offset 非零的 QT action 记录到 `_pendingQtActions`（含执行时间 `eventTime + offset`）。每帧 `BuildState()` 检查到期项 → `QTHelper.SetValue(id, value)` 或 `QTHelper.Toggle(id)`。

### 3.5 开关

由 `FactAxisFlags.QtControl` 控制。关闭时 QT action 跳过不执行。

---

## 4. MovementDemand 完整移动系统

### 4.1 架构

```
辅助轴 TreeScriptNode(FactNodeId) → 脚本计算点位 ─┐
外部分发插件 (IPC) ────────────────────────────────┤
                                                   │
    ┌──────────────────────────────────────────────┘
    ▼
DemandBuffer.Add(MovementDemand{ FactNodeId, Type=MoveTo/TP/Hold })

事实轴 FactEvent(id) → { type: "站位需求", deadline: N }
  → IntelligenceEngine.Update() → DemandBuffer匹配FactNodeId → ActiveDemands

MovementExecutor(每帧):
  foreach ActiveDemands:
    MoveTo: 计算 travelTime = dist/speed, 当前 >= deadline - travelTime
      → vnavmesh.SimpleMove.PathfindAndMoveTo(pos, fly=false)
      → 兜底模式：来不及 → TP
    TP:     坐标瞬移（外部插件）
    Hold:   vnavmesh.Path.Stop() + 阻塞 duration 秒
```

### 4.2 事实轴侧：站位需求

```json
{
  "type": "站位需求",
  "deadline": 12.0,
  "role": "All"
}
```

位置由辅助轴脚本通过 `FactNodeId` 关联提供，事实轴只管 deadline。

### 4.3 辅助轴侧：TreeScriptNode 产出 MovementDemand

```csharp
// 辅助轴脚本节点（已有 FactNodeId 字段）
// 脚本内：
var demand = new MovementDemand
{
    Id = Guid.NewGuid().ToString("N")[..8],
    FactNodeId = this.FactNodeId,   // 关联事实事件
    Type = DemandType.MoveTo,
    TargetPos = new Vector3(x, y, z),
    TargetHeading = heading,
    Source = "AssistAxis"
};
DemandBuffer.Add(demand);
```

### 4.4 DemandBuffer IPC — 外部分发插件接入

DemandBuffer 暴露 IPC 让外部分发插件添加 MovementDemand，无需本地引用 HiAuRo.dll。

**Dalamud IPC 注册** (`Plugin.cs`)：

```csharp
// 注册 IPC 端点，接收 JSON 序列化的 MovementDemand
DService.Instance().PI.GetIpcProvider<string>("HiAuRo.AddMovementDemand")
    .RegisterAction(json =>
    {
        var demand = JsonSerializer.Deserialize<MovementDemand>(json);
        if (demand != null) DemandBuffer.Add(demand);
    });
```

**外部插件调用**：

```csharp
// 分发插件中
var ipc = pi.GetIpcSubscriber<string>("HiAuRo.AddMovementDemand");
ipc.InvokeAction(JsonSerializer.Serialize(demand));
```

**契约**：
- IPC 名称：`HiAuRo.AddMovementDemand`
- 入参：JSON 字符串（`MovementDemand` 序列化）
- 线程安全：`DemandBuffer.Add()` 内部使用 `ConcurrentQueue`

### 4.5 移动时间计算

参考 BossMod (BossmodReborn `NavigationDecision.Build`)：

```csharp
// 常量
const float 基础移速 = 6.0f;          // y/s
const float 安全缓冲 = 0.5f;           // 最小缓冲秒
```

**计算方法**：

```csharp
float 计算移动耗时(Vector3 from, Vector3 to)
{
    // 通过 VNavmesh IPC 获取真实路径
    var waypoints = vnavmesh.Nav.Pathfind(from, to, fly: false);
    if (waypoints == null || waypoints.Count < 2)
        return Vector3.Distance(from, to) / 基础移速;  // 兜底：直线距离

    // 沿途点累计路径长度
    float pathLength = 0;
    for (int i = 1; i < waypoints.Count; i++)
        pathLength += Vector3.Distance(waypoints[i-1], waypoints[i]);

    return pathLength / 基础移速 + 安全缓冲;
}
```

**最晚出发时间**：

```
最晚出发 = deadline - 计算移动耗时(当前位置, 目标位置)
```

**执行移动时**（出发时刻到达）：

```
vnavmesh.SimpleMove.PathfindAndMoveTo(目标位置, fly=false)
```

比直线距离精确，避开不可通行区域导致的路径绕行估计偏差。

DemandBuffer 需要暴露 IPC 让外部分发插件添加 MovementDemand。无需本地脚本引用 HiAuRo.dll。

**Dalamud IPC 注册** (`Plugin.cs`)：

```csharp
// 注册 IPC 端点，接收 JSON 序列化的 MovementDemand
DService.Instance().PI.GetIpcProvider<string>("HiAuRo.AddMovementDemand")
    .RegisterAction(json =>
    {
        var demand = JsonSerializer.Deserialize<MovementDemand>(json);
        if (demand != null) DemandBuffer.Add(demand);
    });
```

**外部插件调用**：

```csharp
// 分发插件中
var ipc = pi.GetIpcSubscriber<string>("HiAuRo.AddMovementDemand");
ipc.InvokeAction(JsonSerializer.Serialize(demand));
```

**契约**：
- IPC 名称：`HiAuRo.AddMovementDemand`
- 入参：JSON 字符串（`MovementDemand` 序列化）
- 线程安全：`DemandBuffer.Add()` 内部使用 `ConcurrentQueue`，天然安全

### 4.6 MovementExecutor 执行逻辑

```csharp
public sealed class MovementExecutor
{
    public static MovementExecutor Instance { get; } = new();
    private MovementExecutor() { }

    private readonly HashSet<string> _executedDemandIds = new();
    private readonly Dictionary<string, double> _startedDemands = new();  // demandId → 出发时刻
    private long _holdUntilMs;

    public void Update(FactState state)
    {
        if (!FactAxisFlags.Enabled.移动执行) return;

        if (Environment.TickCount64 < _holdUntilMs) return;  // Hold 中

        foreach (var demand in IntelligenceEngine.Instance.ActiveDemands)
        {
            if (_executedDemandIds.Contains(demand.Id)) continue;

            switch (demand.Type)
            {
                case DemandType.MoveTo:
                    处理MoveTo(demand, state);
                    break;
                case DemandType.TP:
                    处理TP(demand);
                    break;
                case DemandType.Hold:
                    处理Hold(demand);
                    break;
            }
        }
    }

    private void 处理MoveTo(MovementDemand demand, FactState state)
    {
        var deadline = state.CurrentEvent?.Actions.OfType<站位需求动作>().FirstOrDefault()?.Deadline;
        if (deadline == null) return;

        var travelTime = 计算移动耗时(LocalPlayer.Position, demand.TargetPos!.Value);

        // NavMesh_TP兜底：已出发但来不及 → TP
        if (Config.FactAxis.MovementMode == MovementMode.NavMesh_TP兜底
            && _startedDemands.TryGetValue(demand.Id, out var startedAt))
        {
            var elapsed = (state.TotalTime - startedAt);
            var remainingTravel = 计算移动耗时(LocalPlayer.Position, demand.TargetPos!.Value);
            if (deadline.Value - state.TotalTime < remainingTravel)
            {
                瞬移(demand.TargetPos.Value, demand.TargetHeading);
                _executedDemandIds.Add(demand.Id);
                _startedDemands.Remove(demand.Id);
                return;
            }
        }

        if (state.TotalTime >= deadline.Value - travelTime)
        {
            执行移动(demand);
            _startedDemands[demand.Id] = state.TotalTime;
        }
    }

    private void 执行移动(MovementDemand demand)
    {
        if (Config.FactAxis.MovementMode == MovementMode.TP)
            瞬移(demand.TargetPos!.Value, demand.TargetHeading);
        else
            vnavmesh.MoveTo(demand.TargetPos!.Value, demand.TargetHeading);
    }
    }

    private void 执行移动(MovementDemand demand)
    {
        if (Config.FactAxis.MovementMode == MovementMode.TP)
            瞬移(demand.TargetPos!.Value, demand.TargetHeading);
        else
        {
            // VNavmesh IPC: 寻路 + 移动
            var ipc = DService.Instance().PI.GetIpcSubscriber<Vector3, bool, bool>(
                "vnavmesh.SimpleMove.PathfindAndMoveTo");
            ipc.InvokeFunc(demand.TargetPos!.Value, false);  // false=不走飞行
        }
    }

    private void 处理TP(MovementDemand demand)
    {
        瞬移(demand.TargetPos!.Value, demand.TargetHeading);
        _executedDemandIds.Add(demand.Id);
    }

    private void 处理Hold(MovementDemand demand)
    {
        // VNavmesh IPC: 停止
        DService.Instance().PI.GetIpcSubscriber<object>("vnavmesh.Path.Stop").InvokeAction();
        if (demand.Duration.HasValue)
            _holdUntilMs = Environment.TickCount64 + (long)(demand.Duration.Value * 1000);
        _executedDemandIds.Add(demand.Id);
    }
}
```

### 4.7 用户偏好

```csharp
// FactAxisFlags
public enum MovementMode { NavMesh, TP, NavMesh_TP兜底 }
public MovementMode MovementMode;  // NavMesh走 / TP瞬移 / NavMesh走但来不及就TP
```

`站位需求` 只声明必要性，不关心实现。MovementExecutor 读用户偏好决定 MoveTo 还是 TP。

### 4.8 开关

```
FactAxisFlags:
  MoveTo / TP / Hold — 三个独立开关，默认全部关闭
```

---

## 5. 基础设施

### 5.1 E1 — FactAxisFlags 功能切分开关

#### 5.1.1 数据结构

```csharp
// Infrastructure/PluginConfig.cs
public class FactAxisFlags
{
    // 时间线观测
    public bool Observe = true;

    // QT 调控
    public bool QtControl;

    // 决策分配
    public bool TeamMitigation;
    public bool PersonalMitigation;
    public bool TeamHealing;
    public bool ForceExecute;   // 技能强制释放

    // 智能移动
    public bool MoveTo;
    public bool TP;
    public bool Hold;

    // 移动模式偏好
    public MovementMode MovementMode = MovementMode.NavMesh;
}

public enum MovementMode { NavMesh, TP }
```

#### 5.1.2 ImGui Tab

主窗口新增「事实轴」Tab，分组渲染：
- **观测**：Observe
- **QT调控**：QtControl
- **决策分配**：TeamMitigation / PersonalMitigation / TeamHealing / ForceExecute
- **移动**：MoveTo / TP / Hold + MovementMode 下拉

#### 5.1.3 AIRunner 按开关调度

```csharp
void UpdateFactAxis(state)
{
    var f = Config.FactAxis;

    if (f.Observe)
        FactTimeline.Instance.Update(_battleTimeMs);

    if (f.QtControl || f.TeamMitigation || f.PersonalMitigation
        || f.TeamHealing || f.ForceExecute)
        UpdateDecisions();  // 内部再按子开关分治

    智能层更新();  // IntelligenceEngine 始终运行，MovementExecutor 内部读开关
}
```

### 5.2 E2 — 自动切模式

#### 5.2.1 优先模式配置

```csharp
// PluginConfig
public enum AutoSwitchMode { None, Execution优先, Fact优先 }
public AutoSwitchMode AutoSwitch = AutoSwitchMode.Execution优先;
```

#### 5.2.2 ModeSwitch 统一入口

```csharp
public static void TryAutoSwitch()
{
    if (CurrentMode != Mode.None) return;

    var tid = GameState.TerritoryType;
    bool hasExec = File.Exists($"ExecutionTimelines/{tid}.json");
    bool hasFact = File.Exists($"FactTimelines/{tid}.json");

    if (hasExec && hasFact)
        SetMode(Config.AutoSwitch switch { Execution优先 => ExecutionAxis, _ => FactAxis });
    else if (hasExec)
        SetMode(ExecutionAxis);
    else if (hasFact && Config.FactAxis.Observe)
        SetMode(FactAxis);
}
```

替代现有 `TryAutoSwitchToExecutionAxis()`。

---

## 6. 涉及文件清单

| 文件 | 操作 | 内容 |
|------|------|------|
| `FactAxis/FactNode.cs` | 修改 | 新增 `需求减伤动作`、`需求治疗动作`、`设置QT动作`、`切换QT动作`、`站位需求动作`；标记 `需求动作` [Obsolete] |
| `FactAxis/FactTimeline.cs` | 修改 | `BuildState()` 新增 pending QT actions 检查；`RunActions()` 处理新 action 类型 |
| `FactAxis/FactSpellTable.cs` | **新增** | 技能执行数据表 |
| `Runtime/Intelligence/MovementDemand.cs` | 修改 | 移除 `TargetRole`；新增 `Duration` 字段 |
| `Runtime/Intelligence/MovementExecutor.cs` | **新增** | 移动执行器：MoveTo/TP/Hold + deadline 调度 |
| `Runtime/AIRunner.cs` | 修改 | B2 追加 Reset；B3 重写 UpdateDecisions；接入 MovementExecutor；E1 读开关 |
| `Runtime/ModeSwitch.cs` | 修改 | `TryAutoSwitch()` 替代 `TryAutoSwitchToExecutionAxis()` |
| `Infrastructure/PluginConfig.cs` | 修改 | 新增 `FactAxisFlags`、`AutoSwitchMode` |
| `UI/MainWindow.cs` | 修改 | 新增「事实轴」Tab |
| `Plugin.cs` | 修改 | 注册 IPC `HiAuRo.AddMovementDemand` |
| `UI/web/fact-editor.js` | 修改 | 新建事件时自动生成 UUID id |
| `Decision/DecisionEngine.cs` | 修改 | 拆分 `计算减伤()` / `计算治疗()` 方法；用 FactSpellTable 构造 Spell |

---

## 7. 验证方案

### 7.1 单元级验证

| 测试点 | 方法 |
|--------|------|
| FactSpellTable 查表 | 构造 Spell → 检查属性完整性 |
| 需求分治去重 | 同 event 两次进入 UpdateDecisions → 仅执行一次 |
| 减伤窗口 | 当前时间在窗口外 → 不执行；窗口内 → 执行 |
| QT offset | offset=-3 → event.time-3s 执行 |
| IntelligenceEngine.Reset | 调 Reset → ActiveDemands/_releasedFactNodeIds 空 |

### 7.2 集成级验证

1. 加载 sample_timeline.json，切到 FactAxis 模式
2. 在副本中触发战斗
3. 验证 FactTimeline 启动 → Sync 校准 → 需求动作触发 → DecisionEngine 分配 → 技能执行
4. 验证 ImGui Tab 开关可实时控制功能启停

### 7.3 游戏内验证

1. 用极朱雀诗魂战 sample_timeline.json 实体测试
2. 模拟 8 人队伍（至少覆盖 BRD/MNK/WHM 职业）
3. 验证减伤/治疗分配结果在 `[SlotExec]` 日志中可见
4. 验证 QT 设置后 ACR 行为变化

---

## 8. 后续扩展（不在本次范围）

| 项目 | 说明 |
|------|------|
| 全职业技能注册 | DecisionSkillRegistry + FactSpellTable 补全剩余 18 职业 |
| CD 预留系统 | 前瞻分配，避免前后事件技能冲突 |
| 多 Sync 类型 | hpThreshold / statusGain / castInterrupt 等 |
| 丰富条件系统 | HpThresholdCondition / AndCondition / OrCondition |
| COOP-01 多人协调 | IPC 跨插件通信，全队统一减伤调度 |
| AI-04 自适应兜底 | 偏离预期时自动回退/重校准 |
