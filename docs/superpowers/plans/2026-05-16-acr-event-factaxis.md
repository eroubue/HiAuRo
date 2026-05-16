# ACR 游戏事件与事实轴信息暴露 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 `ITriggerCondParams` 游戏事件全部转发给 ACR 作者，同时暴露 `FactState` 和阶段切换回调

**Architecture:** 扩展 `IRotationEventHandler` 新增 2 个 default 方法；`AIRunner` 在 Load/Unload 中订阅/取消订阅 `GameEventHook.OnEventFired` 和 `FactTimeline.PhaseChanged` 并转发；`FactTimeline` 新增 `PhaseChanged` 事件；`Data` 门面暴露 `FactState` 静态属性

**Tech Stack:** C# / .NET 10 / Dalamud.NET.Sdk 15.0.0

---

## 文件结构

| 文件 | 职责 | 变更类型 |
|------|------|----------|
| `HiAuRo/ACR/Interfaces/IRotationEventHandler.cs` | ACR 事件回调接口，新增 2 个方法 | 修改 |
| `HiAuRo/Runtime/AIRunner.cs` | AI 主引擎，订阅/转发事件 | 修改 |
| `HiAuRo/FactAxis/FactTimeline.cs` | 事实轴引擎，新增 PhaseChanged 事件 | 修改 |
| `HiAuRo/Data/Data.cs` | 数据层门面，新增 FactState 属性 | 修改 |

---

### Task 1: IRotationEventHandler — 新增 OnGameEvent 和 OnPhaseChanged

**Files:**
- Modify: `HiAuRo/ACR/Interfaces/IRotationEventHandler.cs`

- [ ] **Step 1: 在接口末尾追加两个 default 方法**

在文件第 40 行（最后一个 `}` 之前）插入两个新方法：

```csharp
using HiAuRo.Execution.Events;  // ← 新增 using，放在文件顶部现有 using 之后

// ... 现有方法保持不变 ...

/// <summary>游戏事件分发回调。全部 ITriggerCondParams 子类型均转发，ACR 作者自行类型判断过滤。回调在 GameEventHook 线程执行，为只读通知。</summary>
void OnGameEvent(ITriggerCondParams eventParams) { }

/// <summary>事实轴阶段切换回调。仅 FactAxis 运行时触发。</summary>
void OnPhaseChanged(string phaseId, string phaseName) { }
```

完整文件最终内容：
```csharp
using HiAuRo.ACR;
using HiAuRo.Execution.Events;

namespace HiAuRo.ACR;

/// <summary>
/// 战斗事件回调处理接口 —— 对齐 AE 风格，10 个回调
/// 由 AIRunner 在 Slot 执行过程中同步调用
/// </summary>
public interface IRotationEventHandler
{
    /// <summary>非战斗情况下每帧触发（远敏唱歌、T切姿态等）</summary>
    void OnPreCombat() { }

    /// <summary>战斗重置时触发（团灭重来、脱战等）</summary>
    void OnResetBattle() { }

    /// <summary>没目标时触发（舞者转阶段提前跳舞等）</summary>
    void OnNoTarget() { }

    /// <summary>读条判定成功后（读条快结束、可滑步的时间点）</summary>
    void OnSpellCastSuccess(Slot slot, Spell spell) { }

    /// <summary>技能使用前</summary>
    void BeforeSpell(Slot slot, Spell spell) { }

    /// <summary>技能使用后（DoT刷新后记录是否强化等）</summary>
    void AfterSpell(Slot slot, Spell spell) { }

    /// <summary>战斗中每帧触发（最常用的回调）</summary>
    void OnBattleUpdate(int battleTimeMs) { }

    /// <summary>切入当前 ACR 时</summary>
    void OnEnterRotation() { }

    /// <summary>从当前 ACR 退出时</summary>
    void OnExitRotation() { }

    /// <summary>切图时触发</summary>
    void OnTerritoryChanged() { }

    /// <summary>游戏事件分发回调。全部 ITriggerCondParams 子类型均转发，ACR 作者自行类型判断过滤。回调在 GameEventHook 线程执行，为只读通知。</summary>
    void OnGameEvent(ITriggerCondParams eventParams) { }

    /// <summary>事实轴阶段切换回调。仅 FactAxis 运行时触发。</summary>
    void OnPhaseChanged(string phaseId, string phaseName) { }
}
```

- [ ] **Step 2: 编译验证**

```bash
cmd.exe /c "dotnet build HiAuRo.slnx -c Release -nologo"
```

预期：编译通过，零错误零警告（已有 ACR 无需改动即可编译）。

- [ ] **Step 3: 提交**

```bash
git add HiAuRo/ACR/Interfaces/IRotationEventHandler.cs
git commit -m "feat: add OnGameEvent and OnPhaseChanged to IRotationEventHandler"
```

---

### Task 2: FactTimeline — 新增 PhaseChanged 事件并在阶段切换时触发

**Files:**
- Modify: `HiAuRo/FactAxis/FactTimeline.cs`

- [ ] **Step 1: 新增 PhaseChanged 公共事件**

在 `FactTimeline` 类的公共成员区域（第 18 行 `public FactState State` 之后）添加：

```csharp
/// <summary>阶段切换事件（phaseId, phaseName）。EnterPhase 和 TrySwitchBranch 中触发。</summary>
public event Action<string, string>? PhaseChanged;
```

- [ ] **Step 2: 在 EnterPhase 方法中触发 PhaseChanged**

修改 `EnterPhase` 方法（约第 140-150 行），在设置完 `_currentPhase` 后触发事件：

```csharp
private void EnterPhase(FactPhase phase)
{
    _currentPhase = phase;
    _currentEvents = phase.Events;
    _eventIndex = 0;
    _pendingSwitch = phase.Switch;
    _waitingSwitch = false;
    _phaseStartTime = FightNow;

    PhaseChanged?.Invoke(phase.Id, phase.Name);  // ← 新增

    DService.Instance().Log.Debug($"[FactAxis] 进入阶段: {phase.Name} ({phase.Events.Count} 事件)");
}
```

- [ ] **Step 3: 在 TrySwitchBranch 方法中触发 PhaseChanged**

修改 `TrySwitchBranch` 方法（约第 155-195 行），在分支切换的 `_currentEvents` 替换完成后触发事件：

找到该方法中 `_phaseStartTime = FightNow;` 这一行（约第 192 行），在其后添加：

```csharp
PhaseChanged?.Invoke(_currentPhase?.Id ?? "", selected.Name);
```

完整上下文（只显示 `TrySwitchBranch` 末尾的修改）：
```csharp
// 替换事件列表
_currentEvents = selected.Events;
foreach (var e in _currentEvents) ResetEvent(e);
_eventIndex = 0;
_pendingSwitch = selected.Switch;
_waitingSwitch = false;
_phaseStartTime = FightNow;

PhaseChanged?.Invoke(_currentPhase?.Id ?? "", selected.Name);  // ← 新增

return true;
```

- [ ] **Step 4: 编译验证**

```bash
cmd.exe /c "dotnet build HiAuRo.slnx -c Release -nologo"
```

预期：编译通过。

- [ ] **Step 5: 提交**

```bash
git add HiAuRo/FactAxis/FactTimeline.cs
git commit -m "feat: add PhaseChanged event to FactTimeline, fire on phase/switch changes"
```

---

### Task 3: Data — 新增 FactState 查询属性

**Files:**
- Modify: `HiAuRo/Data/Data.cs`

- [ ] **Step 1: 新增 FactState 静态属性**

在 `Data` 类中添加 `FactState` 属性（在 `IsReady` 属性之后）：

```csharp
using HiAuRo.FactAxis;  // ← 在文件顶部新增 using

// ... existing code ...

/// <summary>事实轴当前运行时状态快照。FactAxis 未运行时返回 null。</summary>
public static FactAxis.FactState? FactState =>
    FactTimeline.Instance is { IsRunning: true } ? FactTimeline.Instance.State : null;
```

- [ ] **Step 2: 编译验证**

```bash
cmd.exe /c "dotnet build HiAuRo.slnx -c Release -nologo"
```

预期：编译通过。

- [ ] **Step 3: 提交**

```bash
git add HiAuRo/Data/Data.cs
git commit -m "feat: add Data.FactState property exposing fact axis runtime state"
```

---

### Task 4: AIRunner — 订阅并转发游戏事件和阶段切换

**Files:**
- Modify: `HiAuRo/Runtime/AIRunner.cs`

- [ ] **Step 1: 在 Load 方法末尾订阅事件**

修改 `Load` 方法（第 54-83 行），在 `_loaded = true;` 之前添加订阅：

```csharp
// 订阅游戏事件转发到 ACR（现有 Load 结尾 _loaded = true 之前）
GameEventHook.Instance.OnEventFired += OnGameEvent;
FactTimeline.Instance.PhaseChanged += OnPhaseChanged;

_loaded = true;
```

完整 Load 方法末尾（第 79-83 行变为 79-84）：
```csharp
        CurrentEntry.OnEnterRotation();
        CurrentRotation?.EventHandler?.OnEnterRotation();

        // 订阅游戏事件和阶段事件，转发给 ACR
        GameEventHook.Instance.OnEventFired += OnGameEvent;
        FactTimeline.Instance.PhaseChanged += OnPhaseChanged;

        _loaded = true;
    }
```

- [ ] **Step 2: 在 Unload 方法开头取消订阅**

修改 `Unload` 方法（第 86-112 行），在 `if (!_loaded) return;` 之后立即取消订阅：

```csharp
public void Unload()
{
    if (!_loaded) return;

    // 取消事件订阅，防止卸载后仍收到转发
    GameEventHook.Instance.OnEventFired -= OnGameEvent;
    FactTimeline.Instance.PhaseChanged -= OnPhaseChanged;

    CurrentRotation?.EventHandler?.OnExitRotation();
    // ... 后续保持不变
```

- [ ] **Step 3: 新增两个私有转发方法**

在 `AIRunner` 类末尾（第 545 行 `}` 之前）添加两个私有转发方法：

```csharp
    /// <summary>转发游戏事件到当前 ACR 的 EventHandler</summary>
    private void OnGameEvent(ITriggerCondParams eventParams)
    {
        CurrentRotation?.EventHandler?.OnGameEvent(eventParams);
    }

    /// <summary>转发阶段切换到当前 ACR 的 EventHandler</summary>
    private void OnPhaseChanged(string phaseId, string phaseName)
    {
        CurrentRotation?.EventHandler?.OnPhaseChanged(phaseId, phaseName);
    }
}
```

确保文件已有的 footer `}` 在方法之后。添加位置：`ProcessSpellQueue` 方法末尾 `}` 之后，类的结束 `}` 之前。

- [ ] **Step 4: 编译验证**

```bash
cmd.exe /c "dotnet build HiAuRo.slnx -c Release -nologo"
```

预期：编译通过，零错误。

- [ ] **Step 5: 提交**

```bash
git add HiAuRo/Runtime/AIRunner.cs
git commit -m "feat: forward GameEvent and PhaseChanged to ACR EventHandler in AIRunner"
```

---

## 验证清单

1. 编译通过：`cmd.exe /c "dotnet build HiAuRo.slnx -c Release -nologo"` — 零错误零警告
2. 已有 ACR DLL 不改代码重新编译通过（向后兼容验证）
3. 代码审查检查点：
   - `IRotationEventHandler.cs` 两个新方法均为 `{ }` default 实现
   - `AIRunner.Load()` 中订阅发生在 `OnEnterRotation()` 回调之后（ACR 已就绪）
   - `AIRunner.Unload()` 中取消订阅发生在 `OnExitRotation()` 回调之前（防止卸载后收到事件）
   - `FactTimeline.PhaseChanged` 使用 `?.Invoke` 安全调用
   - `Data.FactState` 在 FactAxis 未运行时返回 `null`
   - 线程安全：`OnGameEvent` 在 Hook 线程调用，文档已注明 ACR 作者自行处理
