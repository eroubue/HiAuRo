# 事实轴事件类型重构 + 目标可选中性声明 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 重构 FactEvent 数据模型（Type/AbilityId 提至事件层、FactSyncDef 精简）、新增 Targetable 声明和 FactState 查询 API

**Architecture:** FactNode.cs 数据模型层变更 → FactTimeline.cs 引擎适配 → fact-editor.js 编辑器更新 → ACR_AUTHOR_GUIDE.md 文档更新。共用 `cmd.exe /c "dotnet build E:\HiAuRo\HiAuRo.slnx -c Release -nologo"` 验证。

**Tech Stack:** C# / .NET 10 / System.Text.Json / Vanilla JS

---

## 文件结构

| 文件 | 职责 | 变更类型 |
|------|------|----------|
| `HiAuRo/FactAxis/FactNode.cs` | 数据模型：枚举、事件、Sync、状态 | 修改 |
| `HiAuRo/FactAxis/FactTimeline.cs` | 运行引擎：状态写入、Sync匹配适配 | 修改 |
| `HiAuRo/UI/web/fact-editor.js` | 编辑器：属性面板、颜色渲染 | 修改 |
| `doc/ACR_AUTHOR_GUIDE.md` | 文档 | 修改 |

---

### Task 1: FactNode.cs — 数据模型层

**Files:**
- Modify: `HiAuRo/FactAxis/FactNode.cs`

- [ ] **Step 1: 新增 FactEventType 枚举**

在 namespace 内、`FactTimelineData` 类之前（第 7 行附近）插入：

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

- [ ] **Step 2: 修改 FactEvent — 新增 Type, AbilityId, Targetable**

在原 `FactEvent` 类的 `Duration` 属性之后（第 60 行之后）、`StartSync` 之前（第 63 行之前）插入三个新字段：

```csharp
    /// <summary>游戏事件类型。None=无游戏事件对照</summary>
    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FactEventType Type { get; set; } = FactEventType.None;

    /// <summary>主要ID（技能ID/连线ID/BuffID等，按Type解释）。0=无</summary>
    [JsonPropertyName("abilityId")]
    public uint AbilityId { get; set; }

    /// <summary>目标可选中状态声明。null=不涉及，true=变为可选中，false=变为不可选中</summary>
    [JsonPropertyName("targetable")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Targetable { get; set; }
```

同时需要在文件顶部加 `using System.Text.Json.Serialization;` ——这个 using 已存在（第 1 行），无需额外添加。但需检查 `JsonStringEnumConverter` 是否需要额外 using——它在 `System.Text.Json.Serialization` 中，已覆盖。

- [ ] **Step 3: 修改 FactSyncDef — 移除 Type 和 AbilityIds**

删除 `FactSyncDef` 类中的以下 4 行（当前第 124-128 行）：

```csharp
    [JsonPropertyName("type")]
    public string Type { get; set; } = ""; // "ability" | "startsUsing" | "inCombat"

    [JsonPropertyName("abilityIds")]
    public List<uint> AbilityIds { get; set; } = [];
```

同时删除 `Match` 方法（当前第 155-156 行）——不再需要。精简后的 `FactSyncDef`：

```csharp
public sealed class FactSyncDef
{
    /// <summary>窗口提前打开秒数（默认 2.5）</summary>
    [JsonPropertyName("windowBefore")]
    public double WindowBefore { get; set; } = 2.5;

    /// <summary>窗口延后关闭秒数（默认 2.5）</summary>
    [JsonPropertyName("windowAfter")]
    public double WindowAfter { get; set; } = 2.5;

    /// <summary>同步命中后跳转到的目标时间（null = 不跳转）</summary>
    [JsonPropertyName("jump")]
    public double? Jump { get; set; }

    /// <summary>是否为无条件跳转（时间到即跳，不等 sync 匹配）</summary>
    [JsonPropertyName("forcejump")]
    public bool ForceJump { get; set; }

    /// <summary>无条件跳转的目标时间</summary>
    [JsonPropertyName("forcejumpTarget")]
    public double? ForceJumpTarget { get; set; }

    // === 运行时计算（不参与 JSON 序列化） ===
    [JsonIgnore] public double Start { get; set; }
    [JsonIgnore] public double End { get; set; }
    [JsonIgnore] public double AnchorTime { get; set; }
}
```

- [ ] **Step 4: 修改 FactState — 新增字段和方法**

在 `FactState` 类的 `LastSyncInfo` 之后（第 297 行后）、`Clear` 之前插入：

```csharp
    /// <summary>当前目标可选中状态。未声明时为 null。</summary>
    [JsonIgnore]
    public bool? IsTargetable { get; set; }

    /// <summary>距下次变为可占用的秒数。当前已可占用时返回 0，无后续声明时返回 null。</summary>
    [JsonIgnore]
    public double? NextTargetableIn =>
        IsTargetable == true ? 0 : PendingEvents.FirstOrDefault(e => e.Targetable == true)?.Time - PhaseTime;

    /// <summary>距下次变为不可占用的秒数。当前已不可占用时返回 0，无后续声明时返回 null。</summary>
    [JsonIgnore]
    public double? NextUntargetableIn =>
        IsTargetable == false ? 0 : PendingEvents.FirstOrDefault(e => e.Targetable == false)?.Time - PhaseTime;

    /// <summary>当前阶段未到达的事件（按时序）。ACR 可自定前向扫描。</summary>
    [JsonIgnore]
    public List<FactEvent> PendingEvents { get; set; } = [];

    /// <summary>查询距指定类游戏事件类型的秒数。无匹配返回 null。</summary>
    public double? NextEventTime(FactEventType type) =>
        PendingEvents.FirstOrDefault(e => e.Type == type)?.Time - PhaseTime;
```

- [ ] **Step 5: 更新 FactState.Clear()**

在 `Clear()` 方法末尾 (第 303 行 `LastSyncInfo = "";` 之后) 添加：

```csharp
        IsTargetable = null;
        PendingEvents.Clear();
```

- [ ] **Step 6: 向后兼容迁移逻辑**

在 `FactEvent` 类中新增一个方法，用于将旧格式（Sync 内 type/abilityIds）迁移到新格式（事件层 Type/AbilityId）：

```csharp
    /// <summary>向后兼容：若 Type=None 则尝试从 StartSync 迁移</summary>
    public void MigrateFromLegacy()
    {
        if (Type != FactEventType.None || AbilityId != 0) return;
        if (StartSync == null) return;

        // 旧版 Sync.Type 示例: "ability" / "startsUsing"
        Type = StartSync.Type switch
        {
            "ability"     => FactEventType.Ability,
            "startsUsing" => FactEventType.StartsUsing,
            _             => FactEventType.None
        };
        if (StartSync.AbilityIds.Count > 0)
            AbilityId = StartSync.AbilityIds[0];

        // 清理 Sync 中的旧字段以使序列化干净
        StartSync.Type = "";
        StartSync.AbilityIds.Clear();
    }
```

同时 `FactSyncDef` 中**保留** `Type` 和 `AbilityIds` 字段作为仅反序列化使用（标记 `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]` 以不输出到 JSON），使得旧 JSON 可读但新 JSON 不写：

```csharp
    // 仅向后兼容反序列化，不参与新格式输出
    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Type { get; set; } = "";

    [JsonPropertyName("abilityIds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<uint> AbilityIds { get; set; } = [];
```

注意：这需要在 FactSyncDef 上使用 `System.Text.Json.Serialization.JsonIgnoreCondition`，需要添加 using。

然后在 `FactTimeline.LoadFromFile` 和 `AutoLoadTimeline` 中，反序列化后遍历所有事件调用 `MigrateFromLegacy()`。

- [ ] **Step 7: 编译验证**

```bash
cmd.exe /c "dotnet build E:\HiAuRo\HiAuRo.slnx -c Release -nologo"
```

预期：编译通过，零错误。

- [ ] **Step 8: 提交**

```bash
git add HiAuRo/FactAxis/FactNode.cs
git commit -m "feat: add FactEventType enum, refactor FactEvent/Sync/State data models"
```

---

### Task 2: FactTimeline.cs — 引擎适配

**Files:**
- Modify: `HiAuRo/FactAxis/FactTimeline.cs`

- [ ] **Step 1: AdvanceTimedEvents — 写入 IsTargetable**

在 `AdvanceTimedEvents` 方法的 `ev.Reached = true;` 之前（约第 345 行），检查 Targetable：

```csharp
                    if (ev.Targetable != null)
                        State.IsTargetable = ev.Targetable.Value;
                    ev.Reached = true;
```

完整上下文（第 342-346 行）：
```csharp
                else
                {
                    ev.ActualStart = fightNow;
                    RunActions(ev);
                    if (ev.Targetable != null)
                        State.IsTargetable = ev.Targetable.Value;
                    ev.Reached = true;
```

- [ ] **Step 2: BuildState — 填充 PendingEvents**

在 `BuildState` 方法中，`AdvanceTimedEvents(fightNow);` 调用之后（约第 290 行），添加：

```csharp
        State.PendingEvents = _currentEvents.Skip(_eventIndex).ToList();
```

- [ ] **Step 3: MatchActiveSyncs — 适配新字段**

原方法（第 439-455 行）使用 `sync.Type` 和 `sync.AbilityIds` 进行匹配。现需改为从关联的 `FactEvent` 读取。但 `_activeSyncs` 存储的是 `FactSyncDef` 对象，需要反向找到所属的 `FactEvent`。

方案：修改 `BuildPhaseSyncWindows`，将 `FactSyncDef` + 所属 `FactEvent` 打包存储。在 `FactTimeline` 中新增内部结构：

```csharp
private readonly List<(FactSyncDef Sync, FactEvent Event)> _activeSyncs = [];
```

修改 `BuildPhaseSyncWindows`（第 405-425 行）：

```csharp
private void BuildPhaseSyncWindows(FactPhase phase)
{
    foreach (var e in phase.Events)
    {
        if (e.StartSync != null)
        {
            e.StartSync.AnchorTime = e.Time;
            e.StartSync.Start = e.Time - e.StartSync.WindowBefore;
            e.StartSync.End = e.Time + e.StartSync.WindowAfter;
            _activeSyncs.Add((e.StartSync, e));
        }
    }
    if (phase.Switch != null)
    {
        phase.Switch.Sync.AnchorTime = double.MaxValue;
        phase.Switch.Sync.Start = 0;
        phase.Switch.Sync.End = double.MaxValue;
        _activeSyncs.Add((phase.Switch.Sync, null!)); // Switch sync 无关联 FactEvent
    }
    _activeSyncs.Sort((a, b) => a.Sync.Start.CompareTo(b.Sync.Start));
}
```

修改 `CollectActiveWindows`（第 427-437 行）：

```csharp
private void CollectActiveWindows(double fightNow)
{
    while (_nextSyncEnd < _activeSyncs.Count)
    {
        var sync = _activeSyncs[_nextSyncEnd].Sync;
        if (sync.Start <= fightNow)
            _nextSyncEnd++;
        else
            break;
    }
}
```

修改 `MatchActiveSyncs`（第 439-455 行），接受 `ITriggerCondParams` 而非 `(string, uint)`：

```csharp
private void MatchActiveSyncs(string gameEventType, uint abilityId, double fightNow)
{
    for (int i = 0; i < _nextSyncEnd; i++)
    {
        var (sync, ev) = _activeSyncs[i];
        if (sync.Start > fightNow) break;
        if (sync.End <= fightNow) continue;

        // 从 FactEvent 读取 type 和 abilityId 做匹配
        if (ev == null) continue; // switch sync
        if (ev.Type == FactEventType.None) continue;

        var evTypeName = ev.Type switch
        {
            FactEventType.Ability     => "ability",
            FactEventType.StartsUsing => "startsUsing",
            _                         => null
        };
        if (evTypeName == null || evTypeName != gameEventType) continue;
        if (ev.AbilityId != 0 && ev.AbilityId != abilityId) continue;

        var targetTime = sync.Jump ?? sync.AnchorTime;
        if (targetTime >= double.MaxValue - 1) continue;

        SyncTo(targetTime);
        return;
    }
}
```

- [ ] **Step 4: BuildSyncWindows — 重置清空适配**

`BuildSyncWindows` 中 `_activeSyncs.Clear()` 仍然有效，但现在泛型变为 `List<(FactSyncDef, FactEvent)>`。

- [ ] **Step 5: OnGameEvent — 暂不需要扩展**

`OnGameEvent` 当前只处理 `ActorCastParams` 和 `ActionEffectParams`，其他类型虽然枚举定义了但 Sync 匹配尚未实现。本次只做数据模型变更，不扩展匹配引擎。后续可单独添加。

- [ ] **Step 6: LoadFromFile — 迁移调用**

在 `LoadFromFile` 和 `AutoLoadTimeline` 中，反序列化后遍历所有 phase → events → 调用 `MigrateFromLegacy()`：

```csharp
// 反序列化后
foreach (var phase in data.Phases)
{
    foreach (var ev in phase.Events)
        ev.MigrateFromLegacy();
    if (phase.Switch != null)
    {
        foreach (var branch in phase.Switch.Branches)
        {
            foreach (var ev in branch.Events)
                ev.MigrateFromLegacy();
            if (branch.Switch != null)
            {
                foreach (var bb in branch.Switch.Branches)
                    foreach (var ev in bb.Events)
                        ev.MigrateFromLegacy();
            }
        }
    }
}
```

- [ ] **Step 7: 编译验证**

```bash
cmd.exe /c "dotnet build E:\HiAuRo\HiAuRo.slnx -c Release -nologo"
```

预期：编译通过，零错误。旧 sample_timeline.json 加载后自动迁移。

- [ ] **Step 8: 提交**

```bash
git add HiAuRo/FactAxis/FactTimeline.cs
git commit -m "feat: adapt FactTimeline to new FactEvent Type/AbilityId model"
```

---

### Task 3: fact-editor.js — 编辑器更新

**Files:**
- Modify: `HiAuRo/UI/web/fact-editor.js`

- [ ] **Step 1: 修改 EVENT_COLORS — 按事件类型着色**

替换 `EVENT_COLORS`（第 25-34 行）：

```javascript
var EVENT_COLORS = {
    'None':               '#94a3b8',  // 灰色
    'Ability':            '#00d4ff',  // 青色
    'StartsUsing':        '#ff9f0a',  // 橙色
    'HeadMarker':         '#ff6b9d',  // 粉色
    'Tether':             '#4ecdc4',  // 墨绿
    'AddedCombatant':     '#00f0a0',  // 绿色
    'RemovedCombatant':   '#95e1d3',  // 浅绿
    'WasDefeated':        '#ff4477',  // 红色
    'GainsEffect':        '#f0d000',  // 黄色
    'LosesEffect':        '#f38181',  // 浅红
    'MapEffect':          '#7e57c2',  // 紫色
    'NPCYell':            '#ff9f0a',  // 橙色
    'default':            '#00d4ff'   // 青色
};
```

同时修改事件节点渲染逻辑（约第 587-674 行），原来基于第一个 action.type 取色的代码，改为读 `ev.type` 或 `ev.Type`（取决于 JSON 键名是 `type` 小写）：

```javascript
// 原代码类似: var color = EVENT_COLORS[firstActionType] || EVENT_COLORS.default;
// 改为:
var typeName = ev.type || ev.Type || 'None';
var color = EVENT_COLORS[typeName] || EVENT_COLORS.default;
```

- [ ] **Step 2: 属性面板 — 基本信息区新增字段**

在 `renderProps()` 的"基本信息"区（时间(s) 和 持续(s) 之后，约第 855 行），添加 Type、AbilityId、Targetable：

```javascript
    // 事件类型
    html += '<div class="prop-row">';
    html += '<span class="prop-label">类型</span>';
    html += '<select class="prop-input" onchange="updateEventProp(\'type\', this.value)">';
    var types = ['None','Ability','StartsUsing','HeadMarker','Tether','AddedCombatant','RemovedCombatant','WasDefeated','GainsEffect','LosesEffect','MapEffect','NPCYell'];
    for (var t of types) {
        html += '<option value="' + t + '"' + ((ev.type || ev.Type || 'None') === t ? ' selected' : '') + '>' + t + '</option>';
    }
    html += '</select></div>';

    // 能力ID
    html += '<div class="prop-row">';
    html += '<span class="prop-label">能力ID</span>';
    html += '<input class="prop-input" type="number" value="' + (ev.abilityId || 0) + '" onchange="updateEventNumProp(\'abilityId\', this.value)">';
    html += '</div>';

    // 目标可选中
    html += '<div class="prop-row">';
    html += '<span class="prop-label">可选中</span>';
    html += '<select class="prop-input" onchange="updateEventProp(\'targetable\', this.value === \'\' ? null : this.value === \'true\')">';
    html += '<option value=""' + (ev.targetable == null ? ' selected' : '') + '>-</option>';
    html += '<option value="true"' + (ev.targetable === true ? ' selected' : '') + '>可选中</option>';
    html += '<option value="false"' + (ev.targetable === false ? ' selected' : '') + '>不可选中</option>';
    html += '</select></div>';
```

- [ ] **Step 3: 属性面板 — 同步校准区精简**

在 `renderSyncSection` 函数中移除 Type 下拉和 AbilityIds 输入框（第 867-875 行删除）。同步区只保留窗口、跳转参数。

同时更新 `updateSyncProp` 函数——不再需要处理 `'type'` 和 `'abilityIds'` 的更新。

- [ ] **Step 4: 编译验证**

```bash
cmd.exe /c "dotnet build E:\HiAuRo\HiAuRo.slnx -c Release -nologo"
```

预期：编译通过。编辑器功能在浏览器中手动验证（`localhost:5678/fact-editor`）。

- [ ] **Step 5: 提交**

```bash
git add HiAuRo/UI/web/fact-editor.js
git commit -m "feat: update fact-editor for new event type/abilityId/targetable model"
```

---

### Task 4: ACR_AUTHOR_GUIDE.md — 文档更新

**Files:**
- Modify: `doc/ACR_AUTHOR_GUIDE.md`

- [ ] **Step 1: 更新 Data.FactState 章节**

替换 4.7 节（`Data.FactState`）中 `FactState` 的字段说明，加入新增属性和 `NextEventTime` 方法：

````markdown
### 4.7 Data.FactState — 事实轴状态

```csharp
Data.FactState              // FactAxis.FactState? 事实轴当前状态快照（未运行时为 null）

// 时间维度
state.PhaseName             // 当前阶段名称
state.PhaseTime             // 当前阶段已过秒数
state.TotalTime             // 战斗总秒数

// 目标可选中
state.IsTargetable          // bool? 当前可选中状态（null=未声明）
state.NextTargetableIn      // double? 距下次变为可占用的秒数（null=无后续声明的可占用窗口）
state.NextUntargetableIn    // double? 距下次变为不可占用的秒数（null=再也不会变不可占用）

// 事件查询
state.NextEventTime(FactEventType.Ability)       // 距下一技能效果秒数
state.NextEventTime(FactEventType.StartsUsing)   // 距下一读条秒数
state.NextEventTime(FactEventType.HeadMarker)    // 距下一点名秒数

// 自定义扫描
state.PendingEvents         // List<FactEvent> 未到达事件（按时序），ACR 可自行 LINQ 过滤
```

```csharp
public void OnBattleUpdate(int battleTimeMs)
{
    var s = Data.FactState;
    if (s == null) return;

    // 10s内可选中吗？
    if (s.NextUntargetableIn == null || s.NextUntargetableIn > 10)
        _canBurst = true;

    // 下一读条还有多久？
    var nextCast = s.NextEventTime(FactEventType.StartsUsing);

    // 自定义：查下一带减伤需求的事件
    var nextMit = s.PendingEvents
        .FirstOrDefault(e => e.Actions.Any(a => a is 需求减伤动作));
}
```
````

- [ ] **Step 2: 提交**

```bash
git add doc/ACR_AUTHOR_GUIDE.md
git commit -m "docs: update ACR author guide with new FactState API"
```

---

## 验证清单

1. 编译通过：`cmd.exe /c "dotnet build E:\HiAuRo\HiAuRo.slnx -c Release -nologo"` — 零错误
2. 旧 `sample_timeline.json` 加载后自动迁移（Type/AbilityId 从 Sync 补全）
3. 新格式 JSON（顶层 `type`/`abilityId`/`targetable`）正确反序列化
4. `FactState.IsTargetable`/`NextTargetableIn`/`NextUntargetableIn` 随事件到达正确更新
5. `NextEventTime(FactEventType.Ability)` 返回正确时间距离
6. 编辑器 Type 下拉正确切换事件渲染颜色
7. 编辑器 Targetable 复选框正确序列化/反序列化
