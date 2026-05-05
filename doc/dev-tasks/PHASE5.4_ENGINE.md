# Phase 5.4: AI 引擎 + BRD 打样

## 目标

实现 ACR 核心执行引擎（AI 主循环），用 BRD 打样验证全部链路：OmenTools → Data → Runtime → ACR → Actions → UI。

**父阶段**: Phase 5
**依赖**: Phase 5.1 + Phase 5.2 + Phase 5.3
**需求**: ACR-15

## 实现原则

- 参考 AEAssist 的 AI 架构：`IAILoop` 接口 → `AILoop_Normal`(PVE) / `AILoop_PVP` / `AILoop_Simulate` 三种模式
- MVP 只实现 `AILoop_Normal`，PVP 和模拟后续版本再加
- AI 循环 = SlotResolver 遍历 → GCD 窗口判断 → 入队列 → SpellQueue 消费
- BRD 打样只做最小验证（1 GCD + 1 oGCD），不做完整循环

## 文件清单

| 操作 | 文件 | 说明 |
|------|------|------|
| 新建 | `Runtime/IAILoop.cs` | AI 循环模式接口 |
| 新建 | `Runtime/AILoop_Normal.cs` | PVE 正常循环 |
| 新建 | `Runtime/SlotExecutor.cs` | Slot/SlotAction 执行引擎（含 Coroutine） |
| 新建 | `Runtime/SpellQueue.cs` | 技能队列管理 |
| 新建 | `Runtime/AIRunner.cs` | AI 主引擎（调度 IAILoop + SlotExecutor） |
| 新建 | `Jobs/BRD/BRDRotationEntry.cs` | 诗人 ACR 入口 |
| 新建 | `Jobs/BRD/BRDBattleData.cs` | 诗人战斗数据缓存 |
| 新建 | `Jobs/BRD/BRD_GCD_强力射击.cs` | 强力射击 SlotResolver |
| 新建 | `Jobs/BRD/BRD_oGCD_失血箭.cs` | 失血箭 SlotResolver |
| 新建 | `Jobs/BRD/BRDOpener.cs` | 诗人起手示例 |
| 修改 | `Runtime/ACRLifecycle.cs` | 接入 AIRunner |
| 修改 | `Runtime/RuntimeCore.cs` | 调度 AIRunner |

## 任务

### Task 1: IAILoop + AILoop_Normal（ACR 循环逻辑）

**操作**:
1. 新建 `Runtime/IAILoop.cs` — AI 循环模式接口
   ```csharp
   public interface IAILoop
   {
       Slot? GetNextSlot();  // 返回下一个要执行的 Slot（null=无可用）
   }
   ```
2. 新建 `Runtime/AILoop_Normal.cs` — PVE 正常循环
   - 实现 `IAILoop`
   - `GetNextSlot()` 逻辑（构建完整的 Slot）：
     1. 遍历 SlotResolvers 中的 Gcd 模式（Always 也归入 Gcd）
     2. 找第一个 `Check() >= 0` 的 → 调用 `resolver.Build(slot)` 构造 Slot → 返回
     3. oGCD 窗口判定（`GCDHelper.CanUseOffGcd()` + `ActionQueueInMs`）
     4. 如果在 oGCD 窗口：遍历 OffGcd 模式，找第一个 `Check() >= 0` 的 → Build → 返回
   - GCD 窗口规则基于 SettingMgr 的 `ActionQueueInMs`、`MaxAbilityTimesInGcd`、`OptimizeGcd`

**验证**: `dotnet build` 通过；AILoop_Normal 可正确构建 Slot

---

### Task 2: SlotExecutor + SpellQueue + AIRunner（执行引擎）

**操作**:
1. 新建 `Runtime/SlotExecutor.cs` — Slot/SlotAction 执行引擎
   - `ExecuteSlot(Slot slot)` — 按顺序执行 Slot 中的 SlotAction 列表
   - 每个 SlotAction 的执行流程:
     1. 根据 `WaitType` 等待（None=立即 / WaitInMs=Coroutine 延迟 / WaitForSndHalfWindow=等后半 GCD 窗口）
     2. 调用 `UseActionManager.UseAction(spell.Id, targetId)` 执行技能
     3. 技能成功 → 触发 `EventHandler.AfterSpell(slot, spell)`
     4. 技能失败 → 在 `maxDuration` 内重试，超时后跳过
   - 判断 `Spell.Idle` → 等待 100ms 后跳过（不执行技能）
   - GCD 队列：普通 GCD 技能等待 `GCDCooldown - ActionQueueInMs` 后再执行
   - 依赖 Phase 4 的 `Coroutine.WaitAsync()` 处理所有等待逻辑
2. 新建 `Runtime/SpellQueue.cs` — HiAuRo 内部 Slot 调度队列（非 FFXIV 客户端队列）
   - `Enqueue(Slot slot)` — 入队
   - `HasPending()` / `QueueSize`
   - `Clear()` — 清空
   - `GetNext()` — 取下一个待执行的 Slot
3. 新建 `Runtime/AIRunner.cs` — AI 主引擎
   - `Load(IRotationEntry)` — 加载 ACR
   - `Unload()` — 卸载 ACR
   - `Update()` — 每帧流程：
     1. 读 CombatContext，非战斗 → EventHandler.OnPreCombat() → return
     2. 无目标 → EventHandler.OnNoTarget()
     3. 更新战斗计时器 → EventHandler.OnBattleUpdate(battleTimeMs)
     4. 处理 OpenerMgr（起手序列）
     5. SpellQueue 执行队列中的 Slot
     6. 队列空 → IAILoop.GetNextSlot() → SlotExecutor.ExecuteSlot()
     7. TriggerCond 检查 → TriggerAction 执行（Phase 6 补齐）
     8. try-catch 全包裹
    - `Reset()` — 清空 SpellQueue + OpenerMgr.Reset() + EventHandler.OnResetBattle()
4. Slot 内部状态说明（供 SlotExecutor 使用）:
   - `internal bool InSequence` — 标志正在序列中执行（影响失败重试行为）
   - `internal long breakTime` — 下次重试的最后时间点

**验证**: `dotnet build` 通过；SlotExecutor 可正确执行 Slot→SlotAction→UseAction 链路
---

### Task 3: BRD 打样

**操作**:
1. 新建 `Jobs/BRD/BRDRotationEntry.cs`
   ```csharp
   public class BRDRotationEntry : IRotationEntry
   {
       public string AuthorName => "HiAuRo";
       public Rotation? Build(string settingFolder)
       {
           BRDBattleData.Init(settingFolder);
           return new Rotation
           {
               SlotResolvers =
               [
                   new(new BRD_GCD_强力射击(), SlotMode.Gcd),
                   new(new BRD_oGCD_失血箭(), SlotMode.OffGcd),
               ],
               Opener = new BRDOpener(),
           };
       }
       public IRotationUI? GetRotationUI() => null;
       public void OnDrawSetting() { }
       public void Dispose() => BRDBattleData.Reset();
   }
   ```
2. 新建 `Jobs/BRD/BRDBattleData.cs` — 战斗数据缓存（当前目标、歌曲状态等）
3. 新建 `Jobs/BRD/BRD_GCD_强力射击.cs`
   - `Check()`: `SpellHelper.CanUseSpell(SpellsDefine.强力射击) && !正在读条` → 返回 97
4. 新建 `Jobs/BRD/BRD_oGCD_失血箭.cs`
   - `Check()`: `!SpellHelper.IsOnCooldown(SpellsDefine.失血箭)` → 返回 110
5. 新建 `Jobs/BRD/BRDOpener.cs`（可选，最简单起手）
6. 在 `Runtime/ACRLifecycle` 中接入 `AIRunner.Load(entry, settingFolder)`

**验证**:
1. `dotnet build` 通过
2. **游戏内验证**：加载插件 → 开启 BRD ACR → 进入战斗
3. 确认强力射击自动使用（GCD 好了就打）
4. 确认失血箭在 oGCD 窗口自动使用
5. 确认脱战后停止

---

## 阶段验证（MVP 终验）

- [ ] IAILoop.GetNextSlot() 正确构建 Slot
- [ ] SlotExecutor 正确执行 SlotAction 序列
- [ ] SlotAction 的 WaitType（None/WaitInMs/WaitForSndHalfWindow）正确
- [ ] Coroutine.WaitAsync() 在 SlotAction 执行中正确工作
- [ ] `/hi on` 启动 ACR
- [ ] 进入战斗后强力射击自动使用
- [ ] 失血箭在 oGCD 窗口自动使用
- [ ] 脱战后 ACR 自动暂停
- [ ] `/hi off` 停止 ACR
- [ ] 悬浮面板显示职业和状态
- [ ] 设置可持久化，重启不丢失
- [ ] 不依赖 AEAssist 运行时

## 威胁模型

| 威胁 | 类别 | 处置 |
|------|------|------|
| AI 循环中异常导致整个循环停止 | D | try-catch 包裹整个 Update，异常输出日志后 continue |
| 技能连打卡 GCD | D | GCD 窗口规则：后半窗口不打 oGCD |
| 起手中被打断导致状态异常 | D | OpenerMgr 超时/脱战后自动 Reset |
| BRD 打样不是完整循环 | — | 设计如此，打样只验证框架，完整循环由 ACR 作者实现 |

## 进度

| Task | 状态 |
|------|------|
| Task 1: IAILoop + AILoop_Normal | 已完成 |
| Task 2: SlotExecutor + SpellQueue + AIRunner | 已完成 |
| Task 3: BRD 打样 + 全链路验证 | 已完成 |

---

*Created: 2026-05-03*
