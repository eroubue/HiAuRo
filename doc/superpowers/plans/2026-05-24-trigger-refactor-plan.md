# Trigger 序列化 + IUiBuilder Draw 改造 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use subagent-driven-development (recommended) or executing-plans to implement task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 把所有内置 ITriggerCond / ITriggerAction 的 `private readonly` 字段改为 public 属性，添加 `Draw(IUiBuilder)` 方法，迁移到 STJ 全量反序列化

**Architecture:** Edit→Save→Load 周期：Web 编辑→JSON→C# STJ 反序列化→公共属性。`Draw()` 只声明 UI 形状（字段名+类型），不负责实时值绑定

**Tech Stack:** C# 13, .NET 10, System.Text.Json, IUiBuilder (C#→JSON→Web)

---

### Task 1: IUiBuilder 扩展 — 新增 AddFloatInput + AddTextInput

**Files:**
- Modify: `HiAuRo/ACR/Interfaces/IUiBuilder.cs:40-41`
- Modify: `HiAuRo/UI/UiBuilderImpl.cs`
- Modify: `HiAuRo/UI/web/app.js` (web 渲染)
- Modify: `HiAuRo/UI/UiControlDef.cs` (如果类型不对)

- [ ] **Step 1: IUiBuilder 接口加方法**

在 `AddIntInput` 后面插入：

```csharp
/// <summary>浮点数输入</summary>
void AddFloatInput(string label, float defaultValue);

/// <summary>文本输入</summary>
void AddTextInput(string label, string defaultValue);
```

- [ ] **Step 2: UiBuilderImpl 实现**

```csharp
public void AddFloatInput(string label, float defaultValue) =>
    _controls.Add(new UiControlDef(Id(label), "floatInput", _currentGroup, label, defaultValue));

public void AddTextInput(string label, string defaultValue) =>
    _controls.Add(new UiControlDef(Id(label), "textInput", _currentGroup, label, defaultValue ?? ""));
```

- [ ] **Step 3: app.js 添加 floatInput / textInput 渲染**

在 `app.js` 的 `renderItems()` 中，在 `case 'intInput':` 分支后添加：

```javascript
case 'floatInput':
    html += `<div class="ctrl-row"><label>${esc(c.label)}</label><input type="number" step="any" id="${esc(c.id)}" value="${esc(c.value)}" onchange="ctlChanged('floatInput','${esc(c.id)}',parseFloat(this.value))" /></div>`;
    break;
case 'textInput':
    html += `<div class="ctrl-row"><label>${esc(c.label)}</label><input type="text" id="${esc(c.id)}" value="${esc(c.value)}" onchange="ctlChanged('textInput','${esc(c.id)}',this.value)" /></div>`;
    break;
```

- [ ] **Step 4: 编译 + 验证**

```bash
DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 dotnet build HiAuRo/HiAuRo.csproj -nologo
```

- [ ] **Step 5: 提交**

```bash
git add HiAuRo/ACR/Interfaces/IUiBuilder.cs HiAuRo/UI/UiBuilderImpl.cs HiAuRo/UI/web/app.js
git commit -m "feat: IUiBuilder 新增 AddFloatInput / AddTextInput"
```

---

### Task 2: ITriggerCond / ITriggerAction 接口变更

**Files:**
- Modify: `HiAuRo/ACR/Interfaces/ITriggerCond.cs`
- Modify: `HiAuRo/ACR/Interfaces/ITriggerAction.cs`
- Modify: `HiAuRo/ACR/Interfaces/ITriggerBase.cs`（可选）

- [ ] **Step 1: ITriggerCond 加 Draw + Remark**

```csharp
public interface ITriggerCond : ITriggerBase
{
    /// <summary>检查触发条件是否满足</summary>
    bool Handle(ITriggerCondParams? condParams = null);

    /// <summary>用户备注</summary>
    string Remark { get; set; }

    /// <summary>编辑器控件。调用 builder 方法声明 UI 形状</summary>
    void Draw(IUiBuilder builder);
}
```

- [ ] **Step 2: ITriggerAction 加 Draw + Remark**

```csharp
public interface ITriggerAction : ITriggerBase
{
    /// <summary>执行触发动作，返回 true 表示已处理</summary>
    bool Handle();

    /// <summary>用户备注</summary>
    string Remark { get; set; }

    /// <summary>编辑器控件。调用 builder 方法声明 UI 形状</summary>
    void Draw(IUiBuilder builder);
}
```

- [ ] **Step 3: 编译验证**

```bash
DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 dotnet build HiAuRo/HiAuRo.csproj -nologo
```
预期：编译报错，因为现有的 ~38 个类型还没有实现 Draw() + Remark。

- [ ] **Step 4: 提交（先提交接口变更，后续类型改造逐步实现）**

```bash
git add HiAuRo/ACR/Interfaces/ITriggerCond.cs HiAuRo/ACR/Interfaces/ITriggerAction.cs
git commit -m "feat: ITriggerCond/ITriggerAction 接口增加 Draw(IUiBuilder) + Remark"
```

---

### Task 3: JSON 反序列化重写 — 注册内置类型 + 去掉 hardcoded switch

**Files:**
- Modify: `HiAuRo/Execution/ExecutionJson.cs`

这个任务较大，分步进行。

- [ ] **Step 1: 内置类型注册方法**

在 `ExecutionJsonLoader` 中加：

```csharp
private static bool _builtInRegistered;

/// <summary>注册所有内置触发条件/动作类型到 STJ 字典</summary>
public static void RegisterBuiltInTypes()
{
    if (_builtInRegistered) return;
    _builtInRegistered = true;

    var asm = typeof(ExecutionJsonLoader).Assembly;
    foreach (var type in asm.GetTypes())
    {
        if (type.IsAbstract) continue;
        if (typeof(ITriggerCond).IsAssignableFrom(type))
            RegisterType(type, _condTypes);
        else if (typeof(ITriggerAction).IsAssignableFrom(type))
            RegisterType(type, _actionTypes);
    }
    DService.Instance().Log.Information($"[ExecAxis] 内置类型注册: {_condTypes.Count} 条件, {_actionTypes.Count} 动作");
}

private static void RegisterType(Type type, Dictionary<string, Type> dict)
{
    dict.TryAdd(type.FullName!, type);
    dict.TryAdd(type.Name, type);
    var attr = type.GetCustomAttribute<TriggerTypeNameAttribute>();
    if (attr != null)
        dict.TryAdd(attr.TypeDiscriminator, type);
}
```

注意：用 `TryAdd` 避免 ACR 类型和内置类型冲突时覆盖。

- [ ] **Step 2: 在 ExecutionAxis.Init() 中注册**

在 `ExecutionAxis.cs` 的 `Init()` 方法里加：

```csharp
ExecutionJsonLoader.RegisterBuiltInTypes();
```

- [ ] **Step 3: 重写 ConvertKnownCondition / ConvertKnownAction**

去掉整个 `TriggerConverter.ConvertKnownCondition()` 和 `ConvertKnownAction()` 的 switch 语句。

`ConvertCondition()` 简化为：

```csharp
public static ITriggerCond? ConvertCondition(JsonElement elem)
{
    try
    {
        var typeName = elem.TryGetProperty("$type", out var tp) ? tp.GetString() ?? "" : "";
        var fullType = typeName.Split(',')[0].Trim();
        if (ExecutionJsonLoader.TryDeserializeCond(fullType, elem, out var cond))
            return cond;
        DService.Instance().Log.Warning($"[ExecAxis] 未知条件类型: {fullType}");
        return null;
    }
    catch (Exception ex)
    {
        DService.Instance().Log.Error($"[ExecAxis] 反序列化条件失败: {ex.Message}");
        return null;
    }
}
```

`ConvertAction()` 同理。

去掉 `private static uint TryGetSpellId()` 和 `private static Dictionary<string, JsonElement> ParseExtra()`（不再需要）。

- [ ] **Step 4: TryDeserializeCond/TryDeserializeAction 增加 PropertyNameCaseInsensitive**

```csharp
internal static bool TryDeserializeCond(string fullTypeName, JsonElement raw, out ITriggerCond? result)
{
    if (_condTypes.TryGetValue(fullTypeName, out var type))
    {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        result = JsonSerializer.Deserialize(raw.GetRawText(), type, opts) as ITriggerCond;
        return result != null;
    }
    result = null;
    return false;
}
```

同样的修改应用到 `TryDeserializeAction`。

- [ ] **Step 5: 重构内置适配类型（ExecutionJson.cs 底部）**

把 `TriggerCond_Variable`、`TriggerCond_AlwaysTrue`、`TriggerAction_SetVariable` 从内部类改为公开类，加上 Draw() 和 Remark：

```csharp
[TriggerDisplay("变量条件", "检查上下文变量值")]
[TriggerTypeName("TriggerCondVariable")]
public sealed class TriggerCond_Variable : ITriggerCond
{
    public string VariableName { get; set; } = "";
    public int VariableVaule { get; set; }  // 注意 AE 拼写
    public string Remark { get; set; } = "";

    public bool Handle(ITriggerCondParams? condParams = null) =>
        ExecutionAxis.Instance.Context.GetVariable(VariableName) == VariableVaule;

    public void Draw(IUiBuilder builder)
    {
        builder.AddTextInput("VariableName", VariableName);
        builder.AddIntInput("VariableVaule", VariableVaule);
    }
}
```

```csharp
[TriggerDisplay("总是真", "始终返回true（调试用）")]
[TriggerTypeName("TriggerCondAlwaysTrue")]
[CloudSync(false)]
public sealed class TriggerCond_AlwaysTrue : ITriggerCond
{
    public string Remark { get; set; } = "";
    public bool Handle(ITriggerCondParams? condParams = null) => true;
    public void Draw(IUiBuilder builder) => builder.AddLabel("始终返回 true");
}
```

```csharp
[TriggerDisplay("设置变量", "设置上下文变量值")]
[TriggerTypeName("TriggerActionAddVariable")]
public sealed class TriggerAction_SetVariable : ITriggerAction
{
    public string VariableName { get; set; } = "";
    public int SetVariableVaule { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle() { ExecutionAxis.Instance.Context.SetVariable(VariableName, SetVariableVaule); return true; }
    public void Draw(IUiBuilder builder)
    {
        builder.AddTextInput("VariableName", VariableName);
        builder.AddIntInput("SetVariableVaule", SetVariableVaule);
    }
}
```

- [ ] **Step 6: 编译验证**

```bash
DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 dotnet build HiAuRo/HiAuRo.csproj -nologo
```

预期仍会报错（其他类型还没实现接口），但 ExecutionJson.cs 本身无编译错误。

- [ ] **Step 7: 提交**

```bash
git add HiAuRo/Execution/ExecutionJson.cs HiAuRo/Execution/ExecutionAxis.cs
git commit -m "refactor: ExecutionJson 全量 STJ 反序列化，去掉 hardcoded switch"
```

---

### Task 4: TriggerMetadata 改造 — 调用 Draw() 收集 controls

**Files:**
- Modify: `HiAuRo/Execution/TriggerMetadata.cs`

- [ ] **Step 1: TriggerInfo 加 controls 字段**

```csharp
/// <summary>UI 控件列表（来自 Draw() 的声明）</summary>
public List<object>? controls { get; set; }
```

- [ ] **Step 2: TriggerCatalogBuilder 构建时调 Draw()**

在 `BuildTriggerInfo()` 方法中找到参数提取部分，添加 Draw() 调用：

```csharp
// 收集 UI 控件（通过 IUiBuilder）
List<object>? controls = null;
var instance = Activator.CreateInstance(type);
if (instance is ITriggerCond cond)
{
    var builder = new UiBuilderImpl();
    cond.Draw(builder);
    controls = builder.GetControls().Select(c => new
    {
        c.Label,
        c.Type,
        defaultValue = c.Value,
        options = c.Options  // dropdown 选项列表
    }).ToList<object>();
}
else if (instance is ITriggerAction action)
{
    var builder = new UiBuilderImpl();
    action.Draw(builder);
    controls = builder.GetControls().Select(c => new
    {
        c.Label,
        c.Type,
        defaultValue = c.Value,
        options = c.Options  // dropdown 选项列表
    }).ToList<object>();
}

var info = new TriggerInfo
{
    typeName = type.FullName ?? type.Name,
    typeDiscriminator = discriminator,
    displayName = displayAttr?.DisplayName ?? DeriveDisplayName(type),
    description = displayAttr?.Description ?? "",
    category = category,
    cloudSync = syncAttr?.Sync ?? true,
    parameters = parameters,
    controls = controls
};
```

- [ ] **Step 3: 编译验证**

```bash
DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 dotnet build HiAuRo/HiAuRo.csproj -nologo
```

- [ ] **Step 4: 提交**

```bash
git add HiAuRo/Execution/TriggerMetadata.cs
git commit -m "feat: TriggerCatalog 收录 Draw() 控件声明"
```

---

### Task 5: Cond 类型改造（简单类型，单字段）

**Files:** 以下 16 个文件

| 文件 | 原字段 | Draw() 调用 |
|------|--------|-------------|
| `TriggerCond_天气变化.cs` | `byte _weatherId` | `AddIntInput("WeatherId", WeatherId)` |
| `TriggerCond_地图特效.cs` | `uint _effectId` | `AddIntInput("EffectId", (int)EffectId)` |
| `TriggerCond_检查目标图标.cs` | `uint _iconId` | `AddIntInput("IconId", (int)IconId)` |
| `TriggerCond_技能后.cs` | `uint _spellId` | `AddIntInput("SpellId", (int)SpellId)` |
| `TriggerCond_倒计时.cs` | `int _timeLeftSec` | `AddIntInput("TimeLeftSec", TimeLeftSec)` |
| `TriggerCond_倒计时开始.cs` | `int _timeLeftSec` | `AddIntInput("TimeLeftSec", TimeLeftSec)` |
| `TriggerCond_收到技能效果.cs` | `uint _spellId` | `AddIntInput("SpellId", (int)SpellId)` |
| `TriggerCond_等待目标.cs` | `uint _dataId` | `AddIntInput("DataId", (int)DataId)` |
| `TriggerCond_Actor死亡.cs` | `uint _dataId` | `AddIntInput("DataId", (int)DataId)` |
| `TriggerCond_上次技能.cs` | `uint _spellId` | `AddIntInput("SpellId", (int)SpellId)` |
| `TriggerCond_单位可选中.cs` | `uint _dataId` | `AddIntInput("DataId", (int)DataId)` |
| `TriggerCond_单位移除.cs` | `uint _dataId` | `AddIntInput("DataId", (int)DataId)` |
| `TriggerCond_连线.cs` | `uint _tetherId` | `AddIntInput("TetherId", (int)TetherId)` |
| `TriggerCond_游戏日志.cs` | `string _messagePattern` | `AddTextInput("MessagePattern", MessagePattern)` |
| `TriggerCond_Omega循环.cs` | `uint _auraId` | `AddIntInput("AuraId", (int)AuraId)` |
| `TriggerCond_经过时间.cs` | `int _timeMs` | `AddIntInput("TimeMs", TimeMs)` |

每个类型遵循同一模板改造。以 TriggerCond_Actor死亡 为例：

```csharp
using HiAuRo.ACR;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

[TriggerDisplay("Actor死亡", "检测指定DataId的Actor死亡")]
[TriggerTypeName("TriggerCondActorDeath")]
public sealed class TriggerCond_Actor死亡 : ITriggerCond
{
    public uint DataId { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        foreach (var obj in Objects.All)
            if (obj is IBattleNPC npc && npc.DataID == DataId && npc.IsDead != true)
                return false;
        return true;
    }

    public void Draw(IUiBuilder builder)
    {
        builder.AddIntInput("DataId", (int)DataId);
    }
}
```

**关键规则：**
- `private readonly X _foo` → `public X Foo { get; set; }`
- 去掉构造函数
- `_foo` → `Foo`（Handle 里的引用）
- 加 `public string Remark { get; set; } = "";`
- 加 `void Draw(IUiBuilder builder)` 调 builder.AddXxx
- `byte` 类型的保留 `byte`（AddIntInput 接收 int，赋值时可能需强制转换：`(int)WeatherId`）

- [ ] **Step 1-16: 逐个改造 16 个简单 Cond 类型**

每个文件独立修改，按以下步骤操作：

1. 编辑文件，应用上述模板
2. 每改完 4-5 个文件就编译一次
3. 全部改完后整体提交

- [ ] **Step 17: 批量提交**

```bash
git add HiAuRo/Execution/Triggers/Cond/TriggerCond_天气变化.cs \
       HiAuRo/Execution/Triggers/Cond/TriggerCond_地图特效.cs \
       HiAuRo/Execution/Triggers/Cond/TriggerCond_检查目标图标.cs \
       ...（全部 16 个文件）
git commit -m "feat: 改造 16 个简单 Cond 类型（public属性+Draw）"
```

---

### Task 6: Cond 类型改造（复杂类型，多字段/枚举）

**Files:** 以下 9 个文件

- [ ] **Step 1-9: 逐个改造**

**TriggerCond_敌人读条.cs** — uint + uint? → 两个 int
```csharp
public uint SpellId { get; set; }
public uint EnemyDataId { get; set; }
public string Remark { get; set; } = "";
public void Draw(IUiBuilder builder)
{
    builder.AddIntInput("SpellId", (int)SpellId);
    builder.AddIntInput("EnemyDataId", (int)EnemyDataId);
}
```

**TriggerCond_技能冷却.cs** — spellId + remainingMs
```csharp
public uint SpellId { get; set; }
public int RemainingMs { get; set; }
public void Draw(IUiBuilder builder)
{
    builder.AddIntInput("SpellId", (int)SpellId);
    builder.AddIntInput("RemainingMs", RemainingMs);
}
```

**TriggerCond_检查职能.cs** — 枚举 → AddDropdown
```csharp
public JobsCategory CategoryType { get; set; }
public void Draw(IUiBuilder builder)
{
    builder.AddDropdown("CategoryType",
        Enum.GetNames<JobsCategory>(), CategoryType.ToString());
}
```

**TriggerCond_角色类型.cs** — 同检查职能

**TriggerCond_最近连线.cs** — tetherId + checkTime
```csharp
public uint TetherId { get; set; }
public float CheckTime { get; set; } = 3f;
public void Draw(IUiBuilder builder)
{
    builder.AddIntInput("TetherId", (int)TetherId);
    builder.AddFloatInput("CheckTime", CheckTime);
}
```

**TriggerCond_无目标技能效果.cs** — actionId + checkTime
**TriggerCond_收到技能效果自身.cs** — actionId + checkTime
同上模式。

- [ ] **Step 10: 提交**

```bash
git add HiAuRo/Execution/Triggers/Cond/*.cs
git commit -m "feat: 改造复杂 Cond 类型（多字段/枚举的Draw）"
```

---

### Task 7: Action 类型改造

**Files:** 以下 15 个文件

- [ ] **Step 1-15: 逐个改造**

**简单类型（无参 / 单字段）：**

| 文件 | Draw() |
|------|--------|
| `TriggerAction_重新起手.cs` | 无字段，`Draw() {}` |
| `TriggerAction_TP.cs` | 无字段，`Draw() {}` |
| `TriggerAction_滑步TP.cs` | `AddIntInput("WaitTillTime", WaitTillTime)` |
| `TriggerAction_切换自动攻击.cs` | `AddCheckbox("Enable", Enable)` |
| `TriggerAction_切换停手.cs` | `AddCheckbox("Stop", Stop)` |
| `TriggerAction_吃药.cs` | `AddIntInput("ItemId", (int)ItemId)` |
| `TriggerAction_发送按键.cs` | `AddTextInput("Command", Key)` |
| `TriggerAction_发送命令.cs` | `AddTextInput("Command", Command)` |

**复杂类型（多字段 / 枚举）：**

| 文件 | Draw() |
|------|--------|
| `TriggerAction_高优Slot.cs` | `AddIntInput("SpellId",) + AddDropdown("TargetType", ...)` |
| `TriggerAction_释放技能.cs` | 同上 |
| `TriggerAction_锁定技能.cs` | `AddIntInput("SpellId",) + AddCheckbox("Locked",)` |
| `TriggerAction_技能队列.cs` | `AddIntInput("SpellId",) + AddDropdown("TargetType", ...)` |
| `TriggerAction_设置Rotation.cs` | `AddIntInput("TargetJobId",)` |
| `TriggerAction_移动到.cs` | `AddFloatInput("X",) + AddFloatInput("Y",) + AddFloatInput("Z",)` |
| `TriggerAction_切换目标.cs` | `AddIntInput("TargetDataId",) + AddCheckbox("Nearest",)` |

SpellTargetType 枚举的 Draw() 示例：
```csharp
public SpellTargetType TargetType { get; set; } = SpellTargetType.Target;
public void Draw(IUiBuilder builder)
{
    builder.AddIntInput("SpellId", (int)SpellId);
    builder.AddDropdown("TargetType",
        Enum.GetNames<SpellTargetType>(), TargetType.ToString());
}
```

- [ ] **Step 16: 编译验证**

```bash
DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 dotnet build HiAuRo/HiAuRo.csproj -nologo
```
预期：0 errors（所有类型都实现了接口）

- [ ] **Step 17: 提交**

```bash
git add HiAuRo/Execution/Triggers/Action/*.cs
git commit -m "feat: 改造全部 Action 类型（public属性+Draw）"
```

---

### Task 8: Web 编辑器改造 — editor.js 参数编辑

**Files:**
- Modify: `HiAuRo/UI/web/editor.js`
- Modify: `HiAuRo/UI/web/editor.css`

- [ ] **Step 1: 识别现有 renderProps 中 TriggerConds / TriggerActions 的渲染区域**

找到 `renderProps()` 函数中处理 `treeCondNode` 和 `treeActionNode` 的部分。

- [ ] **Step 2: 为每个 trigger cond 添加参数控件**

对 `node.TriggerConds` 的每个条目，在列表区域添加：

```javascript
// 渲染单个 trigger cond 的参数编辑
function renderCondParams(container, cond, condIdx, nodePath) {
    var info = findCatalogEntry(cond['$type'], 'cond');
    if (!info || !info.controls) return;

    info.controls.forEach(function(ctrl) {
        var val = cond[ctrl.label] !== undefined ? cond[ctrl.label] : ctrl.defaultValue;
        var html = '';
        switch (ctrl.type) {
            case 'intInput':
                html = `<div class="trigger-field">
                    <label>${esc(ctrl.label)}</label>
                    <input type="number" step="1" data-cond="${condIdx}" data-field="${esc(ctrl.label)}" value="${val}" />
                </div>`;
                break;
            case 'floatInput':
                html = `<div class="trigger-field">
                    <label>${esc(ctrl.label)}</label>
                    <input type="number" step="any" data-cond="${condIdx}" data-field="${esc(ctrl.label)}" value="${val}" />
                </div>`;
                break;
            case 'textInput':
                html = `<div class="trigger-field">
                    <label>${esc(ctrl.label)}</label>
                    <input type="text" data-cond="${condIdx}" data-field="${esc(ctrl.label)}" value="${esc(val)}" />
                </div>`;
                break;
            case 'checkbox':
                html = `<div class="trigger-field">
                    <label><input type="checkbox" data-cond="${condIdx}" data-field="${esc(ctrl.label)}" ${val ? 'checked' : ''} /> ${esc(ctrl.label)}</label>
                </div>`;
                break;
            case 'dropdown':
                var opts = ctrl.options || [];
                html = `<div class="trigger-field">
                    <label>${esc(ctrl.label)}</label>
                    <select data-cond="${condIdx}" data-field="${esc(ctrl.label)}">
                        ${opts.map(function(o) {
                            return `<option value="${esc(o)}" ${val === o ? 'selected' : ''}>${esc(o)}</option>`;
                        }).join('')}
                    </select>
                </div>`;
                break;
        }
        container.innerHTML += html;
    });

    // 绑定输入变化事件
    container.querySelectorAll('[data-field]').forEach(function(input) {
        input.addEventListener('change', function() {
            var field = this.dataset.field;
            var ci = parseInt(this.dataset.cond);
            if (this.type === 'checkbox')
                node.TriggerConds[ci][field] = this.checked;
            else if (this.type === 'number')
                node.TriggerConds[ci][field] = parseFloat(this.value);
            else
                node.TriggerConds[ci][field] = this.value;
            markDirty();
        });
    });
}
```

- [ ] **Step 3: 在 renderProps 中调用**

在 `renderProps()` 的 `treeCondNode` 分支中，在现有条件列表区域后添加：

```javascript
// 为每个条件渲染参数控件
var condList = document.getElementById('cond-params-list');
if (condList) {
    condList.innerHTML = '';
    node.TriggerConds.forEach(function(cond, idx) {
        var div = document.createElement('div');
        div.className = 'cond-item';
        div.innerHTML = `<div class="cond-header">${esc(cond['$type'])} <button onclick="removeTriggerCond('${nodePath}', ${idx})">删除</button></div>`;
        var paramsDiv = document.createElement('div');
        renderCondParams(paramsDiv, cond, idx, nodePath);
        div.appendChild(paramsDiv);
        condList.appendChild(div);
    });
}
```

- [ ] **Step 4: 添加 catalog 查找函数**

```javascript
function findCatalogEntry($type, kind) {
    var list = (kind === 'cond') ? localTriggers.conditions : localTriggers.actions;
    for (var i = 0; i < list.length; i++) {
        if (list[i].typeDiscriminator === $type || list[i].typeName === $type)
            return list[i];
    }
    return null;
}
```

- [ ] **Step 5: editor.css 添加参数控件样式**

```css
.trigger-field {
    margin: 4px 0;
    display: flex;
    align-items: center;
    gap: 8px;
}
.trigger-field label {
    min-width: 80px;
    font-size: 12px;
    color: #aaa;
}
.trigger-field input[type="number"],
.trigger-field input[type="text"],
.trigger-field select {
    flex: 1;
    padding: 4px 8px;
    border: 1px solid #444;
    background: #2a2a2a;
    color: #ddd;
    border-radius: 4px;
}
.trigger-field input[type="checkbox"] {
    margin-right: 6px;
}
.cond-item {
    border: 1px solid #444;
    border-radius: 6px;
    padding: 8px;
    margin: 6px 0;
    background: #1e1e1e;
}
.cond-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 6px;
    font-size: 13px;
    color: #7af;
}
```

- [ ] **Step 6: 浏览器刷新验证**

在 Web 编辑器中打开，确认：
1. 选中 TreeCondNode 后，条件列表显示参数输入框
2. 输入框的值与实例 JSON 一致
3. 修改后保存 → 重新加载，值保留

---

### 自检清单

| Spec 需求 | 对应 Task |
|-----------|-----------|
| IUiBuilder 加 AddFloatInput / AddTextInput | Task 1 |
| ITriggerCond 加 Draw() + Remark | Task 2 |
| ITriggerAction 加 Draw() + Remark | Task 2 |
| 全部内置类型注册到 STJ 字典 | Task 3 |
| 去掉 ConvertKnownCondition/ConvertKnownAction switch | Task 3 |
| 内部类型（Variable/AlwaysTrue/SetVariable）改造 | Task 3 |
| TriggerMetadata 调用 Draw() 收集 controls | Task 4 |
| Simple Cond 类型改造（16 个） | Task 5 |
| Complex Cond 类型改造（9 个） | Task 6 |
| Action 类型改造（15 个） | Task 7 |
| Web editor 渲染参数控件 | Task 8 |
| editor.css 样式 | Task 8 |
