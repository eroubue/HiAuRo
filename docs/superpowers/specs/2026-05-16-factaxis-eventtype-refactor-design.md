# 事实轴事件类型重构 + 目标可选中性声明 设计文档

**日期**: 2026-05-16
**类型**: Feature (FactAxis 数据模型重构 + ACR 查询 API)

---

## 1. 动机

当前 `FactEvent` 缺乏显式游戏事件类型和技能 ID——类型藏在 `FactSyncDef.Type` + `abilityIds` 里，导致：

- ACR 作者无法按事件类型查询"下一读条何时发生""下一技能何时落地"
- 编辑器无法按类型渲染事件（当前仅用第一个 Action 类型着色）
- 无法声明目标可选中性（Boss 跳走/落地）供 ACR 预测输出窗口
- 与 cactbot 时间轴无法直接映射

## 2. 设计决策

| 决策 | 选择 | 理由 |
|------|------|------|
| 事件类型归属 | 从 `FactSyncDef` 提取到 `FactEvent.Type` | 类型是事件身份，不是 Sync 的附属 |
| 类型命名 | 对齐 cactbot 英文名（Ability/StartsUsing/...） | 社区一致，导入时直接映射 |
| Sync 角色 | 精简为纯校准窗口参数 | 移除冗余 type/abilityIds |
| Targetable | `FactEvent.Targetable` 布尔声明 + `FactState` 方向查询 API | 状态机，需前向扫描 |
| ACR API | `NextEventTime(type)` + `NextTargetableIn` + `NextUntargetableIn` + `PendingEvents` | 精确指向可选中/不可选中两个方向 |

---

## 3. 数据模型变更

### 3.1 新增 `FactEventType` 枚举

**文件**: `HiAuRo/FactAxis/FactNode.cs`

```csharp
/// <summary>事实轴事件类型——对应游戏包类型 (ITriggerCondParams 子类)</summary>
public enum FactEventType
{
    /// <summary>无游戏事件对照（纯时间标记/状态声明）</summary>
    None,
    /// <summary>技能效果 → ActionEffectParams</summary>
    Ability,
    /// <summary>读条开始 → ActorCastParams</summary>
    StartsUsing,
    /// <summary>点名标记 → ActorControlTargetIconParams</summary>
    HeadMarker,
    /// <summary>连线 → TetherCreateParams</summary>
    Tether,
    /// <summary>单位出现 → UnitCreateParams</summary>
    AddedCombatant,
    /// <summary>单位消失 → UnitDeleteParams</summary>
    RemovedCombatant,
    /// <summary>单位死亡 → ActorControlDeathParams</summary>
    WasDefeated,
    /// <summary>Buff获得 → BuffGainParams</summary>
    GainsEffect,
    /// <summary>Buff消失 → BuffRemoveParams</summary>
    LosesEffect,
    /// <summary>地图特效 → MapEffectParams</summary>
    MapEffect,
    /// <summary>NPC喊话 → NpcYellParams</summary>
    NPCYell,
}
```

### 3.2 `FactEvent` 修改

**文件**: `HiAuRo/FactAxis/FactNode.cs`

```csharp
public sealed class FactEvent
{
    // === 现有字段（保留） ===
    public string Id { get; set; }
    public string Name { get; set; }
    public double Time { get; set; }
    public double? Duration { get; set; }

    // === 新增 ===
    /// <summary>游戏事件类型。None=无游戏事件对照</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FactEventType Type { get; set; } = FactEventType.None;

    /// <summary>主要ID（技能ID/连线ID/BuffID等，按Type解释）。0=无</summary>
    public uint AbilityId { get; set; }

    /// <summary>目标可选中状态声明。null=不涉及，true=变为可选中，false=变为不可选中</summary>
    [JsonPropertyName("targetable")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Targetable { get; set; }

    // === Sync 保留（成员精简） ===
    public FactSyncDef? StartSync { get; set; }
    public FactSyncDef? EndSync { get; set; }

    // === Actions 保留 ===
    public List<FactAction> Actions { get; set; } = [];

    // === 运行时字段（保留） ===
    // Reached, ActualStart, ActualEnd, ActionsDone, SyncFired
}
```

### 3.3 `FactSyncDef` 精简

```csharp
public sealed class FactSyncDef
{
    // 移除: string Type → 改为 FactEvent.Type
    // 移除: List<uint> AbilityIds → 改为 FactEvent.AbilityId

    /// <summary>窗口提前打开秒数（默认2.5）</summary>
    public double WindowBefore { get; set; } = 2.5;

    /// <summary>窗口延后关闭秒数（默认2.5）</summary>
    public double WindowAfter { get; set; } = 2.5;

    /// <summary>同步命中后跳转到的目标时间</summary>
    public double? Jump { get; set; }

    /// <summary>是否无条件跳转</summary>
    public bool ForceJump { get; set; }

    /// <summary>无条件跳转的目标时间</summary>
    public double? ForceJumpTarget { get; set; }

    // 运行时计算（保留）
    // Start, End, AnchorTime
}
```

### 3.4 `FactState` 新增

```csharp
// FactState 新增

/// <summary>当前目标可选中状态。未声明时为 null。</summary>
[JsonIgnore]
public bool? IsTargetable { get; set; }

/// <summary>距下次变为可占用的秒数。当前已可占用时返回 0，无后续声明时返回 null。</summary>
[JsonIgnore]
public double? NextTargetableIn
{
    get
    {
        if (IsTargetable == true) return 0;
        var next = PendingEvents.FirstOrDefault(e => e.Targetable == true);
        return next?.Time - PhaseTime;
    }
}

/// <summary>距下次变为不可占用的秒数。当前已不可占用时返回 0，无后续声明时返回 null。</summary>
[JsonIgnore]
public double? NextUntargetableIn
{
    get
    {
        if (IsTargetable == false) return 0;
        var next = PendingEvents.FirstOrDefault(e => e.Targetable == false);
        return next?.Time - PhaseTime;
    }
}

/// <summary>当前阶段未到达的事件（按时序）。ACR 可自定前向扫描。</summary>
[JsonIgnore]
public List<FactEvent> PendingEvents { get; set; } = [];

/// <summary>查询距指定游戏事件类型的秒数。无匹配返回 null。</summary>
public double? NextEventTime(FactEventType type)
{
    FactEvent? next = PendingEvents.FirstOrDefault(e => e.Type == type);
    return next?.Time - PhaseTime;
}
```

### 3.5 `FactState.Clear()` 维护

```csharp
IsTargetable = null;
PendingEvents.Clear();
```

---

## 4. 运行时行为

### 4.1 `FactTimeline.AdvanceTimedEvents()`

事件到达时：
```csharp
if (ev.Targetable != null)
    State.IsTargetable = ev.Targetable;
```

### 4.2 `FactTimeline.BuildState()`

```csharp
State.PendingEvents = _currentEvents.Skip(_eventIndex).ToList();
```

### 4.3 `FactTimeline.OnGameEvent()` — Sync 匹配适配

原匹配逻辑使用 `sync.Type` / `sync.AbilityIds`，现改为从 `FactEvent.Type` / `FactEvent.AbilityId` 读取。

`MatchActiveSyncs` 需要重构：遍历 `_activeSyncs` 时，通过 `FactEvent.Type` 映射到 `ITriggerCondParams` 类型名和 `AbilityId` 做匹配。

### 4.4 向后兼容

旧 JSON（type/abilityIds 在 Sync 内）反序列化时自动迁移：
- 若 `FactEvent.Type == None && FactEvent.AbilityId == 0` 且 `StartSync` 有 type/abilityIds，则从 Sync 提取填充到事件层级
- 迁移后 Sync 中 type/abilityIds 字段仍接受但不使用（向前兼容老编辑器输出）

---

## 5. JSON 格式对比

### 旧格式
```json
{ "time": 15.9, "name": "补天之手",
  "startSync": { "type": "Ability", "abilityIds": [46295], "windowBefore": 10, "windowAfter": 5 } }
```

### 新格式
```json
{ "time": 15.9, "name": "补天之手", "type": "Ability", "abilityId": 46295,
  "startSync": { "windowBefore": 10, "windowAfter": 5 } }
```

### Targetable 示例
```json
{ "time": 30.0, "name": "Boss 跳走", "targetable": false },
{ "time": 40.0, "name": "Boss 落地", "type": "Ability", "abilityId": 99999, "targetable": true }
```

> `targetable` 可独立声明（Type=None），也可与任何 Type 共存。

---

## 6. ACR 使用方式

```csharp
public void OnBattleUpdate(int battleTimeMs)
{
    var s = Data.FactState;
    if (s == null) return;

    // 目标可选中
    bool? isTargetable = s.IsTargetable;
    double? canHitIn   = s.NextTargetableIn;    // null=再无可占用窗口
    double? invulnIn    = s.NextUntargetableIn;  // null=不会再变不可占用

    // 10s内不会变不可占用 → 可以开爆发
    if (invulnIn == null || invulnIn > 10)
        _canBurst = true;

    // 游戏事件时间查询
    double? nextCast   = s.NextEventTime(FactEventType.StartsUsing);
    double? nextDamage = s.NextEventTime(FactEventType.Ability);

    // 自定义扫描（复杂预测）
    var nextMarker = s.PendingEvents
        .FirstOrDefault(e => e.Type == FactEventType.HeadMarker && e.AbilityId == 0x017F);

    // 后10s内最后声明的可选中状态
    bool canHitWindow = !s.PendingEvents
        .TakeWhile(e => e.Time <= s.PhaseTime + 10.0)
        .Any(e => e.Targetable == false);
}
```

---

## 7. 文件变更清单

| 文件 | 变更 |
|------|------|
| `HiAuRo/FactAxis/FactNode.cs` | 新增 `FactEventType` 枚举；`FactEvent` + `Type` + `AbilityId` + `Targetable`；`FactSyncDef` 移除 `Type`/`AbilityIds`；`FactState` + `IsTargetable` + `NextTargetableIn` + `NextUntargetableIn` + `PendingEvents` + `NextEventTime()`；向后兼容迁移逻辑 |
| `HiAuRo/FactAxis/FactTimeline.cs` | `AdvanceTimedEvents()` 写入 `IsTargetable`；`BuildState()` 填充 `PendingEvents`；`MatchActiveSyncs` 适配新字段；`OnGameEvent` 处理更多类型 |
| `HiAuRo/UI/web/fact-editor.js` | 事件属性面板：Type 下拉 → 按类型着色；AbilityId 输入框；Targetable 复选框 |
| `doc/ACR_AUTHOR_GUIDE.md` | 更新 `Data.FactState` 章节 |

---

## 8. 验证

1. 编译通过：`dotnet build HiAuRo.slnx -c Release`
2. 新格式 JSON 正确反序列化
3. 旧格式 JSON 自动迁移（Type/AbilityId 从 Sync 补全）
4. `IsTargetable`/`NextTargetableIn`/`NextUntargetableIn` 正确计算
5. `NextEventTime` 按类型正确查找到下一事件
6. 12 种 `FactEventType` 枚举值均已定义
