# ACR 游戏事件与事实轴信息暴露 设计文档

**日期**: 2026-05-16  
**类型**: Feature (ACR 接口层增强)

---

## 1. 动机

当前 ACR 作者通过 `IRotationEventHandler` 的 10 个回调获取战斗信息，但无法感知底层游戏事件（boss 读条、Buff 变化、连线等）和事实轴运行时状态（当前阶段、阶段时间）。这限制了 ACR 作者实现更智能的技能决策。

需要：
1. 将 `ITriggerCondParams` 游戏事件全部转发给 ACR
2. 暴露 `FactState` 供 ACR 查询当前副本时间线状态
3. 阶段切换时通知 ACR

---

## 2. 设计决策

| 决策 | 选择 | 理由 |
|------|------|------|
| 事件过滤 | 不过滤，全部转发 | ACR 作者自行类型判断，框架不预设使用场景 |
| 接口位置 | 扩展 `IRotationEventHandler` | 单一入口，default 实现保证向后兼容 |
| FactState 暴露 | 完整 `FactState` + 静态属性 | 简单直接，ACR 按需取用 |
| 阶段通知 | `OnPhaseChanged` 回调 | 配合查询方式，push+pull 双通道 |

---

## 3. 接口变更

### 3.1 IRotationEventHandler 新增方法

**文件**: `HiAuRo/ACR/Interfaces/IRotationEventHandler.cs`

```csharp
/// <summary>
/// 游戏事件分发回调。
/// 全部 18 种 ITriggerCondParams 子类型都会转发到此处，ACR 作者自行判断类型后过滤处理。
/// 回调在 GameEventHook 线程上执行，属于只读通知——修改共享状态请自行加锁。
/// </summary>
void OnGameEvent(ITriggerCondParams eventParams) { }

/// <summary>
/// 事实轴阶段切换回调。
/// 仅在 FactAxis 运行时（FactTimeline.IsRunning）触发。
/// </summary>
void OnPhaseChanged(string phaseId, string phaseName) { }
```

两个方法均为 default 空实现，已有 ACR 无需修改即可编译通过。

### 3.2 Data.FactState 查询属性

**文件**: `HiAuRo/Data/Data.cs`

```csharp
/// <summary>事实轴当前运行时状态快照。FactAxis 未运行时返回 null。</summary>
public static FactAxis.FactState? FactState => FactTimeline.Instance.State;
```

ACR 作者在 `Check()` / `OnBattleUpdate()` / `OnGameEvent()` 中随时查询。

---

## 4. 事件流与调度

### 4.1 游戏事件转发

```
GameEventHook.Instance.OnEventFired       ← Hook 线程
  ↓
AIRunner.OnGameEvent                      ← 订阅/取消订阅在 Load/Unload
  ↓
EventHandler?.OnGameEvent(eventParams)    ← 直接转发，零过滤
```

**线程说明**: `GameEventHook.OnEventFired` 在 Hook 线程执行，`EventHandler.OnGameEvent` 在同一调用栈上同步执行。ACR 作者需要自行处理线程安全。

### 4.2 阶段切换转发

`FactTimeline` 新增公共事件：

```csharp
// FactTimeline.cs
public event Action<string, string>? PhaseChanged;  // (phaseId, phaseName)
```

当 `_currentPhase` 变更时触发。AIRunner 在 `Load()` 时订阅，转发到 `EventHandler.OnPhaseChanged`。

### 4.3 生命周期

```
Load()   → 订阅 GameEventHook.OnEventFired + FactTimeline.PhaseChanged
Unload() → 取消订阅
```

ACR 不存在 → 无事件投递。多 ACR 切换时旧 ACR 的 Unload 正确解绑。

---

## 5. 文件变更清单

| 文件 | 变更 |
|------|------|
| `HiAuRo/ACR/Interfaces/IRotationEventHandler.cs` | 新增 `OnGameEvent`、`OnPhaseChanged` 两个 default 方法 |
| `HiAuRo/Runtime/AIRunner.cs` | `Load()` → 订阅事件，`Unload()` → 取消订阅；两个私有方法转发事件 |
| `HiAuRo/FactAxis/FactTimeline.cs` | 新增 `PhaseChanged` 公共事件，`_currentPhase` 赋值时触发 |
| `HiAuRo/Data/Data.cs` | 新增 `static FactState? FactState` 属性 |

---

## 6. 验证

1. 编译通过：`dotnet build HiAuRo.slnx -c Release`
2. 已有 ACR 不改代码零警告编译
3. 运行时可观测 `OnGameEvent` 收到各类事件；`Data.FactState` 在 FactAxis 未运行时为 `null`
