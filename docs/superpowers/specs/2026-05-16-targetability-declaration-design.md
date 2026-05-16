# 事实轴目标可选中性声明 设计文档

**日期**: 2026-05-16
**类型**: Feature (FactAxis 数据模型 + FactState 扩展)

---

## 1. 动机

ACR 作者需要知道 Boss 何时跳走（不可选中） / 落地（可选中），以便在这些窗口期内调整输出策略（爆发延迟、资源囤积等）。当前 FactAxis 不支持声明目标可选中性，ACR 作者无法从 `FactState` 获取此时间维度信息。

## 2. 设计决策

| 决策 | 选择 | 理由 |
|------|------|------|
| 数据位置 | `FactEvent.Targetable` 字段 | 声明式、自然嵌入时间线事件 |
| 状态暴露 | `FactState.IsTargetable` + `NextTargetableChangeIn` | 当前状态 + 下次变化时间，ACR 可精确规划 |
| 事件类型 | 纯时间推进（不依赖游戏事件 Sync） | 与 cactbot 时间轴处理方式一致，声明即生效 |

---

## 3. 数据模型变更

### 3.1 `FactEvent` 新增字段

**文件**: `HiAuRo/FactAxis/FactNode.cs`

```csharp
// FactEvent 类中新增
/// <summary>目标可选中状态声明。null=不涉及，true=变为可选中，false=变为不可选中</summary>
[JsonPropertyName("targetable")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public bool? Targetable { get; set; }
```

### 3.2 `FactState` 新增字段

```csharp
// FactState 类中新增
/// <summary>当前阶段目标的可选中状态。事件未声明时为 null。</summary>
[JsonIgnore]
public bool? IsTargetable { get; set; }

/// <summary>距下一次目标可选中变化的秒数。无后续声明时为 null。</summary>
[JsonIgnore]
public double? NextTargetableChangeIn { get; set; }
```

两者均为 `[JsonIgnore]`——运行时计算值，不序列化到 JSON。

### 3.3 `FactState.Clear()` 维护

`Clear()` 方法中新增初始化：
```csharp
IsTargetable = null;
NextTargetableChangeIn = null;
```

---

## 4. 运行时行为

### 4.1 `FactTimeline.AdvanceTimedEvents()` 

事件到达时（`ev.Reached = true`），若 `ev.Targetable != null`，写入：
```csharp
State.IsTargetable = ev.Targetable;
```

### 4.2 `FactTimeline.BuildState()` 

`AdvanceTimedEvents` 执行后，向前扫描 `_currentEvents`（从 `_eventIndex` 到末尾），找第一个 `Targetable != null` 的事件：
```csharp
State.NextTargetableChangeIn = nextTargetableEvent.Time - phaseTime;
```

若没有后续声明，为 `null`。

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

0s 开始 `IsTargetable = true`；30s `IsTargetable = false`，`NextTargetableChangeIn = 10s`；40s `IsTargetable = true`。

---

## 6. ACR 使用方式

```csharp
public void OnBattleUpdate(int battleTimeMs)
{
    var state = Data.FactState;
    if (state == null) return;

    // 当前阶段 + 已过时间 + 目标可选中状态
    // state.PhaseName, state.PhaseTime, state.TotalTime
    // state.IsTargetable, state.NextTargetableChangeIn

    if (state.IsTargetable == false && state.NextTargetableChangeIn > 3.0)
    {
        // Boss 不可选中且距下次可选中还有 3s+，可以提前准备爆发技能冷却
        _preparingBurst = true;
    }
    else if (state.IsTargetable == true && state.NextTargetableChangeIn != null && state.NextTargetableChangeIn < 5.0)
    {
        // Boss 可选中但即将再次跳走（<5s），小爆发窗口内全力输出
        _dumpResources = true;
    }
}
```

---

## 7. 文件变更清单

| 文件 | 变更 |
|------|------|
| `HiAuRo/FactAxis/FactNode.cs` | `FactEvent` + `Targetable`；`FactState` + `IsTargetable` + `NextTargetableChangeIn`；`FactState.Clear()` 维护 |
| `HiAuRo/FactAxis/FactTimeline.cs` | `AdvanceTimedEvents()` 写入 `State.IsTargetable`；`BuildState()` 计算 `NextTargetableChangeIn` |
| `doc/ACR_AUTHOR_GUIDE.md` | 更新 `Data.FactState` 文档，添加 `IsTargetable` / `NextTargetableChangeIn` 说明和使用示例 |

---

## 8. 验证

1. 编译通过：`dotnet build HiAuRo.slnx -c Release`
2. 时间轴 JSON（含 `"targetable": true/false`）正确反序列化
3. 时间轴 JSON（不含 `"targetable"` 字段）不反序列化（null，向后兼容）
4. `FactState.IsTargetable` 在事件到达时正确更新
5. `FactState.NextTargetableChangeIn` 正确计算下一变化的时间距离
