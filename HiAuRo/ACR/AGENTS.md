# ACR 抽象层

## 核心接口

### IRotationEntry — ACR 作者入口
- `Build(string settingFolder)` → `Rotation?`
- `GetRotationUI()` → `IRotationUI?`（声明式 UI 注册）
- `UseCustomUi` — false=HiAuRo IUiBuilder, true=ACR 自带 HTML
- `TargetJobs` — 支持的职业列表（ACRLoader 自动发现）
- `OnEnterRotation()` / `OnExitRotation()` — 生命周期回调
- `Dispose()` — 资源释放

### Rotation — ACR 容器
```
SlotResolvers    ← List<SlotResolverData>  (GCD/oGCD 技能槽)
SlotSequences    ← List<ISlotSequence>      (技能序列)
Opener           ← IOpener?                  (起手序列)
EventHandler     ← IRotationEventHandler?    (事件回调)
TriggerActions   ← List<ITriggerAction>      (全局触发器·动作)
TriggerConditions← List<ITriggerCond>       (全局触发器·条件)
TargetResolvers  ← List<ITargetResolver>    (自动目标选择)
HotkeyEventHandlers ← List<IHotkeyEventHandler>
```

### ISlotResolver — 技能槽位
- `int Check()` — ≥0 可用（值越大优先级越高），<0 禁止
- `void Build(Slot slot)` — 构建执行单元

### IRotationUI — 声明式 UI (IUiBuilder)
- `AddCheckbox / AddDropdown / AddHotkey / AddTab / AddGroup / AddMainControl`
- 翻译为 JSON → Web 前端

## 文件命名约定
中文友好命名：`BRD_GCD_强力射击.cs`、`HotkeyResolver_吃药.cs`

## 关键数据类型
- `Spell` / `Slot` / `SlotAction` — 执行单元
- `SlotMode` — Gcd / OffGcd / Any
- `SpellsDefine` — 技能定义（阶段/ID）
- `Jobs` — 职业枚举（BRD/MNK/WHM/...）

## Helpers (辅助类)
- `GCDHelper` — GCD 时间控制
- `CooldownHelper` — 技能冷却
- `AuraHelper` — Buff/Debuff 检查
- `ComboHelper` — 连击状态
- `SpellHelper` / `SpellHistoryHelper` — 技能状态/历史
- `TargetHelper` / `QTHelper` / `HotkeyHelper` — 目标/QT/热键
- `ItemHelper` — 道具使用
