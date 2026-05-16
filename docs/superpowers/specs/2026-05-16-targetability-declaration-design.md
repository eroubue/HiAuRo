# 事实轴目标可选中性声明与关键事件查询 设计文档

**日期**: 2026-05-16
**类型**: Feature (FactAxis 数据模型 + FactState 扩展)

---

## 1. 动机

ACR 作者需要知道 Boss 何时跳走（不可选中） / 落地（可选中），以及任意关键事件的时间距离，以便在窗口期内调整输出策略（爆发延迟、资源囤积等）。当前 FactAxis 不支持声明目标可选中性，也无通用事件查询 API，ACR 作者无法从 `FactState` 获取此时间维度信息。

## 2. 设计决策

| 决策 | 选择 | 理由 |
|------|------|------|
| 数据位置 | `FactEvent.Targetable` 字段 | 声明式、自然嵌入时间线事件 |
| 状态暴露 | `FactState.IsTargetable` + `PendingEvents` + `NextEventTime()` | 当前状态 + 通用事件时间查询 |
| 查询API | 枚举 `FactEventType` + 实例方法 `NextEventTime(FactEventType)` | 类型安全、中文注释、覆盖全部类型 |
| 事件类型 | 纯时间推进（不依赖游戏事件 Sync） | 与 cactbot 时间轴处理方式一致，声明即生效 |

---

## 3. 数据模型变更

### 3.1 新增 `FactEventType` 枚举

**文件**: `HiAuRo/FactAxis/FactNode.cs`

```csharp
/// <summary>事实轴事件类型——用于 NextEventTime 查询</summary>
public enum FactEventType
{
    /// <summary>目标可选中性变化</summary>
    Targetable,
    /// <summary>Sync 事件（Boss 读条/技能）</summary>
    Sync,
    /// <summary>减伤需求</summary>
    Mitigation,
    /// <summary>治疗需求</summary>
    Healing,
    /// <summary>技能建议</summary>
    Suggestion,
    /// <summary>QT 开关</summary>
    Qt,
    /// <summary>站位需求</summary>
    Position,
    /// <summary>变量操作</summary>
    Variable,
}
```

### 3.2 `FactEvent` 新增字段

```csharp
// FactEvent 类中新增
/// <summary>目标可选中状态声明。null=不涉及，true=变为可选中，false=变为不可选中</summary>
[JsonPropertyName("targetable")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public bool? Targetable { get; set; }
```

### 3.3 `FactState` 新增字段和方法

```csharp
// FactState 类中新增
/// <summary>当前阶段目标的可选中状态。事件未声明时为 null。</summary>
[JsonIgnore]
public bool? IsTargetable { get; set; }

/// <summary>当前阶段未到达的事件（按时序）。ACR 可自定义过滤。</summary>
[JsonIgnore]
public List<FactEvent> PendingEvents { get; set; } = [];

/// <summary>
/// 查询距离指定类型下一事件的秒数（相对 PhaseTime）。
/// 无匹配返回 null。
/// </summary>
public double? NextEventTime(FactEventType eventType)
{
    FactEvent? next = eventType switch
    {
        FactEventType.Targetable  => PendingEvents.FirstOrDefault(e => e.Targetable != null),
        FactEventType.Sync        => PendingEvents.FirstOrDefault(e => e.StartSync != null || e.EndSync != null),
        FactEventType.Mitigation  => PendingEvents.FirstOrDefault(e => e.Actions.Any(a => a is 需求减伤动作)),
        FactEventType.Healing     => PendingEvents.FirstOrDefault(e => e.Actions.Any(a => a is 需求治疗动作)),
        FactEventType.Suggestion  => PendingEvents.FirstOrDefault(e => e.Actions.Any(a => a is SkillSuggestionAction)),
        FactEventType.Qt          => PendingEvents.FirstOrDefault(e => e.Actions.Any(a => a is 设置QT动作 or 切换QT动作)),
        FactEventType.Position    => PendingEvents.FirstOrDefault(e => e.Actions.Any(a => a is 站位需求动作)),
        FactEventType.Variable    => PendingEvents.FirstOrDefault(e => e.Actions.Any(a => a is SetVariableAction or ToggleVariableAction)),
        _                         => null
    };
    return next?.Time - PhaseTime;
}
```

> `NextEventTime` 返回的是**该事件声明的 Time 减当前 PhaseTime**——不是实时校准值。若事件已被 Sync 校准调整过（`ActualStart` / `ActualEnd`），后续可扩展支持校准值。

### 3.4 `FactState.Clear()` 维护

```csharp
IsTargetable = null;
PendingEvents.Clear();
```

---

## 4. 运行时行为

### 4.1 `FactTimeline.AdvanceTimedEvents()`

事件到达时（`ev.Reached = true`），若 `ev.Targetable != null`，写入：
```csharp
State.IsTargetable = ev.Targetable;
```

### 4.2 `FactTimeline.BuildState()`

`AdvanceTimedEvents` 执行后，填充未到达事件：
```csharp
State.PendingEvents = _currentEvents.Skip(_eventIndex).ToList();
```

`NextEventTime()` 由 ACR 调用时按需计算。

---

## 5. JSON 示例

```json
{
  "name": "极朱雀诗魂战",
  "territoryId": 297,
  "author": "示例",
  "phases": [
    {
      "id": "p1",
      "name": "P1 开场",
      "events": [
        { "time": 0, "name": "开怪", "targetable": true },
        { "time": 10.0, "name": "AOE", "actions": [{ "type": "skillSuggestion", "skillId": 7561, "label": "策动" }] },
        { "time": 30.0, "name": "Boss 跳走", "targetable": false },
        { "time": 40.0, "name": "Boss 落地", "targetable": true },
        { "time": 60.0, "name": "AOE 2" }
      ]
    }
  ]
}
```

0s `IsTargetable = true`；30s `IsTargetable = false`；40s `IsTargetable = true`。

---

## 6. ACR 使用方式

```csharp
using HiAuRo.FactAxis;

public void OnBattleUpdate(int battleTimeMs)
{
    var state = Data.FactState;
    if (state == null) return;

    // 当前阶段 + 已过时间
    // state.PhaseName, state.PhaseTime, state.TotalTime

    // 目标可选中状态
    bool? targetable = state.IsTargetable;

    // 通用事件时间查询
    double? nextTargetable = state.NextEventTime(FactEventType.Targetable);
    double? nextMitigation = state.NextEventTime(FactEventType.Mitigation);
    double? nextSync      = state.NextEventTime(FactEventType.Sync);

    // 自定义过滤：直接用 PendingEvents 列表
    var nextAnyAction = state.PendingEvents
        .FirstOrDefault(e => e.Actions.Count > 0);

    if (state.IsTargetable == false && nextTargetable > 3.0)
    {
        // Boss 不可选中且距下次可选中还有 3s+
        _preparingBurst = true;
    }
}
```

---

## 7. 文件变更清单

| 文件 | 变更 |
|------|------|
| `HiAuRo/FactAxis/FactNode.cs` | 新增 `FactEventType` 枚举；`FactEvent` + `Targetable`；`FactState` + `IsTargetable` + `PendingEvents` + `NextEventTime()`；`Clear()` 维护 |
| `HiAuRo/FactAxis/FactTimeline.cs` | `AdvanceTimedEvents()` 写入 `State.IsTargetable`；`BuildState()` 填充 `State.PendingEvents` |
| `doc/ACR_AUTHOR_GUIDE.md` | 更新 `Data.FactState` 章节，添加枚举、方法、示例 |

---

## 8. 验证

1. 编译通过：`dotnet build HiAuRo.slnx -c Release`
2. JSON 含/不含 `"targetable"` 均正确反序列化（null 时忽略，向后兼容）
3. `FactState.IsTargetable` 在事件到达时正确更新
4. `NextEventTime(FactEventType.Targetable)` 返回正确的时间距离（null 时无后续声明）
5. 8 种 `FactEventType` 均有匹配分支
