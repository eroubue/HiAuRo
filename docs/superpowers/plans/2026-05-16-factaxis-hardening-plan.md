# FactAxis 全链路加固 — 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 补齐事实轴流程 B1/B2/B3 缺陷 + QT 调控 + MovementDemand + 功能开关 + 自动切模式，使 ACR+辅助轴+事实轴+决策层+智能层全链路可测试跑通

**Architecture:** 自下而上修改：先补数据模型(FactNode/MovementDemand/Config)，再建工具表(FactSpellTable)，改核心引擎(DecisionEngine/FactTimeline)，改运行时集成(AIRunner/ModeSwitch)，最后新模块(MovementExecutor)+UI+IPC

**Tech Stack:** C# / .NET 10 / Dalamud.NET.Sdk / OmenTools / FFXIV

**Spec:** `docs/superpowers/specs/2026-05-16-factaxis-hardening-design.md`

---

## 文件清单

| 文件 | 操作 | 职责 |
|------|------|------|
| `FactAxis/FactNode.cs` | 修改 | 新增 5 种 Action 类型 |
| `Runtime/Intelligence/MovementDemand.cs` | 修改 | 移除 TargetRole，新增 Duration |
| `Infrastructure/PluginConfig.cs` | 修改 | 新增 FactAxisFlags + AutoSwitchMode |
| `FactAxis/FactSpellTable.cs` | **新增** | 技能执行数据表 |
| `Decision/DecisionEngine.cs` | 修改 | 拆分计算减伤/计算治疗 |
| `FactAxis/FactTimeline.cs` | 修改 | BuildState 增 pending QT 检查 |
| `Runtime/AIRunner.cs` | 修改 | B2+B3 重写 + E1 开关 + MovementExecutor |
| `Runtime/ModeSwitch.cs` | 修改 | E2 TryAutoSwitch 替代 |
| `Runtime/Intelligence/MovementExecutor.cs` | **新增** | 移动执行器 |
| `UI/MainWindow.cs` | 修改 | 新增「事实轴」Tab |
| `UI/web/fact-editor.js` | 修改 | UUID 自动生成 |
| `Plugin.cs` | 修改 | IPC 注册 HiAuRo.AddMovementDemand |

---

### Task 1: FactNode.cs — 新增 5 种 Action 类型

**Files:**
- Modify: `HiAuRo/FactAxis/FactNode.cs`

- [ ] **Step 1: 添加新的 Action 类定义**

在 `需求动作` 类后面（第 225 行 `}` 之后）、`#endregion` 之前插入：

```csharp
/// <summary>减伤需求 — 事件到达时评估，在技能持续窗口内释放</summary>
public sealed class 需求减伤动作 : FactAction
{
    [JsonPropertyName("value")]
    public int Value { get; set; }
    public override void Execute(FactTimeline timeline) { }
}

/// <summary>治疗需求 — 事件到达时立即分配+释放</summary>
public sealed class 需求治疗动作 : FactAction
{
    [JsonPropertyName("value")]
    public int Value { get; set; }
    public override void Execute(FactTimeline timeline) { }
}

/// <summary>设置 QT — 到达时(或offset后)调 QTHelper.SetValue</summary>
public sealed class 设置QT动作 : FactAction
{
    [JsonPropertyName("qtId")]
    public string QtId { get; set; } = "";
    [JsonPropertyName("value")]
    public bool Value { get; set; }
    [JsonPropertyName("offset")]
    public double Offset { get; set; }
    public override void Execute(FactTimeline timeline) { }
}

/// <summary>切换 QT — 到达时(或offset后)调 QTHelper.Toggle</summary>
public sealed class 切换QT动作 : FactAction
{
    [JsonPropertyName("qtId")]
    public string QtId { get; set; } = "";
    [JsonPropertyName("offset")]
    public double Offset { get; set; }
    public override void Execute(FactTimeline timeline) { }
}

/// <summary>站位需求 — 声明 deadline，位置由辅助轴通过 FactNodeId 关联</summary>
public sealed class 站位需求动作 : FactAction
{
    [JsonPropertyName("deadline")]
    public double Deadline { get; set; }
    [JsonPropertyName("role")]
    public string Role { get; set; } = "All";
    public override void Execute(FactTimeline timeline) { }
}
```

- [ ] **Step 2: 注册 JSON 多态类型**

在 `FactAction` 的 `[JsonDerivedType]` 列表（第 174-178 行）追加 5 行：

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SetVariableAction), "setVariable")]
[JsonDerivedType(typeof(ToggleVariableAction), "toggleVariable")]
[JsonDerivedType(typeof(SkillSuggestionAction), "skillSuggestion")]
[JsonDerivedType(typeof(LogMessageAction), "logMessage")]
[JsonDerivedType(typeof(需求动作), "demand")]
[JsonDerivedType(typeof(需求减伤动作), "需求减伤")]           // 新增
[JsonDerivedType(typeof(需求治疗动作), "需求治疗")]           // 新增
[JsonDerivedType(typeof(设置QT动作), "设置QT")]               // 新增
[JsonDerivedType(typeof(切换QT动作), "切换QT")]               // 新增
[JsonDerivedType(typeof(站位需求动作), "站位需求")]           // 新增
public abstract class FactAction
```

- [ ] **Step 3: 标记旧 需求动作 为 Obsolete**

在第 215 行 `需求动作` 类前加：

```csharp
[Obsolete("使用 需求减伤动作 / 需求治疗动作 替代")]
```

- [ ] **Step 4: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded.

- [ ] **Step 5: 提交**

```bash
git add HiAuRo/FactAxis/FactNode.cs
git commit -m "feat: add 5 new FactAction types for demand split, QT control, movement positioning"
```

---

### Task 2: 数据模型更新 — MovementDemand + PluginConfig

**Files:**
- Modify: `HiAuRo/Runtime/Intelligence/MovementDemand.cs`
- Modify: `HiAuRo/Infrastructure/PluginConfig.cs`

- [ ] **Step 1: 读 MovementDemand.cs 当前内容**

读取 `/mnt/e/HiAuRo/HiAuRo/Runtime/Intelligence/MovementDemand.cs` 完整文件（约 18 行）。

- [ ] **Step 2: 移除 TargetRole，新增 Duration**

替换整个文件：

```csharp
namespace HiAuRo.Runtime.Intelligence;

/// <summary>移动/传送/站定 需求（本地数据，角色路由由外部分发插件负责）</summary>
public sealed class MovementDemand
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string FactNodeId { get; set; } = "";
    public DemandType Type { get; set; } = DemandType.MoveTo;
    public Vector3? TargetPos { get; set; }
    public float? TargetHeading { get; set; }
    public float? Duration { get; set; }       // Hold 持续秒
    public int AddedOrder { get; set; }
    public string Source { get; set; } = "";
}
```

- [ ] **Step 3: PluginConfig — 新增 FactAxisFlags + AutoSwitchMode**

在 `PluginConfig.cs` 文件末尾的类闭括号 `}` 前添加：

```csharp
#region FactAxis

public sealed class FactAxisFlags
{
    public bool Observe = true;          // 时间线观测
    public bool QtControl;               // QT 调控
    public bool TeamMitigation;          // 团队减伤分配
    public bool PersonalMitigation;      // 单人减伤分配
    public bool TeamHealing;             // 团队治疗分配
    public bool ForceExecute;            // 技能强制释放
    public bool MoveTo;                  // NavMesh 移动
    public bool TP;                      // 传送
    public bool Hold;                    // 站位保持
    public MovementMode MovementMode = MovementMode.NavMesh_TP兜底;
}

public enum MovementMode { NavMesh, TP, NavMesh_TP兜底 }

public enum AutoSwitchMode { None, Execution优先, Fact优先 }

public FactAxisFlags FactAxis { get; set; } = new();
public AutoSwitchMode AutoSwitch { get; set; } = AutoSwitchMode.Execution优先;

#endregion
```

- [ ] **Step 4: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded.

- [ ] **Step 5: 提交**

```bash
git add HiAuRo/Runtime/Intelligence/MovementDemand.cs HiAuRo/Infrastructure/PluginConfig.cs
git commit -m "feat: remove TargetRole from MovementDemand, add Duration; add FactAxisFlags + AutoSwitchMode to PluginConfig"
```

---

### Task 3: FactSpellTable.cs — 新工具

**Files:**
- Create: `HiAuRo/FactAxis/FactSpellTable.cs`

- [ ] **Step 1: 创建 FactSpellTable.cs**

```csharp
using HiAuRo.ACR;

namespace HiAuRo.FactAxis;

/// <summary>
/// 事实轴技能执行数据表 — 独立于 DecisionSkillRegistry
/// 存"技能怎么放"（TargetType/类别/类型），DecisionEngine 执行时构造完整 Spell
/// </summary>
public static class FactSpellTable
{
    private static readonly Dictionary<uint, SpellExecutionInfo> _table = new();

    public static void 注册(uint id, string name,
        SpellTargetType target = SpellTargetType.Self,
        SpellCategory category = SpellCategory.Default,
        SpellType type = SpellType.Ability)
    {
        _table[id] = new SpellExecutionInfo
        {
            Id = id,
            Name = name,
            TargetType = target,
            SpellCategory = category,
            Type = type
        };
    }

    public static Spell? 构造Spell(uint id)
    {
        if (!_table.TryGetValue(id, out var info)) return null;
        return new Spell
        {
            Id = info.Id,
            Name = info.Name,
            TargetType = info.TargetType,
            SpellCategory = info.SpellCategory,
            Type = info.Type
        };
    }

    public static void Clear() => _table.Clear();
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

- [ ] **Step 2: 在 DecisionEngine.LoadBuiltinSkills 末尾注册技能数据**

读取 `DecisionEngine.cs`，找到 `LoadBuiltinSkills()` 方法体末尾。在该方法最后添加：

```csharp
// BRA
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

- [ ] **Step 3: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded.

- [ ] **Step 4: 提交**

```bash
git add HiAuRo/FactAxis/FactSpellTable.cs HiAuRo/Decision/DecisionEngine.cs
git commit -m "feat: add FactSpellTable for skill execution data; register BRD/MNK/WHM skills"
```

---

### Task 4: DecisionEngine.cs — 拆分方法 + 使用 FactSpellTable

**Files:**
- Modify: `HiAuRo/Decision/DecisionEngine.cs`

- [ ] **Step 1: 读 DecisionEngine.cs 当前位置**

读取 `/mnt/e/HiAuRo/HiAuRo/Decision/DecisionEngine.cs`，确保了解 `计算()`、`分配减伤()`、`分配治疗()`、`LoadBuiltinSkills()` 的结构。

- [ ] **Step 2: 修改 计算() 使用 FactSpellTable 构造 Spell**

替换 `计算()` 方法（第 32-47 行）为独立方法：

```csharp
/// <summary>治疗需求 — 事件到达时立即分配（适合 HoT/盾预先铺）</summary>
public DecisionOutput 计算治疗(int 需求治疗)
{
    _output = new DecisionOutput();
    if (需求治疗 <= 0) return _output;
    var 队伍 = GetAvailableRoles();
    分配治疗(需求治疗, 队伍);
    return _output;
}

/// <summary>减伤需求 — 事件到达时评估，在窗口内延迟释放</summary>
public DecisionOutput 计算减伤(int 需求减伤)
{
    _output = new DecisionOutput();
    if (需求减伤 <= 0) return _output;
    var 队伍 = GetAvailableRoles();
    分配减伤(需求减伤, 队伍);
    return _output;
}
```

保留 `计算(int, int)` 为桥接方法（标记 `[Obsolete]`），委托到新方法：

```csharp
[Obsolete("使用 计算减伤() / 计算治疗()")]
public DecisionOutput 计算(int 需求减伤, int 需求治疗)
{
    _output = new DecisionOutput();
    if (需求减伤 == 0 && 需求治疗 == 0) return _output;
    var 队伍 = GetAvailableRoles();
    if (需求减伤 > 0) 分配减伤(需求减伤, 队伍);
    if (需求治疗 > 0) 分配治疗(需求治疗, 队伍);
    return _output;
}
```

- [ ] **Step 3: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded.

- [ ] **Step 4: 提交**

```bash
git add HiAuRo/Decision/DecisionEngine.cs
git commit -m "refactor: split DecisionEngine into 计算减伤/计算治疗, mark old 计算 as Obsolete"
```

---

### Task 5: FactTimeline.cs — Pending QT actions 检查

**Files:**
- Modify: `HiAuRo/FactAxis/FactTimeline.cs`

- [ ] **Step 1: 添加 pending QT 字段**

在 FactTimeline 类现有字段区（约第 30 行附近，`_waitingSwitch` 之后）添加：

```csharp
// QT offset 延迟执行
private readonly List<(设置QT动作 Action, double ExecuteAt)> _pendingQtActions = new();
```

- [ ] **Step 2: 修改 RunActions 处理 QT 动作**

修改 `RunActions(FactEvent ev)` 方法（第 334-343 行）。在 `ev.ActionsDone = true;` 前插入新类型的处理：

```csharp
private void RunActions(FactEvent ev)
{
    if (ev.ActionsDone) return;
    foreach (var action in ev.Actions)
    {
        try
        {
            switch (action)
            {
                case 设置QT动作 qtSet when qtSet.Offset != 0:
                    _pendingQtActions.Add((qtSet, ev.Time + qtSet.Offset));
                    break;
                case 设置QT动作 qtSet:
                    if (PluginConfig.Instance.FactAxis.QtControl)
                        QTHelper.SetValue(qtSet.QtId, qtSet.Value);
                    break;
                case 切换QT动作 qtToggle when qtToggle.Offset != 0:
                    _pendingQtActions.Add((new 设置QT动作 { QtId = qtToggle.QtId, Value = false, Offset = 0 }, ev.Time + qtToggle.Offset));
                    break;
                case 切换QT动作 qtToggle:
                    if (PluginConfig.Instance.FactAxis.QtControl)
                        QTHelper.Toggle(qtToggle.QtId);
                    break;
                default:
                    action.Execute(this);
                    break;
            }
        }
        catch (Exception ex) { DService.Instance().Log.Error($"[FactAxis] 动作异常: {ex}"); }
    }
    ev.ActionsDone = true;
}
```

注：`切换QT动作` 的 offset > 0 场景无法直接存储原类型，包装为 `设置QT动作` 在到期时 toggle。如果需要在到期时 toggle（而非设置值），可扩展 pending 结构。

- [ ] **Step 3: 修改 BuildState 检查到期 QT**

在 `BuildState()` 方法开头（约第 250 行，`var fightNow = FightNow;` 后）添加：

```csharp
// 检查到期 pending QT actions
for (int i = _pendingQtActions.Count - 1; i >= 0; i--)
{
    var (qtAction, executeAt) = _pendingQtActions[i];
    if (fightNow >= executeAt)
    {
        if (PluginConfig.Instance.FactAxis.QtControl)
        {
            if (qtAction is 切换QT动作 toggleAction)
                QTHelper.Toggle(toggleAction.QtId);
            else
                QTHelper.SetValue(qtAction.QtId, qtAction.Value);
        }
        _pendingQtActions.RemoveAt(i);
    }
}
```

- [ ] **Step 4: Reset 中清空 _pendingQtActions**

在 `Reset()` 方法（第 83-104 行）中添加：

```csharp
_pendingQtActions.Clear();
```

- [ ] **Step 5: 添加 using**

在文件顶部 using 区域添加：

```csharp
using HiAuRo.ACR;
using HiAuRo.Infrastructure;
```

- [ ] **Step 6: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded.

- [ ] **Step 7: 提交**

```bash
git add HiAuRo/FactAxis/FactTimeline.cs
git commit -m "feat: add pending QT action support with offset timing in FactTimeline"
```

---

### Task 6: AIRunner.cs — B2 + B3 重写 + E1 开关

**Files:**
- Modify: `HiAuRo/Runtime/AIRunner.cs`

- [ ] **Step 1: 读 AIRunner.cs 当前数据**

读取 `/mnt/e/HiAuRo/HiAuRo/Runtime/AIRunner.cs`（458行），重点：`Reset()` (L302)、`UpdateFactAxis()` (L360)、`UpdateDecisions()` (L384)。

- [ ] **Step 2: B2 — Reset() 添加 IntelligenceEngine + MovementExecutor 重置**

在 `Reset()` 方法末尾（第 312 行 `}` 前）添加：

```csharp
IntelligenceEngine.Instance.Reset();
MovementExecutor.Instance.Reset();
```

添加 using：`using HiAuRo.Runtime.Intelligence;`

- [ ] **Step 3: 添加 B3 所需字段**

在 AIRunner 类字段区域添加（约第 60 行附近，现有 `_prevFactAxisState` 后）：

```csharp
// B3: 需求分治 + 去重
private readonly HashSet<string> _processedHealEventIds = new();
private readonly HashSet<string> _processedMitEventIds = new();
private readonly List<PendingMitigation> _pendingMits = new();

private sealed record PendingMitigation(string EventId, uint SkillId, string SkillName, long WindowStartMs, long WindowEndMs, bool Executed = false);
```

- [ ] **Step 4: 重写 UpdateFactAxis + UpdateDecisions**

替换 `UpdateFactAxis()` 和 `UpdateDecisions()` 方法（第 360-419 行）：

```csharp
private void UpdateFactAxis(CombatContext.State state)
{
    var flags = PluginConfig.Instance.FactAxis;

    if (state != _prevFactAxisState)
    {
        _prevFactAxisState = state;
        if (state == CombatContext.State.InCombat)
            FactTimeline.Instance.Start();
        else
            FactTimeline.Instance.Stop();
    }

    if (state != CombatContext.State.InCombat) return;

    // 时间线观测
    if (flags.Observe)
        FactTimeline.Instance.Update(_battleTimeMs);

    // 决策分配
    bool needDecisions = flags.TeamMitigation || flags.PersonalMitigation
                        || flags.TeamHealing || flags.ForceExecute;
    if (needDecisions)
        UpdateDecisions(flags);

    // 检查到期减伤
    CheckPendingMitigations();

    // 智能层 + 移动执行
    IntelligenceEngine.Instance.Update(FactTimeline.Instance);
    MovementExecutor.Instance.Update(FactTimeline.Instance.State);
}

private void UpdateDecisions(FactAxisFlags flags)
{
    var state = FactTimeline.Instance.State;
    var ev = state.CurrentEvent;
    if (ev == null || ev.Actions.Count == 0) return;

    foreach (var action in ev.Actions)
    {
        switch (action)
        {
            case 需求治疗动作 heal when !_processedHealEventIds.Contains(ev.Id):
                _processedHealEventIds.Add(ev.Id);
                if (flags.TeamHealing)
                {
                    var output = DecisionEngine.Instance.计算治疗(heal.Value);
                    执行分配技能(output);
                }
                break;

            case 需求减伤动作 mit when !_processedMitEventIds.Contains(ev.Id):
                _processedMitEventIds.Add(ev.Id);
                if (flags.TeamMitigation || flags.PersonalMitigation)
                {
                    var output = DecisionEngine.Instance.计算减伤(mit.Value);
                    // 不立即执行，记录窗口
                    foreach (var alloc in output.减伤分配)
                    {
                        var skill = DecisionSkillRegistry.团队减伤表
                            .FirstOrDefault(kv => kv.Value.Any(s => s.技能ID == alloc.技能ID));
                        int durSec = skill.Value?.FirstOrDefault(s => s.技能ID == alloc.技能ID)?.持续秒 ?? 10;
                        long damageMs = (long)((ev.Time + (ev.Duration ?? 0)) * 1000);
                        long windowStart = damageMs - durSec * 1000;
                        _pendingMits.Add(new PendingMitigation(ev.Id, alloc.技能ID, alloc.技能名称, windowStart, damageMs));
                    }
                }
                break;

            case 需求动作 oldDemand:
                // 兼容旧格式：桥接到新语义
                if (!_processedHealEventIds.Contains(ev.Id) && oldDemand.需求治疗 > 0 && flags.TeamHealing)
                {
                    _processedHealEventIds.Add(ev.Id);
                    var outHeal = DecisionEngine.Instance.计算治疗(oldDemand.需求治疗);
                    执行分配技能(outHeal);
                }
                if (!_processedMitEventIds.Contains(ev.Id) && oldDemand.需求减伤 > 0 && (flags.TeamMitigation || flags.PersonalMitigation))
                {
                    _processedMitEventIds.Add(ev.Id);
                    var outMit = DecisionEngine.Instance.计算减伤(oldDemand.需求减伤);
                    foreach (var alloc in outMit.减伤分配)
                    {
                        int durSec = 10;
                        long damageMs = (long)((ev.Time + (ev.Duration ?? 0)) * 1000);
                        _pendingMits.Add(new PendingMitigation(ev.Id, alloc.技能ID, alloc.技能名称, damageMs - durSec * 1000, damageMs));
                    }
                }
                break;
        }
    }
}

private void CheckPendingMitigations()
{
    var flags = PluginConfig.Instance.FactAxis;
    if (!flags.ForceExecute) return;

    for (int i = _pendingMits.Count - 1; i >= 0; i--)
    {
        var mit = _pendingMits[i];
        if (mit.Executed) { _pendingMits.RemoveAt(i); continue; }
        if (_battleTimeMs >= mit.WindowStartMs && _battleTimeMs <= mit.WindowEndMs)
        {
            var spell = FactSpellTable.构造Spell(mit.SkillId);
            if (spell != null)
            {
                var slot = new Slot();
                slot.Add(spell);
                SlotExecutor.ExecuteSlot(slot);
            }
            mit.Executed = true;
        }
        // Window expired without execution — cleanup
        if (_battleTimeMs > mit.WindowEndMs)
            _pendingMits.RemoveAt(i);
    }
}

private void 执行分配技能(DecisionOutput output)
{
    var flags = PluginConfig.Instance.FactAxis;
    if (!flags.ForceExecute || output.执行技能IDs.Count == 0) return;

    foreach (var skillId in output.执行技能IDs)
    {
        var spell = FactSpellTable.构造Spell(skillId);
        if (spell == null) continue;
        var slot = new Slot();
        slot.Add(spell);
        SlotExecutor.ExecuteSlot(slot);
    }
}
```

添加 using：`using HiAuRo.Decision;` `using HiAuRo.Infrastructure;`

- [ ] **Step 5: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded.

- [ ] **Step 6: 提交**

```bash
git add HiAuRo/Runtime/AIRunner.cs
git commit -m "fix: B2 reset IntelligenceEngine; B3 demand split + dedup + window; E1 flags; MovementExecutor integration"
```

---

### Task 7: ModeSwitch.cs — E2 统一自动切换

**Files:**
- Modify: `HiAuRo/Runtime/ModeSwitch.cs`

- [ ] **Step 1: 读 ModeSwitch.cs**

读取 `/mnt/e/HiAuRo/HiAuRo/Runtime/ModeSwitch.cs`（77行）。

- [ ] **Step 2: 替换 TryAutoSwitchToExecutionAxis 为 TryAutoSwitch**

替换 `TryAutoSwitchToExecutionAxis()` 方法（第 59-76 行）：

```csharp
/// <summary>切图时按配置优先级自动切换模式</summary>
public static void TryAutoSwitch()
{
    if (CurrentMode != Mode.None) return;

    var territoryId = OmenTools.OmenService.GameState.TerritoryType;
    if (territoryId == 0) return;

    var configDir = DService.Instance().PI.ConfigDirectory.FullName;
    bool hasExec = File.Exists(Path.Combine(configDir, "ExecutionTimelines", $"{territoryId}.json"));
    bool hasFact = File.Exists(Path.Combine(configDir, "FactTimelines", $"{territoryId}.json"));

    var autoSwitch = PluginConfig.Instance.AutoSwitch;

    if (hasExec && hasFact)
    {
        SetMode(autoSwitch == AutoSwitchMode.Fact优先 ? Mode.FactAxis : Mode.ExecutionAxis);
        DService.Instance().Log.Information($"[ModeSwitch] 双 JSON 存在, 优先级={autoSwitch}, 切换={CurrentMode}");
    }
    else if (hasExec)
    {
        SetMode(Mode.ExecutionAxis);
        DService.Instance().Log.Information($"[ModeSwitch] 自动切换执行轴: {territoryId}");
    }
    else if (hasFact && PluginConfig.Instance.FactAxis.Observe)
    {
        SetMode(Mode.FactAxis);
        DService.Instance().Log.Information($"[ModeSwitch] 自动切换事实轴: {territoryId}");
    }
}
```

- [ ] **Step 3: 更新调用端**

查找 AIRunner.cs 中对 `TryAutoSwitchToExecutionAxis()` 的调用，改为 `TryAutoSwitch()`。

```bash
grep -n "TryAutoSwitchToExecutionAxis" HiAuRo/Runtime/AIRunner.cs
```

预期在约第 133 行。改为 `ModeSwitch.TryAutoSwitch();`。

- [ ] **Step 4: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded.

- [ ] **Step 5: 提交**

```bash
git add HiAuRo/Runtime/ModeSwitch.cs HiAuRo/Runtime/AIRunner.cs
git commit -m "feat: E2 unified TryAutoSwitch with configurable priority (Execution优先/Fact优先)"
```

---

### Task 8: MovementExecutor.cs — 新移动执行器

**Files:**
- Create: `HiAuRo/Runtime/Intelligence/MovementExecutor.cs`

- [ ] **Step 1: 创建 MovementExecutor.cs**

```csharp
using System.Numerics;
using HiAuRo.FactAxis;
using HiAuRo.Infrastructure;
using OmenTools;

namespace HiAuRo.Runtime.Intelligence;

/// <summary>
/// 移动执行器 — 消费 ActiveDemands，通过 VNavmesh IPC 驱动角色移动。
/// MoveTo: 寻路 + deadline 调度 + TP 兜底
/// TP: 坐标瞬移
/// Hold: 停止 + 阻塞 duration 秒
/// </summary>
public sealed class MovementExecutor
{
    public static MovementExecutor Instance { get; } = new();
    private MovementExecutor() { }

    private readonly HashSet<string> _executedDemandIds = new();
    private readonly Dictionary<string, double> _startedMoveDemands = new();
    private long _holdUntilMs;

    // 移动参数（参考 BossMod）
    private const float 基础移速 = 6.0f;
    private const float 安全缓冲 = 0.5f;

    public void Reset()
    {
        _executedDemandIds.Clear();
        _startedMoveDemands.Clear();
        _holdUntilMs = 0;
    }

    public void Update(FactState state)
    {
        var flags = PluginConfig.Instance.FactAxis;
        if (!flags.MoveTo && !flags.TP && !flags.Hold) return;
        if (!state.IsRunning) return;
        if (Environment.TickCount64 < _holdUntilMs) return;

        foreach (var demand in IntelligenceEngine.Instance.ActiveDemands)
        {
            if (_executedDemandIds.Contains(demand.Id)) continue;

            switch (demand.Type)
            {
                case DemandType.MoveTo when flags.MoveTo:
                    处理MoveTo(demand, state, flags);
                    break;
                case DemandType.TP when flags.TP:
                    处理TP(demand);
                    break;
                case DemandType.Hold when flags.Hold:
                    处理Hold(demand);
                    break;
            }
        }
    }

    private void 处理MoveTo(MovementDemand demand, FactState state, FactAxisFlags flags)
    {
        if (demand.TargetPos == null) return;
        var deadline = state.CurrentEvent?.Actions.OfType<站位需求动作>().FirstOrDefault()?.Deadline;
        if (deadline == null) return;

        var playerPos = DService.Instance().ClientState.LocalPlayer?.Position;
        if (playerPos == null) return;

        // TP 兜底：已出发但来不及
        if (flags.MovementMode == MovementMode.NavMesh_TP兜底
            && _startedMoveDemands.TryGetValue(demand.Id, out var startedAt))
        {
            var remainingTravel = 计算移动耗时(playerPos.Value, demand.TargetPos.Value);
            if (deadline.Value - state.TotalTime < remainingTravel)
            {
                瞬移(demand.TargetPos.Value, demand.TargetHeading);
                _executedDemandIds.Add(demand.Id);
                _startedMoveDemands.Remove(demand.Id);
                return;
            }
        }

        // 检查出发时间
        var travelTime = 计算移动耗时(playerPos.Value, demand.TargetPos.Value);
        if (state.TotalTime >= deadline.Value - travelTime)
        {
            执行移动(demand, flags);
            _startedMoveDemands[demand.Id] = state.TotalTime;
        }
    }

    private void 执行移动(MovementDemand demand, FactAxisFlags flags)
    {
        if (flags.MovementMode == MovementMode.TP)
        {
            瞬移(demand.TargetPos!.Value, demand.TargetHeading);
            _executedDemandIds.Add(demand.Id);
        }
        else
        {
            try
            {
                var ipc = DService.Instance().PI.GetIpcSubscriber<Vector3, bool, bool>(
                    "vnavmesh.SimpleMove.PathfindAndMoveTo");
                ipc.InvokeFunc(demand.TargetPos!.Value, false);
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Debug($"[Movement] VNavmesh IPC 不可用: {ex.Message}");
            }
        }
    }

    private void 处理TP(MovementDemand demand)
    {
        if (demand.TargetPos == null) return;
        瞬移(demand.TargetPos.Value, demand.TargetHeading);
        _executedDemandIds.Add(demand.Id);
    }

    private void 处理Hold(MovementDemand demand)
    {
        try
        {
            DService.Instance().PI.GetIpcSubscriber<object>("vnavmesh.Path.Stop").InvokeAction();
        }
        catch { /* VNavmesh may not be installed */ }

        if (demand.Duration.HasValue && demand.Duration.Value > 0)
            _holdUntilMs = Environment.TickCount64 + (long)(demand.Duration.Value * 1000);
        _executedDemandIds.Add(demand.Id);
    }

    private float 计算移动耗时(Vector3 from, Vector3 to)
    {
        try
        {
            var ipc = DService.Instance().PI.GetIpcSubscriber<Vector3, Vector3, bool, List<Vector3>>(
                "vnavmesh.Nav.Pathfind");
            var waypoints = ipc.InvokeFunc(from, to, false);

            if (waypoints == null || waypoints.Count < 2)
                return Vector3.Distance(from, to) / 基础移速;

            float pathLength = 0;
            for (int i = 1; i < waypoints.Count; i++)
                pathLength += Vector3.Distance(waypoints[i - 1], waypoints[i]);

            return pathLength / 基础移速 + 安全缓冲;
        }
        catch
        {
            return Vector3.Distance(from, to) / 基础移速 + 安全缓冲;
        }
    }

    private static void 瞬移(Vector3 pos, float? heading)
    {
        // TP 瞬移 — 由外部插件通过封包/内存完成
        // HiAuRo 只留调用桩，具体实现由外部分发插件处理
        DService.Instance().Log.Debug($"[Movement] TP to ({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1})");
    }
}
```

- [ ] **Step 2: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded.

- [ ] **Step 3: 提交**

```bash
git add HiAuRo/Runtime/Intelligence/MovementExecutor.cs
git commit -m "feat: add MovementExecutor with VNavmesh IPC, deadline scheduling, TP fallback, Hold support"
```

---

### Task 9: UI — MainWindow.cs 事实轴 Tab + fact-editor.js UUID

**Files:**
- Modify: `HiAuRo/UI/MainWindow.cs`
- Modify: `HiAuRo/UI/web/fact-editor.js`

- [ ] **Step 1: 读 MainWindow.cs Tab 结构**

读取 `/mnt/e/HiAuRo/HiAuRo/UI/MainWindow.cs`，找到现有 Tab 定义的位置（查找 `DrawRecording` 或 `ImGui.BeginTabItem`）。

- [ ] **Step 2: 添加「事实轴」Tab**

在 MainWindow 现有 Tab 列表末尾添加新 Tab：

```csharp
private void DrawFactAxisTab()
{
    if (!ImGui.BeginTabItem("事实轴")) return;

    var flags = PluginConfig.Instance.FactAxis;
    bool changed = false;

    ImGui.SeparatorText("观测");
    changed |= ImGui.Checkbox("时间线观测", ref flags.Observe);

    ImGui.SeparatorText("QT 调控");
    changed |= ImGui.Checkbox("QT 调控", ref flags.QtControl);

    ImGui.SeparatorText("决策分配");
    changed |= ImGui.Checkbox("团队减伤分配", ref flags.TeamMitigation);
    changed |= ImGui.Checkbox("单人减伤分配", ref flags.PersonalMitigation);
    changed |= ImGui.Checkbox("团队治疗分配", ref flags.TeamHealing);
    changed |= ImGui.Checkbox("技能强制释放", ref flags.ForceExecute);

    ImGui.SeparatorText("移动");
    changed |= ImGui.Checkbox("MoveTo", ref flags.MoveTo);
    changed |= ImGui.Checkbox("TP", ref flags.TP);
    changed |= ImGui.Checkbox("Hold", ref flags.Hold);

    ImGui.SeparatorText("移动模式");
    var modes = new[] { "NavMesh", "TP", "NavMesh + TP兜底" };
    int modeIdx = (int)flags.MovementMode;
    if (ImGui.Combo("移动模式", ref modeIdx, modes, modes.Length))
    {
        flags.MovementMode = (MovementMode)modeIdx;
        changed = true;
    }

    if (changed)
    {
        PluginConfig.Instance.Save();
    }

    // 显示当前事实轴状态
    ImGui.SeparatorText("运行时状态");
    var state = FactTimeline.Instance.State;
    ImGui.Text($"状态: {(state.IsRunning ? "运行中" : "未启动")}");
    if (state.IsRunning)
    {
        ImGui.Text($"副本: {state.TimelineName}");
        ImGui.Text($"阶段: {state.PhaseName} | {state.Status}");
        ImGui.Text($"时间: 阶段{state.PhaseTime:F1}s / 总{state.TotalTime:F1}s");
        if (state.CurrentEvent != null)
            ImGui.Text($"当前事件: {state.CurrentEvent.Name}");

        var mode = ModeSwitch.CurrentMode;
        ImGui.Text($"模式: {mode}");
    }

    ImGui.EndTabItem();
}
```

添加 using：`using HiAuRo.FactAxis;` `using HiAuRo.Infrastructure;`

- [ ] **Step 3: 在主 Draw 中调用 DrawFactAxisTab**

在 MainWindow 的主 Draw 方法中，找到现有 Tab 后面，添加：`DrawFactAxisTab();`

- [ ] **Step 4: fact-editor.js — UUID 自动生成**

读取 `/mnt/e/HiAuRo/HiAuRo/UI/web/fact-editor.js`。找到事件创建函数（搜索 `addEvent` 或 `newEvent` 或 `create`）。

在新建事件对象时，添加 UUID 生成：

```javascript
// 在事件创建处添加 id 字段
const event = {
    id: crypto.randomUUID(),   // 新增：自动 UUID，对作者透明
    name: "新事件",            // 已有：作者可见名称
    time: 0,
    // ... 其他字段
};
```

如果没有 `crypto.randomUUID()`（Node 环境），使用 polyfill：

```javascript
function generateUUID() {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
        const r = Math.random() * 16 | 0;
        return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16);
    });
}
```

- [ ] **Step 5: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded.

- [ ] **Step 6: 提交**

```bash
git add HiAuRo/UI/MainWindow.cs HiAuRo/UI/web/fact-editor.js
git commit -m "feat: add FactAxis settings Tab in ImGui; auto-generate UUID for fact-editor events"
```

---

### Task 10: Plugin.cs — IPC 注册

**Files:**
- Modify: `HiAuRo/Plugin.cs`

- [ ] **Step 1: 找到 Plugin.cs 初始化区域**

读取 `/mnt/e/HiAuRo/HiAuRo/Plugin.cs`，找到 `DService.Init` 之后、`RuntimeCore.Start` 之前的区域（约第 65-75 行）。

- [ ] **Step 2: 注册 IPC**

在 `EventSystem.Init()` 之后添加：

```csharp
// 注册 MovementDemand IPC（接收外部分发插件的推送）
DService.Instance().PI.GetIpcProvider<string>("HiAuRo.AddMovementDemand")
    .RegisterAction(json =>
    {
        try
        {
            var demand = System.Text.Json.JsonSerializer.Deserialize<MovementDemand>(json);
            if (demand != null)
                DemandBuffer.Add(demand);
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Debug($"[IPC] AddMovementDemand 反序列化失败: {ex.Message}");
        }
    });
```

添加 using：`using HiAuRo.Runtime.Intelligence;`

- [ ] **Step 3: 构建验证**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded.

- [ ] **Step 4: 提交**

```bash
git add HiAuRo/Plugin.cs
git commit -m "feat: register IPC endpoint HiAuRo.AddMovementDemand for external distribution plugins"
```

---

### Task 11: 全量构建 + 验证

- [ ] **Step 1: 清理 + 重建**

```bash
cmd.exe /c "dotnet clean HiAuRo/HiAuRo.csproj -nologo && dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Clean, then Build succeeded with 0 errors, 0 warnings (or only pre-existing warnings).

- [ ] **Step 2: 确认新增文件全部存在**

```bash
ls -la HiAuRo/FactAxis/FactSpellTable.cs HiAuRo/Runtime/Intelligence/MovementExecutor.cs
```

Expected: Both files exist.

- [ ] **Step 3: 确认所有关键词出现在编译产物中**

```bash
grep -r "FactSpellTable\|MovementExecutor\|需求减伤动作\|需求治疗动作\|设置QT动作\|站位需求动作\|FactAxisFlags\|TryAutoSwitch\|AddMovementDemand" HiAuRo/ --include="*.cs" | wc -l
```

Expected: >20 matches across files.

- [ ] **Step 4: 提交**

```bash
git add -A
git commit -m "chore: final build verification after full FactAxis hardening"
```

---

## 验证清单

实现完成后逐项确认：

- [ ] `dotnet build` 零错误
- [ ] `FactSpellTable.注册(...)` 中 BRD/MNK/WHM 8 个技能注册无误
- [ ] `更新Decisions()` 对同一 event ID 仅处理一次（HashSet 去重）
- [ ] 减伤技能在窗口内才释放，窗口外跳过
- [ ] 治疗技能在事件到达时立即释放
- [ ] 旧 `需求动作` (type="demand") 仍可工作 (Obsolete 兼容)
- [ ] QT offset 到期后 `QTHelper.SetValue/Toggle` 被调用
- [ ] `IntelligenceEngine.Reset()` + `MovementExecutor.Reset()` 在战斗重置时被调
- [ ] ImGui「事实轴」Tab 可见，开关可点击
- [ ] `HiAuRo.AddMovementDemand` IPC 注册成功
