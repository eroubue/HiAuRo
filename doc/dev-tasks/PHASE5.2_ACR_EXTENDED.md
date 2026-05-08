# Phase 5.2: 起手 + 序列 + 触发器 + 战斗 Helper

## 目标

扩展 ACR 框架：提供起手爆发、技能序列、触发器接口，以及 ACR 作者日常开发最常用的战斗工具 Helper。

**父阶段**: Phase 5
**依赖**: Phase 5.1
**需求**: ACR-04, ACR-05, ACR-06, ACR-07, ACR-09

## 实现原则

- IOpener / ISlotSequence 接口保持简单，和 AE 风格一致
- CountDownHandler（倒计时管理器）预埋在 Runtime 层，MVP 阶段仅搭建空壳
- ITriggerAction / ITriggerCond / ITargetResolver / IRotationEventHandler 先只定义接口，Phase 6 执行轴阶段再接入完整触发器引擎
- Helper 类全部 static，方法短平快，直接组合 Data 层和 OmenTools API

## 文件清单

| 操作 | 文件 | 说明 |
|------|------|------|
| 新建 | `ACR/IOpener.cs` | 起手爆发接口（继承 ISlotSequence） |
| 新建 | `ACR/OpenerMgr.cs` | 起手管理器 |
| 新建 | `ACR/ISlotSequence.cs` | 技能序列接口（AE 对齐版） |
| 新建 | `ACR/ITriggerAction.cs` | 触发动作接口 |
| 新建 | `ACR/ITriggerCond.cs` | 触发条件接口 |
| 新建 | `ACR/ITriggerCondParams.cs` | 触发条件参数接口（空壳预埋） |
| 新建 | `ACR/ITriggerBase.cs` | 触发器基础接口（空壳预埋） |
| 新建 | `ACR/ITargetResolver.cs` | 目标选择器接口 |
| 新建 | `ACR/IRotationEventHandler.cs` | 战斗事件处理接口 |
| 新建 | `Runtime/CountDownHandler.cs` | 倒计时管理器 |
| 新建 | `ACR/GCDHelper.cs` | GCD 剩余时间 / 窗口判断 |
| 新建 | `ACR/SpellHelper.cs` | 技能可用性、冷却、距离 |
| 新建 | `ACR/TargetHelper.cs` | 目标选择、敌人数、身位 |
| 新建 | `ACR/AuraHelper.cs` | Buff/DOT 检测 |
| 新建 | `ACR/CooldownHelper.cs` | 充能技能冷却 |

## 任务

### Task 1: IOpener + OpenerMgr + ISlotSequence

**操作**:
1. 新建 `ACR/ISlotSequence.cs`（IOpener 依赖此接口，先建）
   ```csharp
   /// 技能序列接口（与 AE 对齐：
   /// AE 原版为 List<Action<Slot>> + StartCheck/StopCheck，
   /// HiAuRo 版采用 Action<Slot> 委托列表模拟 AE 的 Sequence 构建方式）
   public interface ISlotSequence
   {
       /// 技能构建步骤列表：每个 Action 接收 Slot 对象并调用 Add(spell)/AddDelaySpell() 等方式构建
       List<Action<Slot>> Sequence { get; }
       /// 返回 >=0 表示可以启动序列，<0 表示不可启动
       int StartCheck();
       /// 返回 >=0 表示可以在第 index 步中断，<0 表示不可中断
       int StopCheck(int index);
   }
   ```
   - 用途：组合按键场景（如诗人歌曲切换 = 切歌 GCD + 触发歌的效果 oGCD）
   - 每个 Action<Slot> 构造一个 Slot 内的技能执行单元，通过 Slot 的 Add/AddDelaySpell 等方法填充

2. 新建 `ACR/IOpener.cs`
   ```csharp
   /// 起手爆发接口。起手本质是一个固定的技能序列，因此继承 ISlotSequence。
   /// 注意：AE 原版 IOpener 还继承 IScript，HiAuRo MVP 暂不引入 IScript。
   public interface IOpener : ISlotSequence
   {
       uint Level { get; }
       /// 倒计时阶段行为注册（如黑魔预读等）
       /// MVP 阶段预埋接口，具体行为在 Phase 6+ 实现
       void InitCountDown(CountDownHandler handler);
   }
   ```

3. 新建 `ACR/OpenerMgr.cs`
   - 状态机：NotStarted → Running → Finished
   - `StartCheck()` → 从 ISlotSequence 继承，判断是否可以启动
   - `StopCheck(int index)` → 判断是否可以在第 index 步中断
   - `Start(IOpener)` — 开始执行起手，调用 opener.StartCheck() 前置判定
   - `Update()` — 每帧推进，逐个执行 Sequence 中的 Action<Slot>
   - 起手完成或被打断时自动 Reset

4. 新建 `Runtime/CountDownHandler.cs`
   - 管理倒计时阶段行为，提供 `AddAction(int timeleft, uint spellId, SpellTargetType targetType)` API
   - 倒计时期间每帧 Update 推进，检查是否有到时的注册行为并执行
   - MVP 阶段仅搭建空壳，具体行为在 Phase 6+ 实现

**验证**: `dotnet build` 通过；接口可用

---

### Task 2: ITriggerAction + ITriggerCond + ITriggerBase + ITriggerCondParams + IRotationEventHandler（接口占位）

**操作**:
1. 新建 `ACR/ITriggerBase.cs` — 触发器基础接口（空壳预埋，为 Phase 6 统一 Handle 签名准备）
   ```csharp
   public interface ITriggerBase { }
   ```
2. 新建 `ACR/ITriggerCondParams.cs` — 触发条件参数接口（空壳预埋，Phase 6 传入触发上下文）
   ```csharp
   /// 触发条件参数传递机制（空壳预埋），携带触发上下文（如哪个敌人读条、什么技能等）
   public interface ITriggerCondParams { }
   ```
3. 新建 `ACR/ITriggerAction.cs`
   ```csharp
   /// 触发动作接口（对齐 AE 签名：bool Handle()，返回 true 表示已处理）
   public interface ITriggerAction : ITriggerBase
   {
       bool Handle();
   }
   ```
4. 新建 `ACR/ITriggerCond.cs`
   ```csharp
   /// 触发条件接口（对齐 AE 签名：bool Handle(ITriggerCondParams)，携带条件参数上下文）
   public interface ITriggerCond : ITriggerBase
   {
       bool Handle(ITriggerCondParams? condParams = null);
   }
   ```
5. 新建 `ACR/IRotationEventHandler.cs`
   ```csharp
   /// 常用战斗事件回调处理（对齐 AE 风格）
   public interface IRotationEventHandler
   {
       /// 非战斗情况下每帧触发（远敏唱歌、T切姿态等）
       void OnPreCombat();
       
       /// 战斗重置时触发（团灭重来、脱战等）
       void OnResetBattle();
       
       /// 没目标时触发（舞者转阶段提前跳舞等）
       void OnNoTarget();
       
       /// 读条判定成功后（读条快结束、可滑步的时间点）
       void OnSpellCastSuccess(Slot slot, Spell spell);
       
       /// 技能使用前
       void BeforeSpell(Slot slot, Spell spell);
       
       /// 技能使用后（Dot刷新后记录是否强化等）
       void AfterSpell(Slot slot, Spell spell);
       
       /// 战斗中每帧触发（最常用的回调）
       void OnBattleUpdate(int battleTimeMs);
       
       /// 切到当前 ACR 时
       void OnEnterRotation();
       
       /// 从当前 ACR 退出时
       void OnExitRotation();
       
       /// 切图时触发
       void OnTerritoryChanged();
   }
   ```
   - `OnSpellCastSuccess` / `BeforeSpell` / `AfterSpell` 接收 `Slot` 和 `Spell`，不是裸 uint
   - Phase 4 的 `EventSystem` 负责触发这些回调
   - `OnPreCombat` / `OnNoTarget` / `OnBattleUpdate` 由 AIRunner 每帧根据状态调度
   - `OnSpellCastSuccess` / `BeforeSpell` / `AfterSpell` 由 EventSystem 的 UseAction Hook 触发
    - `OnResetBattle` / `OnEnterRotation` / `OnExitRotation` / `OnTerritoryChanged` 由 CombatContext + ACRLifecycle 触发
6. Reflection 中的 `TriggerActions` / `TriggerConditions` 列表在 Phase 5 保持空列表
   - 具体触发器实现（TriggerCond_敌人读条、TriggerAction_切换目标 等 30+ 种）留到 Phase 6 执行轴阶段补齐
   - Phase 5 的 AIRunner 检查 TriggerCond 列表 → 如果列表为空则跳过

**验证**: `dotnet build` 通过；接口可被 ACR 作者实现；空列表不导致异常

---

### Task 3: ITargetResolver（目标选择器接口）

**操作**:
1. 新建 `ACR/ITargetResolver.cs`
   ```csharp
   /// 目标选择器接口：在进战前/进战后主动选择目标。
   /// Rotation 可通过 AddTargetResolver() 注册多个选择器，按顺序调用。
   public interface ITargetResolver
   {
       /// 尝试选择目标，成功时通过 out 参数返回目标对象
       /// 返回 true 表示成功选择目标，false 表示无法选择
       bool ResolveTarget(out IBattleChara agent);
   }
   ```
2. Rotation 中增加 `List<ITargetResolver> TargetResolvers` 和 `AddTargetResolver(params ITargetResolver[])` 方法
3. 用途：近战切换目标、法系智能选择、AOE 找最优位置目标
4. MVP 阶段仅定义接口，具体选择器实现（如优先选中 HP 最低敌人、优先选中读条敌人等）在 Phase 6+ 补充

**验证**: `dotnet build` 通过；接口可被 ACR 作者实现

---

### Task 4: 战斗 Helper（GCD / Spell / Target / Aura / Cooldown）

**操作**:
1. 新建 `ACR/GCDHelper.cs`
   - `GetGCDCooldown()` — GCD 剩余毫秒
   - `GetGCDDuration()` — GCD 总时长
    - `CanUseOffGcd()` — 是否可在 oGCD 窗口插入（前半窗口 < 750ms 或后半 > 1500ms）
    - 最终 oGCD 窗口阈值以 `SettingMgr.ActionQueueInMs` 为准，`GCDHelper` 读取该配置而非硬编码
2. 新建 `ACR/SpellHelper.cs`
   - `CanUseSpell(uint id)` — 综合判断（冷却、距离、等级、资源）
   - `GetSpellCooldown(uint id)` — 剩余冷却毫秒
   - `IsInRange(uint id, target)` — 目标是否在技能射程内
   - `GetCharges(uint id)` — 充能技能当前层数
3. 新建 `ACR/TargetHelper.cs`
   - `GetNearbyEnemyCount(target, range)` — 周围敌人数（AOE 判断核心）
   - `IsBehind(target)` / `IsFlanking(target)` — 身位判定
   - `FindBestAoeTarget(spellId, count)` — 最优 AOE 目标
4. 新建 `ACR/AuraHelper.cs`
   - `HasAura(target, buffId)` / `HasAnyAura(target, buffIds)`
   - `GetAuraTimeLeft(target, buffId)` — 剩余毫秒
   - 支持自己 / 当前目标 / 指定对象三种场景
5. 新建 `ACR/CooldownHelper.cs`
   - `IsOnCooldown(spellId)` — 是否在冷却中
   - `GetCooldownRemaining(spellId)` — 剩余冷却毫秒
   - `GetChargeCooldown(spellId)` — 充能技能距下次充能的毫秒数
6. 以上全部 static class，方法直接组合 `DService.*` 和 `Data.*`，中文注释优先

**验证**: `dotnet build` 通过；每个 Helper 方法在 IDE 中可查看文档注释

---

## 阶段验证

- [ ] `dotnet build` 通过
- [ ] IOpener / ISlotSequence / ITriggerAction / ITriggerCond / ITargetResolver / IRotationEventHandler 接口完整
- [ ] OpenerMgr 状态机正确
- [ ] 5 个 Helper 类方法可被调用
- [ ] Helper 方法可被 ACR 作者在 SlotResolver.Check() 中直接组合使用

## 进度

| Task | 状态 |
|------|------|
| Task 1: IOpener + OpenerMgr + ISlotSequence + CountDownHandler | 已完成 |
| Task 2: ITriggerAction + ITriggerCond + ITriggerBase + ITriggerCondParams + IRotationEventHandler | 已完成 |
| Task 3: ITargetResolver（目标选择器接口） | 已完成 |
| Task 4: 战斗 Helper（GCD/Spell/Target/Aura/Cooldown） | 已完成 |

---

*Created: 2026-05-03*
