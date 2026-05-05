# Phase 4: Runtime Core（运行时核心层）— 开发任务

## 目标

建立每帧 Tick 循环、战斗上下文状态机、ACR 生命周期管理；预埋模式切换骨架。

**依赖**: Phase 3
**需求**: CORE-01, CORE-02, CORE-03, CORE-04, CORE-05

## 实现原则

- 运行时骨架优先保证清晰和可调试，不为了抽象完整度引入额外跳转层
- Tick 循环基于 OmenTools `FrameworkManager.Reg()`（自带节流）
- 模式切换入口预埋但 MVP 阶段不接入

## 文件清单

| 操作 | 文件 | 说明 |
|------|------|------|
| 新建 | `Runtime/RuntimeCore.cs` | 主 Tick 循环入口 |
| 新建 | `Runtime/CombatContext.cs` | 战斗上下文状态机 |
| 新建 | `Runtime/EventSystem.cs` | 战斗事件监听分发 |
| 新建 | `Runtime/Coroutine.cs` | 轻量协程调度器 |
| 新建 | `Runtime/ACRLifecycle.cs` | ACR 生命周期管理 |
| 新建 | `Runtime/ModeSwitch.cs` | 模式切换骨架（预埋） |

## 任务

### Task 1: 主 Tick 循环 + 战斗上下文

**操作**:
1. 新建 `Runtime/RuntimeCore.cs`
   - 通过 `FrameworkManager.Instance.Reg(OnTick, throttleMS: 0)` 注册帧更新
   - `OnTick` 中依次调用 `Coroutine.Instance.Update()` 推进协程、CombatContext 检查、ACRLifecycle 驱动
2. 新建 `Runtime/CombatContext.cs`
   - 状态枚举：Idle / InCombat / OutOfCombat / Zoning
   - 每帧检查 `GameState.IsLoggedIn`、进战斗/脱战/切图条件
   - 提供当前状态查询接口
   - 用 `GameState.IsInInstanceArea` + `DService.Condition` 判断状态切换

**验证**: `dotnet build` 通过；RuntimeCore 挂入 FrameworkManager 帧更新

**完成**: Tick 循环已建立，战斗状态机正确切换

---

### Task 2: EventSystem（战斗事件监听分发）

**操作**:
1. 新建 `Runtime/EventSystem.cs`
   - 负责监听游戏运行时的底层事件，并分发给注册的 Handler
2. 监听的事件来源（基于 OmenTools 的 Hook 系统）:
   - **技能使用**: `UseActionManager` Hook（PreUseAction / PostUseAction）
   - **读条开始/完成**: `UseActionManager` Hook（PreCharacterStartCast / PostCharacterCompleteCast）
   - **目标切换**: 监控 `TargetManager.Target` 变化触发 `OnTargetChanged`
3. 事件分发方法:
   - `OnActionUsed(uint spellId, ulong targetId)` — 技能使用时
   - `OnActionCompleted(uint spellId)` — 技能完成时
   - `OnTargetChanged(IGameObject? newTarget)` — 目标变化时
4. 注册机制:
   - `Register(Action<uint, ulong> onAction)` — 注册回调
   - `Unregister(Action<uint, ulong> onAction)` — 取消注册
5. 战斗状态（进战斗/脱战）不通过 EventSystem 分发
   - 由 `CombatContext` 管理，AI 循环直接读取
6. **时序说明**：
   - EventSystem 是**底层基础设施**，通过 Hook 监听原始游戏事件，提供异步回调
   - Handler 回调（`BeforeSpell` / `AfterSpell` / `OnSpellCastSuccess`）由 AIRunner 在 Slot 执行过程中**同步调用**，不走 EventSystem 的 Hook 回调路径
   - 两者关系：EventSystem 用于框架内部监听底层事件，Handler 用于 ACR 作者同步接收 Slot 执行前后的回调；它们的触发时机和触发方式不同，需要区分对待

**验证**: `dotnet build` 通过；EventSystem 可注册/注销回调

**完成**: 底层游戏事件监听和分发能力已建立

---

### Task 3: ACR 生命周期管理

**操作**:
1. 新建 `Runtime/ACRLifecycle.cs`
   - 持有当前 ACR 的 `IRotationEntry?` 引用
   - Init：进战斗时初始化（后续 Phase 5 接入）
   - Update：每帧驱动（后续 Phase 5 接入）
   - Dispose：脱战/切图时释放
2. 定义 `Update()` 入口，供 Phase 5 的 RotationManager 调用
3. 先做空壳（ACR 实例为 null 时跳过），Phase 5 接入完整逻辑

**验证**: `dotnet build` 通过；ACRLifecycle 与 CombatContext 联动正确

**完成**: ACR Init/Update/Dispose 在正确时机被调用

---

### Task 4: 模式切换骨架

**操作**:
1. 新建 `Runtime/ModeSwitch.cs`
   - 枚举：None / ExecutionAxis / FactAxis
   - 当前仅支持 None
   - 提供 `SetMode()` 和 `GetMode()` 接口
   - 互斥约束：切换模式时先清理前一模式的状态
   - MVP 阶段只预埋，不做实际的模式接入

**验证**: `dotnet build` 通过；ModeSwitch 接口已定义

**完成**: 模式切换入口已预埋，可编译但不影响运行

---

### Task 5: Coroutine 协程系统

**操作**:
1. 新建 `Runtime/Coroutine.cs`
   - 单例模式：`public static Coroutine Instance`
   - 每帧由 `RuntimeCore.OnTick()` 调用 `Update()` 推进所有等待中的协程
2. 核心 API：
   - `WaitAsync(long ms)` — 返回可等待对象，协程暂停指定毫秒后恢复
   - `Update()` — 每帧遍历等待队列，将到期协程唤醒
3. 设计约束：
   - 不引入 C# `Task` / `async` / `await`，全部在帧循环内用时间戳推进
   - 内部使用 `List<CoroutineTask>` 管理等待任务，每帧检查 `currentTime >= dueTime`
   - 超时/异常安全：协程超时不阻塞主循环，异常输出日志后继续
4. 用途：SlotAction 执行过程中的 GCD 等待、动画锁等待、服务器确认等待

**验证**: `dotnet build` 通过；`WaitAsync()` 延迟准确，协程到期后被正确唤醒

**完成**: 轻量协程调度器已运行，不依赖任何第三方异步框架

---

## 阶段验证

- [x] 每帧 Tick 正常调度
- [x] 进战斗 / 脱战 / 切图状态正确切换
- [x] CombatContext 状态与 `GameState.IsLoggedIn` 联动
- [x] EventSystem 可监听技能使用、目标切换等事件
- [x] ACRLifecycle 空壳运行正常（Phase 5 可接入）
- [x] ModeSwitch 骨架可编译
- [x] 没有额外的调度框架层

## 威胁模型

| 威胁 | 类别 | 处置 |
|------|------|------|
| 切图后持有失效对象引用 | D | CombatContext 检测到 Zoning 后清理上下文，再重建 |
| 帧更新中异常导致循环中断 | D | OnTick 包裹 try-catch，异常时输出日志 |
| 模式切换时状态残留 | T | SetMode 先清理旧模式状态，再设置新模式 |

---

## 进度

| Task | 状态 |
|------|------|
| Task 1: Tick 循环 + 战斗上下文 | 已完成 |
| Task 2: EventSystem | 已完成 |
| Task 3: ACR 生命周期 | 已完成 |
| Task 4: 模式切换骨架 | 已完成 |
| Task 5: Coroutine 协程系统 | 已完成 |

---

*Created: 2026-05-03*
