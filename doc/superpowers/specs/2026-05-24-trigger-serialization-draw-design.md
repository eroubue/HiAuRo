# Trigger 序列化 + IUiBuilder Draw() 改造设计

## 问题

1. 所有内置 ITriggerCond / ITriggerAction 使用 `private readonly` 字段，JSON 不可序列化
2. Web 编辑器没有参数编辑 UI
3. JSON 反序列化依赖 hardcoded switch（`ConvertKnownCondition/ConvertKnownAction`）

## 数据流（核心架构决策）

```
Web 编辑器编辑触发条件参数 → 保存 JSON 文件
     ↓
C# 加载 JSON 文件 → STJ 反序列化 → 设置公共属性
     ↓
运行时调用 Handle() 使用已设置的属性值
```

**无实时双向通信**。Edit→Save→Load 是完整周期：

- `Draw()` 只声明"这个类型有哪些字段、什么类型"，不负责实时值绑定
- `defaultValue` 只在新建实例时使用，实际值来自 JSON 反序列化
- `IUiBuilder` 复用现有的 C#→JSON→Web 渲染管线

---

## 1. IUiBuilder 新增控件

```csharp
public interface IUiBuilder
{
    // === 已有 ===
    void AddCheckbox(string label, bool defaultValue);
    void AddSlider(string label, float min, float max, float defaultValue);
    void AddDropdown(string label, string[] options, string defaultValue);
    void AddIntInput(string label, int defaultValue, int step = 1, int stepFast = 10);
    void AddLabel(string text);
    void AddQtToggle(...);
    // ... 结构方法 (AddTab, AddGroup 等)

    // === 新增（满足 trigger 字段类型需求） ===
    void AddFloatInput(string label, float defaultValue);
    void AddTextInput(string label, string defaultValue);
}
```

| 控件 | C# 类型 | 用于 |
|------|---------|------|
| `AddCheckbox` | `bool` | 开关选项 |
| `AddIntInput` | `int` / `uint` | 技能ID、DataId、时间毫秒 |
| `AddFloatInput` | `float` / `double` | 坐标、检测时间秒数 |
| `AddTextInput` | `string` | 命令、按键、变量名 |
| `AddDropdown` | 枚举 | 目标类型、职业分类 |
| `AddSlider` | `float` | 范围值 |

## 2. 接口变更

```csharp
public interface ITriggerCond : ITriggerBase
{
    bool Handle(ITriggerCondParams? condParams = null);
    string Remark { get; set; }
    void Draw(IUiBuilder builder);  // 新增
}

public interface ITriggerAction : ITriggerBase
{
    bool Handle();
    string Remark { get; set; }
    void Draw(IUiBuilder builder);  // 新增
}
```

**不破坏现有 ACR 类型**（内部测试阶段，ACR 后续适配即可）。

## 3. 类型字段改造模式

### 改前
```csharp
public sealed class TriggerCond_Actor死亡 : ITriggerCond
{
    private readonly uint _dataId;
    public TriggerCond_Actor死亡(uint dataId) { _dataId = dataId; }
    public bool Handle(...) { ... _dataId ... }
}
```

### 改后
```csharp
[TriggerDisplay("Actor死亡", "检测指定DataId的Actor死亡")]
[TriggerTypeName("TriggerCondActorDeath")]
public sealed class TriggerCond_Actor死亡 : ITriggerCond
{
    public uint DataId { get; set; }
    public string Remark { get; set; } = "";

    public void Draw(IUiBuilder builder)
    {
        builder.AddIntInput("DataId", (int)DataId);
    }

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        foreach (var obj in Objects.All)
            if (obj is IBattleNPC npc && npc.DataID == DataId && npc.IsDead != true)
                return false;
        return true;
    }
}
```

### 改造规则

| 原模式 | 新模式 |
|--------|--------|
| `private readonly uint _dataId` | `public uint DataId { get; set; }` |
| 构造函数设置字段 | STJ 反序列化直接设置属性 |
| `_dataId` 引用 | `DataId` 引用 |
| 无 `Draw()` | `void Draw(IUiBuilder builder)` 调用 builder 方法 |
| `[JsonPropertyName]` 可选 | 如果 JSON 字段名和属性名不同，加 `[JsonPropertyName("...")]` |

## 4. UI Builder 实现（UiBuilderImpl）

新增 `AddFloatInput` 和 `AddTextInput` 的实现：

```csharp
public void AddFloatInput(string label, float defaultValue) =>
    _controls.Add(new UiControlDef(Id(label), "floatInput", _currentGroup, label, defaultValue));

public void AddTextInput(string label, string defaultValue) =>
    _controls.Add(new UiControlDef(Id(label), "textInput", _currentGroup, label, defaultValue ?? ""));
```

`UiControlDef.Value` 的类型已支持 `object?`，float 和 string 直接存入。

## 5. Catalog 扩展

`TriggerCatalogBuilder` 在扫描类型时调用 `Draw(builder)` 获取 UI 描述：

```csharp
var builder = new UiBuilderImpl();
if (instance is ITriggerCond cond) cond.Draw(builder);
var controls = builder.GetControls(); // List<UiControlDef>

// 存入 catalog
var info = new TriggerInfo
{
    ...,
    controls: controls.Select(c => new { c.Label, c.Type, c.Value }).ToList()
};
```

Catalog 新增 `controls` 字段：
```json
{
    "typeDiscriminator": "TriggerCondActorDeath",
    "displayName": "Actor死亡",
    "controls": [
        { "label": "DataId", "type": "intInput", "defaultValue": 0 }
    ]
}
```

## 6. Web 编辑器渲染

editor.js 在渲染 trigger 条件/动作列表时：

```
for each cond in node.TriggerConds:
  1. 根据 cond.$type 找到 catalog entry
  2. 获取 entry.controls[]
  3. 从 cond 实例 JSON 中读取对应字段值
  4. 渲染输入控件并填充值
  5. 用户修改时更新 cond 实例对象
  6. 保存 Node 时整体序列化为 JSON
```

渲染逻辑按 type 分配：

| controls.type | HTML 控件 |
|---------------|-----------|
| `intInput` | `<input type='number' step='1' />` |
| `floatInput` | `<input type='number' step='any' />` |
| `textInput` | `<input type='text' />` |
| `checkbox` | `<input type='checkbox' />` |
| `dropdown` | `<select>` |
| `slider` | `<input type='range' />` |

值映射：`cond[control.label]` ←→ input value。例如 `DataId` 字段 → `cond["DataId"]`。

## 7. JSON 反序列化

### 注册所有内置类型

```csharp
public static void RegisterAllTypes()
{
    var asm = typeof(TriggerCond_Actor死亡).Assembly;
    foreach (var type in asm.GetTypes().Where(t => !t.IsAbstract))
    {
        if (typeof(ITriggerCond).IsAssignableFrom(t))
            RegisterType(t, _condTypes);
        else if (typeof(ITriggerAction).IsAssignableFrom(t))
            RegisterType(t, _actionTypes);
    }
}
```

### 去掉 hardcoded switch

`ConvertCondition/ConvertAction` 统一走 STJ：

```csharp
public static ITriggerCond? ConvertCondition(JsonElement elem)
{
    var typeName = elem.TryGetProperty("$type", out var tp) ? tp.GetString() ?? "" : "";
    var fullType = typeName.Split(',')[0].Trim();
    return ExecutionJsonLoader.TryDeserializeCond(fullType, elem, out var cond) ? cond : null;
}
```

## 8. 改动清单

| 文件 | 改动 |
|------|------|
| `ACR/Interfaces/IUiBuilder.cs` | 加 `AddFloatInput`, `AddTextInput` |
| `ACR/Interfaces/ITriggerCond.cs` | 加 `Draw(IUiBuilder)`, `Remark` |
| `ACR/Interfaces/ITriggerAction.cs` | 加 `Draw(IUiBuilder)`, `Remark` |
| `UI/UiBuilderImpl.cs` | 实现新控件方法 |
| `UI/web/app.js` | 渲染 floatInput, textInput 控件 |
| `Execution/TriggerMetadata.cs` | 构建时调 Draw() 存 controls |
| `Execution/ExecutionJson.cs` | 注内置类型，删 hardcoded switch |
| `Execution/Triggers/Cond/*.cs` (×23) | 改造为 public 属性 + Draw() |
| `Execution/Triggers/Action/*.cs` (×15) | 改造为 public 属性 + Draw() |
| `UI/web/editor.js` | 用 catalog controls 渲染参数编辑 |
| `UI/web/editor.css` | 参数控件样式 |

## 9. 实施顺序

```
Step 1: IUiBuilder 加 AddFloatInput / AddTextInput + UiBuilderImpl 实现
Step 2: Web 端 app.js 渲染新控件类型
Step 3: 改造 ITriggerCond / ITriggerAction 接口（加 Draw + Remark）
Step 4: 逐批改造 Trigger Cond 类型（约 23 个）
Step 5: 逐批改造 Trigger Action 类型（约 15 个）
Step 6: 重写 ExecutionJson 反序列化（注册内置类型，删 switch）
Step 7: TriggerMetadata 构建时调 Draw() + catalog 存 controls
Step 8: Web 编辑器 editor.js 用 catalog controls 渲染参数编辑
Step 9: editor.css 样式
```
