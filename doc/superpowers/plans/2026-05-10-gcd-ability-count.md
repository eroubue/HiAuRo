# GCD 窗口能力技计数与上限 — 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 AILoop_Normal 内部的能力技计数改为 `Data.Combat` 公开属性，ACR 作者可读计数、可改上限

**Architecture:** `Data.Combat` 新增两个属性（一个 `internal set` 只读、一个 `public set` 可写），`AILoop_Normal` 删除私有字段改用 `Data.Combat`，`AIRunner`/`ACRLifecycle` 在生命周期事件时重置为 `PluginConfig` 默认值

**Tech Stack:** .NET 10, Dalamud, OmenTools

---

### Task 1: Data.Combat 新增属性

**Files:**
- Modify: `HiAuRo/Data/Data.Combat.cs:9-39`

- [ ] **Step 1: 在 Data.Combat 中添加能力技计数和上限属性**

```csharp
// HiAuRo/Data/Data.Combat.cs — 在 using 之后、class 内最上方添加两个属性

/// <summary>当前 GCD 窗口已使用的能力技数量（框架内部设置，ACR 只读）</summary>
public static int AbilityCountInGcd { get; internal set; }

/// <summary>当前 GCD 窗口能力技上限（ACR 可读写，框架仅在生命周期事件时重置为 PluginConfig 默认值）</summary>
public static int MaxAbilityTimesInGcd { get; set; } = 2;
```

- [ ] **Step 2: 确认文件编译通过**

Run: `dotnet build HiAuRo/HiAuRo.csproj -nologo`
Expected: build success

- [ ] **Step 3: Commit**

```bash
git add HiAuRo/Data/Data.Combat.cs
git commit -m "feat(Data.Combat): add AbilityCountInGcd and MaxAbilityTimesInGcd"
```

---

### Task 2: AILoop_Normal 改为使用 Data.Combat

**Files:**
- Modify: `HiAuRo/Runtime/AILoop_Normal.cs:14,67,97,123,125-128`

- [ ] **Step 1: 删除私有字段和硬编码上限，改用 Data.Combat**

```csharp
// 1) 删除行 14:
// 旧: private int _abilityCount;

// 2) 删除行 67:
// 旧: var maxAbility = 2;

// 3) 改行 97 — 窗口判定 gate:
// 旧: SlotMode.OffGcd => isOffGcdWindow && _abilityCount < maxAbility,
// 新: SlotMode.OffGcd => isOffGcdWindow && Data.Combat.AbilityCountInGcd < Data.Combat.MaxAbilityTimesInGcd,

// 4) 改行 123 — 日志中的引用:
// 旧: ... ab={_abilityCount}/{maxAbility})"
// 新: ... ab={Data.Combat.AbilityCountInGcd}/{Data.Combat.MaxAbilityTimesInGcd})"

// 5) 改行 125-128 — 计数逻辑:
// 旧:
//   if (data.Mode == SlotMode.Gcd)
//       _abilityCount = 0;
//   else if (slot.Actions.Any(a => a.Spell.IsAbility()))
//       _abilityCount++;
// 新:
//   if (data.Mode == SlotMode.Gcd)
//       Data.Combat.AbilityCountInGcd = 0;
//   else if (slot.Actions.Any(a => a.Spell.IsAbility()))
//       Data.Combat.AbilityCountInGcd++;
```

- [ ] **Step 2: 确认编译通过**

Run: `dotnet build HiAuRo/HiAuRo.csproj -nologo`
Expected: build success

- [ ] **Step 3: Commit**

```bash
git add HiAuRo/Runtime/AILoop_Normal.cs
git commit -m "refactor(AILoop_Normal): use Data.Combat for ability count"
```

---

### Task 3: AIRunner 生命周期事件重置计数器

**Files:**
- Modify: `HiAuRo/Runtime/AIRunner.cs:298-306` (Reset 方法) + `:125-133` (切图检测)

- [ ] **Step 1: 在 `Reset()` 中添加计数器重置**

```csharp
// HiAuRo/Runtime/AIRunner.cs — Reset() 方法末尾，OnResetBattle 之前
public void Reset()
{
    SpellQueue.Clear();
    OpenerMgr.Reset();
    CountDownHandler.Reset();
    Coroutine.Instance.Clear();
    _battleTimeMs = 0;

    // 重置 GCD 能力技计数和上限
    Data.Combat.AbilityCountInGcd = 0;
    Data.Combat.MaxAbilityTimesInGcd = PluginConfig.Instance.MaxAbilityTimesInGcd;

    CurrentRotation?.EventHandler?.OnResetBattle();
}
```

- [ ] **Step 2: 在切图检测中添加计数器重置**

```csharp
// HiAuRo/Runtime/AIRunner.cs — Update() 方法内切图检测块
// 在行 131 (EventHandler.OnTerritoryChanged) 之前或之后添加
var territoryId = Data.Combat.TerritoryType;
if (territoryId != _lastTerritoryId)
{
    if (_lastTerritoryId != 0)
    {
        DService.Instance().Log.Information($"[AIRunner] 切图: {_lastTerritoryId} → {territoryId}");

        // 切图时重置 GCD 能力技计数和上限
        Data.Combat.AbilityCountInGcd = 0;
        Data.Combat.MaxAbilityTimesInGcd = PluginConfig.Instance.MaxAbilityTimesInGcd;

        CurrentRotation?.EventHandler?.OnTerritoryChanged();
    }
    _lastTerritoryId = territoryId;
}
```

- [ ] **Step 3: 确认编译通过**

Run: `dotnet build HiAuRo/HiAuRo.csproj -nologo`
Expected: build success

- [ ] **Step 4: Commit**

```bash
git add HiAuRo/Runtime/AIRunner.cs
git commit -m "feat(AIRunner): reset ability count on battle reset and zone change"
```

---

### Task 4: ACRLifecycle 切 ACR 时重置计数器

**Files:**
- Modify: `HiAuRo/Runtime/ACRLifecycle.cs:158-166` (LoadRotation)

- [ ] **Step 1: 在 LoadRotation 开头添加计数器重置**

```csharp
// HiAuRo/Runtime/ACRLifecycle.cs — LoadRotation 方法，UnloadRotation() 之后
private static void LoadRotation(IRotationEntry entry, string settingFolder)
{
    IsLoadingRotation = true;
    UnloadRotation();

    // 切换 ACR 时重置 GCD 能力技计数和上限
    Data.Combat.AbilityCountInGcd = 0;
    Data.Combat.MaxAbilityTimesInGcd = PluginConfig.Instance.MaxAbilityTimesInGcd;

    CurrentJobId = _lastJob;
    // ... 后续代码不变
```

- [ ] **Step 2: 确认编译通过**

Run: `dotnet build HiAuRo/HiAuRo.csproj -nologo`
Expected: build success

- [ ] **Step 3: Commit**

```bash
git add HiAuRo/Runtime/ACRLifecycle.cs
git commit -m "feat(ACRLifecycle): reset ability count on ACR switch"
```

---

### Task 5: 验证完整构建

- [ ] **Step 1: 完整构建**

Run: `dotnet build HiAuRo/HiAuRo.csproj -nologo`
Expected: build success, no warnings

- [ ] **Step 2: 最终 commit（如有额外修正）**

```bash
git add -A
git status
```
