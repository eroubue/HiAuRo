using System.Text.Json;
using System.Text.Json.Serialization;
using HiAuRo.ACR;

namespace HiAuRo.Execution;

#region JSON Schema — 对齐 AE 触发树格式

public sealed class ExecutionTimelineData
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("TerritoryTypeId")]
    public uint TerritoryId { get; set; }

    [JsonPropertyName("Note")]
    public string Note { get; set; } = "";

    [JsonPropertyName("ExposedVars")]
    public List<string> ExposedVarNames { get; set; } = [];

    [JsonPropertyName("TreeRoot")]
    public TriggerNodeData? TreeRoot { get; set; }

    [JsonIgnore]
    public Dictionary<string, int> ExposedVars { get; } = [];

    [JsonIgnore]
    public TriggerCompositeNode? Root { get; set; }
}

public sealed class TriggerNodeData
{
    [JsonPropertyName("$type")]
    public string TypeName { get; set; } = "";

    [JsonPropertyName("DisplayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("Id")]
    public int Id { get; set; }

    [JsonPropertyName("Enable")]
    public bool Enable { get; set; } = true;

    [JsonPropertyName("Remark")]
    public string Remark { get; set; } = "";

    [JsonPropertyName("Tag")]
    public string Tag { get; set; } = "";

    [JsonPropertyName("Childs")]
    public List<TriggerNodeData>? Childs { get; set; }

    [JsonPropertyName("IgnoreNodeResult")]
    public bool? IgnoreNodeResult { get; set; }

    [JsonPropertyName("AnyReturn")]
    public bool? AnyReturn { get; set; }

    [JsonPropertyName("StopWhenDead")]
    public bool? StopWhenDead { get; set; }

    [JsonPropertyName("Times")]
    public int? Times { get; set; }

    [JsonPropertyName("Delay")]
    public double? Delay { get; set; }

    [JsonPropertyName("CondLogicType")]
    public CondLogicType? CondLogicType { get; set; }

    [JsonPropertyName("CheckOnce")]
    public bool? CheckOnce { get; set; }

    [JsonPropertyName("ReverseResult")]
    public bool? ReverseResult { get; set; }

    [JsonPropertyName("TriggerConds")]
    public List<JsonElement>? TriggerConds { get; set; }

    [JsonPropertyName("TriggerActions")]
    public List<JsonElement>? TriggerActions { get; set; }

    [JsonPropertyName("OnlyCheck")]
    public bool? OnlyCheck { get; set; }

    [JsonPropertyName("Script")]
    public string? Script { get; set; }

    [JsonPropertyName("Info")]
    public string? Info { get; set; }

    [JsonPropertyName("OnlyPreNode")]
    public bool? OnlyPreNode { get; set; }

    public TriggerNode ToNode()
    {
        var type = TypeName.Split(',').FirstOrDefault()?.Split('.').Last() ?? "";

        switch (type)
        {
            case "TreeRoot":
            case "TreeSequence":
                return new TreeSequence
                {
                    Id = Id, DisplayName = DisplayName, Remark = Remark, Tag = Tag, Enable = Enable,
                    IgnoreNodeResult = IgnoreNodeResult ?? (type == "TreeRoot"),
                    Childs = Childs?.Select(c => c.ToNode()).ToList() ?? []
                };
            case "TreeParallel":
                return new TreeParallel
                {
                    Id = Id, DisplayName = DisplayName, Remark = Remark, Tag = Tag, Enable = Enable,
                    AnyReturn = AnyReturn ?? false,
                    Childs = Childs?.Select(c => c.ToNode()).ToList() ?? []
                };
            case "TreeSelect":
                return new TreeSelect
                {
                    Id = Id, DisplayName = DisplayName, Remark = Remark, Tag = Tag, Enable = Enable,
                    Childs = Childs?.Select(c => c.ToNode()).ToList() ?? []
                };
            case "TreeLoop":
                return new TreeLoop
                {
                    Id = Id, DisplayName = DisplayName, Remark = Remark, Tag = Tag, Enable = Enable,
                    Times = Times ?? 1,
                    Childs = Childs?.Select(c => c.ToNode()).ToList() ?? []
                };
            case "TreeDelayNode":
                return new TreeDelayNode
                {
                    Id = Id, DisplayName = DisplayName, Remark = Remark, Tag = Tag, Enable = Enable,
                    Delay = Delay ?? 0
                };
            case "TreeCondNode":
                {
                    var conds = new List<ITriggerCond>();
                    if (TriggerConds != null)
                    {
                        foreach (var elem in TriggerConds)
                        {
                            var cond = TriggerConverter.ConvertCondition(elem);
                            if (cond != null) conds.Add(cond);
                        }
                    }
                    return new TreeCondNode
                    {
                        Id = Id, DisplayName = DisplayName, Remark = Remark, Tag = Tag, Enable = Enable,
                        CondLogicType = CondLogicType ?? Execution.CondLogicType.And,
                        CheckOnce = CheckOnce ?? false,
                        ReverseResult = ReverseResult ?? false,
                        TriggerConds = conds
                    };
                }
            case "TreeActionNode":
                {
                    var actions = new List<ITriggerAction>();
                    if (TriggerActions != null)
                    {
                        foreach (var elem in TriggerActions)
                        {
                            var action = TriggerConverter.ConvertAction(elem);
                            if (action != null) actions.Add(action);
                        }
                    }
                    return new TreeActionNode
                    {
                        Id = Id, DisplayName = DisplayName, Remark = Remark, Tag = Tag, Enable = Enable,
                        TriggerActions = actions
                    };
                }
            case "TreeScriptNode":
                return new TreeScriptNode
                {
                    Id = Id, DisplayName = DisplayName, Remark = Remark, Tag = Tag, Enable = Enable,
                    OnlyCheck = OnlyCheck ?? false,
                    Script = Script ?? ""
                };
            case "TreePrintDebugInfoNode":
                return new TreePrintDebugInfoNode
                {
                    Id = Id, DisplayName = DisplayName, Remark = Remark, Tag = Tag, Enable = Enable,
                    Info = Info ?? ""
                };
            case "TreeClearWaitNode":
                return new TreeClearWaitNode
                {
                    Id = Id, DisplayName = DisplayName, Remark = Remark, Tag = Tag, Enable = Enable,
                    OnlyPreNode = OnlyPreNode ?? true
                };
            default:
                return new TreeSequence
                {
                    Id = Id, DisplayName = $"未知: {type}", Enable = Enable,
                    Childs = Childs?.Select(c => c.ToNode()).ToList() ?? []
                };
        }
    }
}

#endregion

#region 条件/动作转换

internal static class TriggerConverter
{
    public static ITriggerCond? ConvertCondition(JsonElement elem)
    {
        try
        {
            var typeName = elem.TryGetProperty("$type", out var tp) ? tp.GetString() ?? "" : "";
            var type = typeName.Split(',').FirstOrDefault()?.Split('.').Last() ?? "";
            var fullType = typeName.Split(',')[0].Trim();
            var extra = ParseExtra(elem);

            // 先查已知映射
            var known = ConvertKnownCondition(type, extra, elem);
            if (known != null) return known;

            // 再查 ACR 注册的类型
            if (ExecutionJsonLoader.TryDeserializeCond(fullType, elem, out var custom))
                return custom;

            return null;
        }
        catch { return null; }
    }

    public static ITriggerAction? ConvertAction(JsonElement elem)
    {
        try
        {
            var typeName = elem.TryGetProperty("$type", out var tp) ? tp.GetString() ?? "" : "";
            var type = typeName.Split(',').FirstOrDefault()?.Split('.').Last() ?? "";
            var fullType = typeName.Split(',')[0].Trim();
            var extra = ParseExtra(elem);

            var known = ConvertKnownAction(type, extra, elem);
            if (known != null) return known;

            if (ExecutionJsonLoader.TryDeserializeAction(fullType, elem, out var custom))
                return custom;

            return null;
        }
        catch { return null; }
    }

    private static Dictionary<string, JsonElement> ParseExtra(JsonElement elem)
    {
        var dict = new Dictionary<string, JsonElement>();
        foreach (var prop in elem.EnumerateObject())
        {
            if (prop.Name != "$type" && prop.Name != "DisplayName" && prop.Name != "Remark")
                dict[prop.Name] = prop.Value;
        }
        return dict;
    }

    private static uint TryGetSpellId(Dictionary<string, JsonElement> extra) =>
        extra.TryGetValue("SpellId", out var e) ? e.GetUInt32() :
        extra.TryGetValue("ActionId", out var a) ? a.GetUInt32() :
        extra.TryGetValue("RegexNameOrId", out var r) && uint.TryParse(r.GetString()?.Split('|')[0], out var sid) ? sid : 0;

    internal static ITriggerCond? ConvertKnownCondition(string type, Dictionary<string, JsonElement> extra, JsonElement raw)
    {
        switch (type)
        {
            case "TriggerCondEnemyCastSpell":
                {
                    var ids = extra.TryGetValue("RegexNameOrId", out var re) ? re.GetString() ?? "0" : "0";
                    var sid = uint.TryParse(ids.Split('|')[0], out var id) ? id : 0;
                    return new Triggers.Cond.TriggerCond_敌人读条(sid);
                }
            case "TriggerCondAfterSpell":
            case "TriggerCondCheckLastSpell":
                return new Triggers.Cond.TriggerCond_技能后(TryGetSpellId(extra));
            case "TriggerCondCheckSpellCd":
                {
                    var spellId = extra.TryGetValue("SpellId", out var s) ? s.GetUInt32() : 0u;
                    var cd = extra.TryGetValue("CoolDown", out var c) ? c.GetInt32() : 0;
                    return new Triggers.Cond.TriggerCond_技能冷却(spellId, cd * 1000);
                }
            case "TriggerCondReceviceAbilityEffect":
                return new Triggers.Cond.TriggerCond_收到技能效果(TryGetSpellId(extra));
            case "TriggerCondVariable":
                {
                    var name = extra.TryGetValue("VariableName", out var vn) ? vn.GetString() ?? "" : "";
                    var val = extra.TryGetValue("VariableVaule", out var vv) ? vv.GetInt32() : 1;
                    return new TriggerCond_Variable(name, val);
                }
            case "TriggerCondOnWeatherIdChanged":
                {
                    var wid = extra.TryGetValue("WeatherId", out var w) ? w.GetByte() : (byte)0;
                    return new Triggers.Cond.TriggerCond_天气变化(wid);
                }
            case "TriggerCondMapEffect":
                return new Triggers.Cond.TriggerCond_地图特效(0);
            case "TriggerCondAfterBattleStart":
                return new Triggers.Cond.TriggerCond_经过时间(extra.TryGetValue("TimeMs", out var t) ? t.GetInt32() : 0);
            case "TriggerCondWaitTarget":
                {
                    var dataId = extra.TryGetValue("DataId", out var d) ? d.GetUInt32() : 0u;
                    return new Triggers.Cond.TriggerCond_等待目标(dataId);
                }
            case "TriggerCondCheckPartyRole":
                {
                    var role = extra.TryGetValue("PartyRole", out var p) ? p.GetString() ?? "" : "";
                    var category = role switch
                    {
                        "Tank" => JobsCategory.Tank,
                        "Healer" => JobsCategory.Healer,
                        "DPS" or "Melee" => JobsCategory.Melee,
                        "Ranged" => JobsCategory.Ranged,
                        "Caster" => JobsCategory.Caster,
                        _ => JobsCategory.Caster
                    };
                    return new Triggers.Cond.TriggerCond_检查职能(category);
                }
            case "TriggerCondActorDeath":
                {
                    var dataId = extra.TryGetValue("DataId", out var d) ? d.GetUInt32() : 0u;
                    return new Triggers.Cond.TriggerCond_Actor死亡(dataId);
                }
            case "TriggerCond_CheckCharacterType":
                {
                    var role = extra.TryGetValue("CategoryType", out var ct) ? ct.GetString() ?? "" : "";
                    var category = role switch
                    {
                        "Tank" => JobsCategory.Tank,
                        "Healer" => JobsCategory.Healer,
                        "DPS" or "Melee" => JobsCategory.Melee,
                        "Ranged" => JobsCategory.Ranged,
                        "Caster" => JobsCategory.Caster,
                        _ => JobsCategory.Caster
                    };
                    return new Triggers.Cond.TriggerCond_角色类型(category);
                }
            case "TriggerCondCheckOmegaLoop":
                {
                    var auraId = extra.TryGetValue("AuraId", out var a) ? a.GetUInt32() : 0u;
                    return new Triggers.Cond.TriggerCond_Omega循环(auraId);
                }
            case "TriggerCondCheckRecentlyTether":
                {
                    var tetherId = extra.TryGetValue("TetherId", out var ti) ? ti.GetUInt32() : 0u;
                    var checkTime = extra.TryGetValue("CheckTime", out var ct2) ? ct2.GetSingle() : 3f;
                    return new Triggers.Cond.TriggerCond_最近连线(tetherId, checkTime);
                }
            case "TriggerCondCheckAbilityEffect":
                {
                    var actionId = TryGetSpellId(extra);
                    var checkTime = extra.TryGetValue("CheckTime", out var cta) ? cta.GetSingle() : 3f;
                    return new Triggers.Cond.TriggerCond_收到技能效果自身(actionId, checkTime);
                }
            case "TriggerCondReceviceNoTargetAbilityEffect":
                {
                    var actionId = TryGetSpellId(extra);
                    var checkTime = extra.TryGetValue("CheckTime", out var ctnt) ? ctnt.GetSingle() : 3f;
                    return new Triggers.Cond.TriggerCond_无目标技能效果(actionId, checkTime);
                }
            default:
                return null;
        }
    }

    internal static ITriggerAction? ConvertKnownAction(string type, Dictionary<string, JsonElement> extra, JsonElement raw)
    {
        switch (type)
        {
            case "TriggerActionCastSpell":
                {
                    uint spellId = 0;
                    if (extra.TryGetValue("SpellConfig", out var sc))
                        spellId = sc.TryGetProperty("SpellId", out var si) ? si.GetUInt32() : 0;
                    return spellId > 0 ? new Triggers.Action.TriggerAction_释放技能(spellId) : null;
                }
            case "TriggerActionUsePotion":
                return new Triggers.Action.TriggerAction_吃药(39727);
            case "TriggerActionSelectenemy":
                return new Triggers.Action.TriggerAction_切换目标();
            case "TriggerActionHighPrioritySlot":
                {
                    uint spellId = 0;
                    if (extra.TryGetValue("SpellConfig", out var sc))
                        spellId = sc.TryGetProperty("SpellId", out var si) ? si.GetUInt32() : 0;
                    return spellId > 0 ? new Triggers.Action.TriggerAction_高优Slot(spellId) : null;
                }
            case "TriggerAction_SendCommand":
                {
                    var cmd = extra.TryGetValue("Command", out var c) ? c.GetString() ?? "" : "";
                    return new Triggers.Action.TriggerAction_发送命令(cmd);
                }
            case "TriggerAction_SendKey":
                {
                    var key = extra.TryGetValue("Command", out var k) ? k.GetString() ?? "" : "";
                    return new Triggers.Action.TriggerAction_发送按键(key);
                }
            case "TriggerActionAddVariable":
                {
                    var name = extra.TryGetValue("VariableName", out var vn) ? vn.GetString() ?? "" : "";
                    var val = extra.TryGetValue("SetVariableVaule", out var vv) ? vv.GetInt32() : 0;
                    return new TriggerAction_SetVariable(name, val);
                }
            case "TriggerActionSwitchStop":
                return new Triggers.Action.TriggerAction_切换停手(true);
            case "TriggerActionSpellQueue":
                {
                    uint spellId = 0;
                    if (extra.TryGetValue("SpellConfig", out var sc))
                        spellId = sc.TryGetProperty("SpellId", out var si) ? si.GetUInt32() : 0;
                    return spellId > 0 ? new Triggers.Action.TriggerAction_技能队列(spellId) : null;
                }
            case "TriggerActionLockSpell":
                {
                    uint spellId = 0;
                    if (extra.TryGetValue("SpellConfig", out var sc))
                        spellId = sc.TryGetProperty("SpellId", out var si) ? si.GetUInt32() : 0;
                    return spellId > 0 ? new Triggers.Action.TriggerAction_锁定技能(spellId, true) : null;
                }
            case "TriggerActionSetRotation":
                return new Triggers.Action.TriggerAction_设置Rotation(0);
            case "TriggerActionSwitchPull":
                {
                    var enable = extra.TryGetValue("Pull", out var p) ? p.GetBoolean() : true;
                    return new Triggers.Action.TriggerAction_切换自动攻击(enable);
                }
            case "TriggerActionReplayOpener":
                return new Triggers.Action.TriggerAction_重新起手();
            case "TriggerAction_MoveTo":
                {
                    var x = extra.TryGetValue("X", out var ex) ? ex.GetSingle() : 0f;
                    var y = extra.TryGetValue("Y", out var ey) ? ey.GetSingle() : 0f;
                    var z = extra.TryGetValue("Z", out var ez) ? ez.GetSingle() : 0f;
                    return new Triggers.Action.TriggerAction_移动到(x, y, z);
                }
            case "TriggerAction_SimpleTP":
                return new Triggers.Action.TriggerAction_TP();
            case "TriggerAction_OnCastingTP":
                {
                    var waitTill = extra.TryGetValue("WaitTillTime", out var wt) ? wt.GetInt32() : 0;
                    return new Triggers.Action.TriggerAction_滑步TP(waitTill);
                }
            default:
                return null;
        }
    }
}

#endregion

#region 内置适配条件/动作

[TriggerDisplay("变量条件", "检查上下文变量值")]
[TriggerTypeName("TriggerCondVariable")]

internal sealed class TriggerCond_Variable : ITriggerCond
{
    private readonly string _name;
    private readonly int _expectedValue;
    public TriggerCond_Variable(string name, int expectedValue) { _name = name; _expectedValue = expectedValue; }
    public bool Handle(ITriggerCondParams? condParams = null) =>
        ExecutionAxis.Instance.Context.GetVariable(_name) == _expectedValue;
}

[TriggerDisplay("总是真", "始终返回true（调试用）")]
[TriggerTypeName("TriggerCondAlwaysTrue")]
[CloudSync(false)]

internal sealed class TriggerCond_AlwaysTrue : ITriggerCond
{
    public bool Handle(ITriggerCondParams? condParams = null) => true;
}

[TriggerDisplay("设置变量", "设置上下文变量值")]
[TriggerTypeName("TriggerActionAddVariable")]

internal sealed class TriggerAction_SetVariable : ITriggerAction
{
    private readonly string _name;
    private readonly int _value;
    public TriggerAction_SetVariable(string name, int value) { _name = name; _value = value; }
    public bool Handle() { ExecutionAxis.Instance.Context.SetVariable(_name, _value); return true; }
}

#endregion

#region 加载器 + 类型注册

public static class ExecutionJsonLoader
{
    private static readonly Dictionary<string, Type> _condTypes = [];
    private static readonly Dictionary<string, Type> _actionTypes = [];

    /// <summary>注册自定义条件类型</summary>
    public static void RegisterConditionType(string fullTypeName, Type type)
    {
        _condTypes[fullTypeName] = type;
    }

    /// <summary>注册自定义动作类型</summary>
    public static void RegisterActionType(string fullTypeName, Type type)
    {
        _actionTypes[fullTypeName] = type;
    }

    /// <summary>从 Rotation 自动注册所有触发条件/动作（ACR 加载时调用）</summary>
    public static void RegisterFromRotation(Rotation rotation)
    {
        foreach (var cond in rotation.TriggerConditions)
        {
            var t = cond.GetType();
            _condTypes[t.FullName!] = t;
            _condTypes[t.Name] = t;
        }
        foreach (var action in rotation.TriggerActions)
        {
            var t = action.GetType();
            _actionTypes[t.FullName!] = t;
            _actionTypes[t.Name] = t;
        }
        DService.Instance().Log.Information($"[ExecAxis] ACR 注册: {_condTypes.Count} 条件, {_actionTypes.Count} 动作");
    }

    internal static bool TryDeserializeCond(string fullTypeName, JsonElement raw, out ITriggerCond? result)
    {
        if (_condTypes.TryGetValue(fullTypeName, out var type))
        {
            result = JsonSerializer.Deserialize(raw.GetRawText(), type) as ITriggerCond;
            return result != null;
        }
        result = null;
        return false;
    }

    internal static bool TryDeserializeAction(string fullTypeName, JsonElement raw, out ITriggerAction? result)
    {
        if (_actionTypes.TryGetValue(fullTypeName, out var type))
        {
            result = JsonSerializer.Deserialize(raw.GetRawText(), type) as ITriggerAction;
            return result != null;
        }
        result = null;
        return false;
    }

    public static ExecutionTimelineData? FromJson(string json)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var data = JsonSerializer.Deserialize<ExecutionTimelineData>(json, options);
            if (data == null) return null;

            if (data.TreeRoot != null)
                data.Root = (TriggerCompositeNode)data.TreeRoot.ToNode();

            foreach (var name in data.ExposedVarNames)
                data.ExposedVars[name] = 0;

            return data;
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[ExecAxis] JSON 解析失败: {ex.Message}");
            return null;
        }
    }

    public static ExecutionTimelineData? FromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            DService.Instance().Log.Error($"[ExecAxis] 文件不存在: {filePath}");
            return null;
        }
        return FromJson(File.ReadAllText(filePath));
    }
}

#endregion
