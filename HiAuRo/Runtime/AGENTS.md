# 运行时核心

## 核心类型

### RuntimeCore — Tick 循环入口
- `Start()` / `Stop()` / `Shutdown()`
- 通过 `OmenService.FrameworkManager.Reg(OnTick)` 注册帧回调
- OnTick: Data.IsReady → Coroutine → CombatContext → EventSystem → HotkeyPoller → ACRLifecycle

### AIRunner — AI 主引擎
- 加载 ACR、调度 IAILoop + SlotExecutor
- `Load(IRotationEntry)` → 卸载旧 ACR, 构建 Rotation, 注册回调
- `Update()` — 每帧核心循环：
  1. 战斗状态检查（Idle/Zoning → 跳过）
  2. 对象/队伍刷新
  3. 无目标 → TargetResolvers 自动选择
  4. 执行轴检查（Phase 6）→ 可能跳过正常循环
  5. 起手序列 → OpenerMgr
  6. 队列 Slot → SpellQueue
  7. AI 循环 → AILoop_Normal.GetNextSlot
  8. SlotExecutor.ExecuteSlot

### AILoop_Normal — GCD/oGCD 双通道 AI 循环
- 实现 `IAILoop`
- `GetNextSlot(blockBuild)` — 依次检查 ISlotResolver 列表
- Check 始终执行，Build 受 blockBuild 控制

### SlotExecutor — Slot 执行器
- `ExecuteSlot(Slot)` → 调用 SpellQueue/技能释放

### 其他组件
- `CombatContext` — 战斗状态机 (Idle/Zoning/Combat)
- `CountDownHandler` — 倒计时行为（通过 IPC）
- `OpenerMgr` — 起手序列管理
- `SpellQueue` — 技能队列（同 GCD 帧多次释放）
- `Coroutine` — 协程系统（技能延迟等）
- `EventSystem` — 事件分发（TargetChanged/SpellCastSuccess 等）
- `ModeSwitch` — 模式切换 (ExecutionAxis ← 当前 / FactAxis ← 未来)
- `ACRLoader` / `ACRLifecycle` — ACR DLL 发现与热加载

## 加载/卸载流程
```
ACRLoader 扫描 DLL → 发现 IRotationEntry → ACRLifecycle 保存
→ 用户切换职业 → ACRLifecycle 触发 LoadEntry → AIRunner.Load(entry)
→ AIRunner.Build → 注入 SlotResolvers/Handler/Opener → 进入循环
```
