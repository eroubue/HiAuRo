# Trigger 序列化 + Draw() 改造设计

## 问题

1. 所有内置 ITriggerCond / ITriggerAction 使用 `private readonly` 字段，JSON 不可序列化
2. 编辑器没有参数编辑 UI，用户只能手改 JSON
3. JSON 反序列化依赖 hardcoded switch（`ConvertKnownCondition`/`ConvertKnownAction`），无法扩展

## 解决方案总览

| 层面 | 改动 |
|------|------|
| Interfaces | `ITriggerCond` / `ITriggerAction` 加 `Draw()` + `Remark` |
| 类型字段 | `private readonly` → `public` auto-property + `[JsonPropertyName]` |
| Draw() | 反射 `[JsonPropertyName]` 自动生成 HTML，特殊类型可重写 |
| JSON 反序列化 | 所有内置类型注册到 STJ 字典，去掉 hardcoded switch |
| Catalog | 增加 `draw` HTML 字段 |
| Web 编辑器 | 用 catalog 的 `draw` HTML + `data-field` 绑定渲染参数控件 |

---

## 1. 接口变更

```csharp
public interface ITriggerCond : ITriggerBase
{
    bool Handle(ITriggerCondParams? condParams = null);
    string Remark { get; set; }
    string Draw() => TriggerDrawHelper.GenerateHtml(this);  // 默认实现
}

public interface ITriggerAction : ITriggerBase
{
    bool Handle();
    string Remark { get; set; }
    string Draw() => TriggerDrawHelper.GenerateHtml(this);  // 默认实现
}
```

`Draw()` 返回 HTML 片段，默认用反射自动生成。类型可显式实现 `Draw()` 覆盖默认行为。

## 2. 类型字段改造模式

### 改前
```csharp
public sealed class TriggerCond_Actor死亡 : ITriggerCond
{
    private readonly uint _dataId;
    public TriggerCond_Actor死亡(uint dataId) { _dataId = dataId; }
    public bool Handle(ITriggerCondParams? condParams = null) { ... }
}
```

### 改后
```csharp
[TriggerDisplay("Actor死亡", "检测指定DataId的Actor死亡")]
[TriggerTypeName("TriggerCondActorDeath")]
public sealed class TriggerCond_Actor死亡 : ITriggerCond
{
    [JsonPropertyName("DataId")]
    public uint DataId { get; set; }
    
    public string Remark { get; set; } = "";
    
    public bool Handle(ITriggerCondParams? condParams = null)
    {
        foreach (var obj in Objects.All)
            if (obj is IBattleNPC npc && npc.DataID == DataId && npc.IsDead != true)
                return false;
        return true;
    }
    
    // Draw() 使用默认接口实现 → 自动生成 HTML
}
```

关键规则：
- `private readonly` 字段 → `[JsonPropertyName("X")] public T X { get; set; }`
- 去掉构造函数
- `Handle()` 中用 `DataId` 替代 `_dataId`
- （可选）`Draw()` 可重写，默认自动生成

## 3. `[JsonPropertyName]` 命名规则

以 AE JSON 字段名为准（即 hardcoded switch 中使用的名字）：

| 含义 | JsonPropertyName |
|------|-----------------|
| DataId | `"DataId"` |
| SpellId | `"SpellId"` |
| IconId / 图标ID | `"IconId"` |
| 天气ID | `"WeatherId"` |
| 效果ID | `"EffectId"` |
| 职业分类 | `"PartyRole"` 或 `"CategoryType"`（已存在的字段名） |
| 冷却时间 | `"CoolDown"`（当前 converter 用的名字） |
| 经过时间 | `"TimeMs"` |
| 延迟秒数 | `"CheckTime"` 或 `"Time"` |
| 目标类型 | `"TargetType"` |
| 是否启用 | `"Enable"` / `"Pull"` / `"Stop"`（不同 action 用不同名） |
| 变量名 | `"VariableName"` |
| 变量值 | `"VariableVaule"`（注意 AE 的拼写） |
| 坐标 | `"X"` / `"Y"` / `"Z"` |
| 按键/命令 | `"Command"` |
| 物品ID | `"ItemId"` |

**不保留 `SpellConfig` 嵌套。** Web 编辑器产生的 JSON 是扁平格式（`{ "$type": "...", "SpellId": 123 }`），C# 端直接 STJ 反序列化。

## 4. Draw() 自动生成（`TriggerDrawHelper.GenerateHtml()`）

```csharp
public static class TriggerDrawHelper
{
    public static string GenerateHtml(object trigger)
    {
        var sb = new StringBuilder();
        var type = trigger.GetType();
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        
        foreach (var prop in props)
        {
            var attr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
            if (attr == null) continue;
            
            var value = prop.GetValue(trigger);
            var jsonName = attr.Name;
            
            if (prop.PropertyType == typeof(bool))
            {
                var chk = (bool)value ? "checked" : "";
                sb.AppendLine($"<label class='field-checkbox'><input type='checkbox' data-field='{jsonName}' {chk} /> {jsonName}</label>");
            }
            else if (prop.PropertyType.IsEnum)
            {
                sb.AppendLine($"<div class='field'><label>{jsonName}</label><select data-field='{jsonName}'>");
                foreach (var enumVal in Enum.GetValues(prop.PropertyType))
                {
                    var v = Convert.ToInt32(enumVal);
                    var sel = v == Convert.ToInt32(value) ? "selected" : "";
                    sb.AppendLine($"  <option value='{v}' {sel}>{enumVal}</option>");
                }
                sb.AppendLine("</select></div>");
            }
            else if (prop.PropertyType == typeof(string))
            {
                sb.AppendLine($"<div class='field'><label>{jsonName}</label><input type='text' data-field='{jsonName}' value='{value}' /></div>");
            }
            else // 数值类型
            {
                sb.AppendLine($"<div class='field'><label>{jsonName}</label><input type='number' data-field='{jsonName}' value='{value}' step='1' /></div>");
            }
        }
        return sb.ToString();
    }
}
```

## 5. Catalog 扩展

`TriggerInfo` 新增 `draw` 字段：

```csharp
public sealed class TriggerInfo
{
    // 已有字段
    public string typeName;
    public string typeDiscriminator;
    public string displayName;
    public string description;
    public string category;
    public bool cloudSync;
    public List<ParameterInfo> parameters;
    
    // 新增
    public string draw;  // Draw() 返回的 HTML
}
```

`TriggerCatalogBuilder` 构建时调用 `Draw()` 并缓存：

```csharp
var info = new TriggerInfo { ... };
var instance = Activator.CreateInstance(type) as ITriggerBase;
if (instance is ITriggerCond cond)
    info.draw = cond.Draw();
else if (instance is ITriggerAction action)
    info.draw = action.Draw();
```

## 6. JSON 反序列化（全部 STJ）

### 注册所有内置类型

在 `ExecutionJsonLoader` 初始化时：

```csharp
public static void RegisterBuiltInTypes()
{
    // 扫描当前程序集所有 ITriggerCond / ITriggerAction
    var asm = typeof(TriggerCond_Actor死亡).Assembly;
    foreach (var type in asm.GetTypes())
    {
        if (typeof(ITriggerCond).IsAssignableFrom(type) && !type.IsAbstract)
        {
            RegisterType(type, _condTypes);
        }
        else if (typeof(ITriggerAction).IsAssignableFrom(type) && !type.IsAbstract)
        {
            RegisterType(type, _actionTypes);
        }
    }
}

private static void RegisterType(Type type, Dictionary<string, Type> dict)
{
    dict[type.FullName!] = type;
    dict[type.Name] = type;
    var attr = type.GetCustomAttribute<TriggerTypeNameAttribute>();
    if (attr != null)
        dict[attr.TypeDiscriminator] = type;
}
```

### 去掉 hardcoded switch

```csharp
// ConvertCondition 简化
public static ITriggerCond? ConvertCondition(JsonElement elem)
{
    try
    {
        var typeName = elem.TryGetProperty("$type", out var tp) ? tp.GetString() ?? "" : "";
        var fullType = typeName.Split(',')[0].Trim();
        
        if (ExecutionJsonLoader.TryDeserializeCond(fullType, elem, out var cond))
            return cond;
        
        return null;
    }
    catch { return null; }
}
```

## 7. Web 编辑器改动

### catalog 加载

editor.js 在加载 catalog 后，`localTriggers.conditions[]` 和 `localTriggers.actions[]` 的每个条目里多了一个 `draw` 字符串。

### renderProps() 改动

当选中 `treeCondNode` 时，在 `TriggerConds` 列表区域：

```
For each cond in node.TriggerConds:
  1. 根据 cond.$type 找 catalog entry
  2. 获取 entry.draw HTML
  3. 将 HTML 插入到属性面板
  4. 对每个 [data-field] 元素：
     - 从 cond 中读取对应字段值，设置到 input
     - 添加 input/change 事件监听
     - 变化时更新 cond 的对应字段
```

事件绑定示例（JS）：

```javascript
// 当 draw HTML 插入后
container.querySelectorAll('[data-field]').forEach(function(input) {
    var fieldName = input.dataset.field;
    // 从实例 JSON 读取初始值
    if (cond[fieldName] !== undefined) {
        if (input.type === 'checkbox')
            input.checked = cond[fieldName];
        else
            input.value = cond[fieldName];
    }
    // 变化时更新实例
    input.addEventListener('change', function() {
        if (input.type === 'checkbox')
            cond[fieldName] = input.checked;
        else if (input.type === 'number')
            cond[fieldName] = parseFloat(input.value);
        else
            cond[fieldName] = input.value;
    });
});
```

### addTriggerCond / addTriggerAction

添加时不再只塞默认值。从 catalog 获取参数信息，用参数名作为 JSON key：

```javascript
function addTriggerCond(nodePath, aeTypeName) {
    var info = findCatalogEntry(aeTypeName);
    var newCond = { '$type': aeTypeName };
    if (info && info.parameters) {
        info.parameters.forEach(function(p) {
            if (p.defaultValue !== undefined && p.defaultValue !== null)
                newCond[p.name] = p.defaultValue;
            else if (p.type === 'number')
                newCond[p.name] = 0;
            else if (p.type === 'text')
                newCond[p.name] = '';
            else if (p.type === 'checkbox')
                newCond[p.name] = false;
        });
    }
    node.TriggerConds.push(newCond);
}
```

## 8. 影响范围

| 文件 | 改动类型 | 数量 |
|------|---------|------|
| `ACR/Interfaces/ITriggerCond.cs` | 修改接口 | 1 |
| `ACR/Interfaces/ITriggerAction.cs` | 修改接口 | 1 |
| `Infrastructure/TriggerDrawHelper.cs` | **新建** | 1 |
| `Execution/ExecutionJson.cs` | 重写反序列化 | 1 |
| `Execution/TriggerMetadata.cs` | 增加 draw 字段 | 1 |
| `Execution/Triggers/Cond/*.cs` | 改造类型 | ~23 |
| `Execution/Triggers/Action/*.cs` | 改造类型 | ~15 |
| `UI/web/editor.js` | 渲染参数控件 | 1 |
| `UI/web/editor.css` | 参数控件样式 | 1 |

总计：~45 个文件改动，1 个新文件。

## 9. 实施顺序

```
Step 1: 新建 TriggerDrawHelper + 修改接口 ITriggerCond/ITriggerAction
Step 2: 改造 ExecutionJson（注册内置类型，去掉 switch）
Step 3: 改造 TriggerMetadata（增加 draw 字段）
Step 4-7: 分批改造 38 个 trigger 类型（先 cond，再 action）
Step 8: 改造 Web 编辑器（editor.js 渲染参数控件）
Step 9: 样式（editor.css）
```
