# Phase 5.1: ACR 核心接口 + 常量

## 目标

定义 ACR 最基础的接口和数据结构，让 ACR 作者可以先搭骨架。本阶段只做"能编译的接口层"，不做运行时。

**父阶段**: Phase 5
**依赖**: Phase 4
**需求**: ACR-01, ACR-02, ACR-03, ACR-08

## 实现原则

- 接口直接平铺在 `ACR/` 目录，不建子目录
- 命名和概念严格对齐 AE：Rotation / SlotResolverData / SlotMode / ISlotResolver（Check + Build 双方法）
- 技能和 BUFF 常量用中文命名，ACR 作者一眼能懂

## 文件清单

| 操作 | 文件 | 说明 |
|------|------|------|
| 新建 | `ACR/IRotationEntry.cs` | 职业执行器统一接口 |
| 新建 | `ACR/IRotationUI.cs` | 职业悬浮窗 UI 接口（RegisterControls → IUiBuilder） |
| 新建 | `ACR/IUiBuilder.cs` | 描述性 UI 控件注册接口 |
| 新建 | `ACR/Rotation.cs` | Rotation 容器 |
| 新建 | `ACR/SlotMode.cs` | Gcd / OffGcd / Always |
| 新建 | `ACR/ISlotResolver.cs` | 槽位解析器接口（Check + Build 双方法） |
| 新建 | `ACR/SlotResolverData.cs` | 技能槽位数据（ISlotResolver + SlotMode） |
| 新建 | `ACR/SpellTargetType.cs` | 技能目标类型枚举（Self/Target/TargetTarget/Party1-8/Location 等） |
| 新建 | `ACR/SpellCategory.cs` | 技能分类枚举（Default/LimitBreak/Potion/Sprint/Dance/Item） |
| 新建 | `ACR/SpellType.cs` | 技能类型枚举（None/RealGcd/GeneralGcd/Ability） |
| 新建 | `ACR/Spell.cs` | 技能定义（ID、名称、目标类型、SpellCategory、Spell.Idle 等） |
| 新建 | `ACR/SlotAction.cs` | 单个技能执行行为（Spell + WaitType + 延迟） |
| 新建 | `ACR/Slot.cs` | 技能执行单元（一组 SlotAction + 尝试时间 + 序列追加） |
| 新建 | `ACR/SpellsDefine.cs` | 常用技能 ID（中文） |
| 新建 | `ACR/AurasDefine.cs` | 常用 BUFF ID（中文） |
| 新建 | `ACR/Jobs.cs` | 战斗职业枚举（如项目已引用 OmenTools，可别名其职业枚举） |
| 新建 | `ACR/JobsCategory.cs` | 职业职能分类（Tank/Healer/Melee/Ranged/Caster） |

## 任务

### Task 1: IRotationEntry + IRotationUI + Rotation

**操作**:
1. 新建 `ACR/IRotationEntry.cs`
   ```csharp
   /// ACR 作者入口接口
   public interface IRotationEntry
   {
       string AuthorName { get; }                  // 作者名
       bool UseCustomUi { get; }                   // false=HiAuRo IUiBuilder / true=ACR 自带 HTML
       Rotation? Build(string settingFolder);      // settingFolder = ACR DLL 所在目录
       IRotationUI? GetRotationUI();               // UseCustomUi=true 时返回 null
       void OnDrawSetting();                       // 可选设置页
       void Dispose();                             // 资源释放
   }
   ```
   **两种 UI 模式**:
   - `UseCustomUi == false`（默认）：ACR 通过 `IRotationUI.RegisterControls(IUiBuilder)` 注册控件，HiAuRo 转 JSON → Web 前端渲染
   - `UseCustomUi == true`：ACR 作者在 DLL 同目录下放置 `settings.html` / `jobview.html`，HiAuRo 直接提供这些文件给前端
     - 路由：`GET /acr/{acrName}/settings` → `{settingFolder}/settings.html`
     - 自定义 HTML 可通过同一个 WebSocket (`ws://localhost:5678/ws`) 与 HiAuRo 通信
2. 新建 `ACR/IRotationUI.cs` — ACR 作者 UI 定义接口
   ```csharp
   /// ACR 作者通过此接口注册 UI 控件，HiAuRo 自动转为 Web 前端渲染
   public interface IRotationUI
   {
       void RegisterControls(IUiBuilder builder);  // 注册 QT 开关、热键、设置控件
   }
   ```
3. 新建 `ACR/IUiBuilder.cs` — 描述性 UI 控件注册接口
   ```csharp
   /// C# 描述 UI，HiAuRo 转为 JSON → Web 前端渲染为 HTML 控件
   public interface IUiBuilder
   {
       // 布局
       void AddTab(string id, string title);         // 切换到新 Tab 页，后续控件归属该 Tab
       void AddGroup(string id, string title);       // 可折叠分组（CollapsingHeader）
       void AddSeparator();                           // 分隔线
       void AddSameLine();                            // 下一个控件同行排列

       // 控件
       void AddCheckbox(string id, string label, bool defaultValue);     // QT 开关
       void AddSlider(string id, string label, float min, float max, float defaultValue);
       void AddDropdown(string id, string label, string[] options, string defaultValue);
       void AddHotkey(string id, string label, string defaultKey);       // 热键绑定
       void AddIntInput(string id, string label, int defaultValue, int step = 1, int stepFast = 10);
       void AddLabel(string id, string text);

       // 辅助
       void AddTooltip(string targetId, string tooltip);  // 悬浮提示（关联到上一个控件）
   }
   ```
   **Tab 说明**：`AddTab` 调用后所有控件归属该 Tab，直到下一个 `AddTab`。前端渲染为 TabBar。**分组说明**：`AddGroup` / `EndGroup` 支持嵌套，可在 Tab 内使用。
4. 新建 `ACR/Rotation.cs` — 容器类，持有所有子组件引用

   Rotation 完整字段：
   ```csharp
   public class Rotation
   {
       // === MVP 必需字段 ===
       public List<SlotResolverData> SlotResolvers;   // 技能槽位列表
       public List<ISlotSequence> SlotSequences;      // 技能序列
       public IOpener? Opener;                        // 起手爆发
       public IRotationEventHandler? EventHandler;     // 战斗事件回调
        public List<ITriggerAction> TriggerActions;     // 触发动作（Phase 6+ 补齐）
        public List<ITriggerCond> TriggerConditions; // 触发条件（Phase 6+ 补齐）

       public Jobs TargetJob;            // 适配职业（MVP 必需）
       public AcrType AcrType;           // ACR 类型：Both / PvE / PvP（MVP 必需）
       public int MinLevel;              // 最低等级（MVP 必需）
       public int MaxLevel;              // 最高等级（MVP 必需）
       public string Description;        // 描述文本（MVP 必需）

       public List<ITargetResolver> TargetResolvers;       // 目标选择器（MVP 必需）
       public List<IHotkeyEventHandler> HotkeyEventHandlers; // 热键事件处理（MVP 必需）

       // === 可选字段 ===
       public Func<int>? CanPauseACRCheck;  // 暂停 ACR 的条件检查（返回 >0 暂停）

       // === Phase 6+ 预埋字段 ===
       public Func<int>? CanUseHighPrioritySlotCheck; // 高优先级技能插入合法性检查（Phase 6 预埋）
   }
   ```

   Rotation 链式调用方法：
   ```csharp
   public Rotation AddOpener(IOpener opener);
   public Rotation AddSlotSequences(params ISlotSequence[] seqs);
    public Rotation AddTriggerAction(ITriggerAction action);
    public Rotation AddTriggerCondition(ITriggerCond cond);
   public Rotation AddTargetResolver(ITargetResolver resolver);
   public Rotation AddHotkeyEventHandlers(params IHotkeyEventHandler[] handlers);
   public Rotation AddCanPauseACRCheck(Func<int> check);
   ```

**验证**: `dotnet build` 通过

---

### Task 2: SlotMode / ISlotResolver / SlotResolverData

**操作**:
1. 新建 `ACR/SlotMode.cs` → `enum SlotMode { Gcd, OffGcd, Always }`
2. 新建 `ACR/ISlotResolver.cs` — 双方法接口：
   ```csharp
   public interface ISlotResolver
   {
       /// <summary>
       /// 检查优先级/可用性。>=0 表示可用（返回值越大优先级越高），<0 表示禁止。
       /// 注意：返回值不是技能 ID，而是优先级/可用性判断值。
       /// </summary>
       int Check();

       /// <summary>
       /// 构建 Slot 对象。Check() >= 0 时由 AI 引擎调用，内部通过 slot.Add(spell)、
       /// slot.Add2NdWindowAbility(spell)、slot.AddDelaySpell(delay, spell)、
       /// slot.AppendSequence(seq) 等 API 填充 SkillAction。
       /// </summary>
       void Build(Slot slot);
   }
   ```
3. 新建 `ACR/SlotResolverData.cs` → `ISlotResolver Resolver` + `SlotMode Mode`

**验证**: `dotnet build` 通过；ISlotResolver 双方法与 AE 概念对齐

---

### Task 3: Spell / SpellTargetType / SpellCategory / SpellType / SlotAction / Slot

**操作**:

1. 新建 `ACR/SpellTargetType.cs` — 技能目标类型枚举：
   - `Self` — 自身
   - `Target` — 当前目标
   - `TargetTarget` — 目标的目标
   - `Pm1` ~ `Pm8` — 小队成员 1~8
   - `SpecifyTarget` — 指定目标对象（配合 Spell.SpecifyTarget 字段）
   - `Location` — 地面放置技能（配合 Spell.UsePos 坐标）
   - `DynamicTarget` — 动态目标（配合 Spell.GetDynamicsTarget 委托）
   - `MapCenter` — 地图中心
   - 注释：参考 AE 11 种目标类型定义，MVP 阶段至少实现 Self/Target/TargetTarget

2. 新建 `ACR/SpellCategory.cs` — 技能分类枚举：
   - `Default` — 普通技能
   - `LimitBreak` — LB
   - `Potion` — 爆发药
   - `Sprint` — 疾跑
   - `Dance` — 舞蹈
   - `Item` — 道具

3. 新建 `ACR/SpellType.cs` — 技能类型枚举：
   - `None` — 未分类
   - `RealGcd` — 真实 GCD 技能（受 GCD 冷却约束）
   - `GeneralGcd` — 通用 GCD 技能
   - `Ability` — 能力技（不受 GCD 约束）

4. 新建 `ACR/Spell.cs` — 技能定义：
   - 字段：
     - `uint Id` — 技能 ID
     - `string Name` — 技能名称
     - `SpellTargetType TargetType` — 目标类型
     - `SpellCategory SpellCategory` — 技能分类
     - `SpellType Type` — 技能类型（GCD/能力技）
     - `object? SpecifyTarget` — 指定目标对象（配合 SpellTargetType.SpecifyTarget）
     - `Func<object?>? GetDynamicsTarget` — 动态目标委托（配合 SpellTargetType.DynamicTarget）
     - `Vector3? UsePos` — 地面技能坐标（配合 SpellTargetType.Location）
     - `bool DontUseGcdOpt` — 不使用 GCD 偏移优化
     - `bool WaitServerAcq` — 等待服务器确认
   - 方法：
     - `GetTarget()` — 根据 TargetType 返回合适的目标 ID
     - `IsAbility()` 扩展方法 — 判断是否为能力技（SpellType == Ability）
   - 静态实例：
     ```csharp
     public static readonly Spell Idle = new()
     {
         Id = 0,
         Name = "Idle",
         TargetType = SpellTargetType.Self,
         Type = SpellType.None
     };
     ```
     Spell.Idle 是哨兵技能（ID=0），SlotAction.Run() 遇到它时等待 100ms 而非尝试释放，用于"空转等待"场景。
   - SpellTargetLimit（如 HP 阈值过滤、职业过滤）标注为 **Phase 6+ 补充**，MVP 阶段 `Spell.GetTarget()` 不含限制型过滤。

5. 新建 `ACR/SlotAction.cs` — 单个技能执行行为：
   - `Spell Spell` — 要用的技能
   - `WaitType Wait` — None(立即) / WaitInMs(延迟) / WaitForSndHalfWindow(后半GCD)
   - `int TimeInMs` — 延迟毫秒数
   - `int MaxDuration` — 执行失败最大尝试时间（默认 1000ms）

6. 新建 `ACR/Slot.cs` — 技能执行单元，可包含连续多个 SlotAction：
   - `List<SlotAction> Actions` — 要按顺序执行的技能列表
   - `int MaxDuration` — 整体失败尝试时间（默认 600ms）
   - `bool Wait2NextGcd` — 强制延后到下个 GCD
   - `AppendSequence(ISlotSequence)` — Slot 快结束时追加序列
   - 常用构建 API：
     - `Add(Spell spell)` — 添加一个技能到 Actions
     - `Add2NdWindowAbility(Spell spell)` — 在第二个能力技窗口添加
     - `AddDelaySpell(int delayMs, Spell spell)` — 延迟后添加技能
   - 用途：一个 Slot 可以 = GCD + 能力技、能力技 + GCD、单个技能、或组合序列

7. Slot 和 ISlotResolver 的关系：
   - `ISlotResolver.Check()` 返回 int（>=0 表示可用，返回优先级/可用性判断值，不是技能 ID）
   - Check() >= 0 时，AI 引擎调用 `ISlotResolver.Build(Slot slot)` 构建 Slot
   - Build(slot) 内部调用 `slot.Add(spell)`、`slot.Add2NdWindowAbility(spell)`、`slot.AddDelaySpell(delay, spell)` 等 API 填充 SkillAction
   - 构建完成的 Slot 被排入 SpellQueue 等待执行
   - EventHandler 的回调中传递 Slot 和 Spell，不是裸 uint

**验证**: `dotnet build` 通过；Spell 含 SpellTargetType/SpellCategory/SpellType 三枚举；Slot/SlotAction/Spell 可被创建和管理

---

### Task 4: SpellsDefine + AurasDefine

**操作**:
1. 新建 `ACR/SpellsDefine.cs` — 常用技能 ID（中文命名），至少含 BRD 打样用到的
2. 新建 `ACR/AurasDefine.cs` — 常用 BUFF/DOT ID（中文命名）

**验证**: `dotnet build` 通过；常量命名可读

---

## 阶段验证

- [ ] `dotnet build` 通过
- [ ] `IRotationEntry` 接口可用
- [ ] `ISlotResolver` 双方法（Check + Build）与 AE 概念对齐
- [ ] `SlotResolverData` + `SlotMode` 定义正确
- [ ] `Spell` 含 SpellTargetType / SpellCategory / SpellType / Spell.Idle
- [ ] `SpellsDefine` / `AurasDefine` 中文命名清晰

## 进度

| Task | 状态 |
|------|------|
| Task 1: IRotationEntry + Rotation | 已完成 |
| Task 2: SlotMode / ISlotResolver / SlotResolverData | 已完成 |
| Task 3: Spell / SpellTargetType / SpellCategory / SpellType / SlotAction / Slot | 已完成 |
| Task 4: SpellsDefine + AurasDefine | 已完成 |

---

*Created: 2026-05-03*
