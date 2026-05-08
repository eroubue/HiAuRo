using System.Text.Json.Serialization;

namespace HiAuRo.FactAxis;

#region 数据模型

/// <summary>副本时间线根</summary>
public sealed class FactTimelineData
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("territoryId")]
    public uint TerritoryId { get; set; }

    [JsonPropertyName("author")]
    public string Author { get; set; } = "";

    [JsonPropertyName("phases")]
    public List<FactPhase> Phases { get; set; } = [];
}

/// <summary>
/// 一个阶段 = 时间线列表 + 切换点
/// </summary>
public sealed class FactPhase
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>阶段内事件（纯时间推进）</summary>
    [JsonPropertyName("events")]
    public List<FactEvent> Events { get; set; } = [];

    /// <summary>阶段切换点 — Sync 触发后择分支，替换后续事件列表</summary>
    [JsonPropertyName("switch")]
    public FactPhaseSwitch? Switch { get; set; }
}

/// <summary>
/// 一个事件 — 阶段时间线上的一个 Boss 行为
/// </summary>
public sealed class FactEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>阶段内预期开始秒数</summary>
    [JsonPropertyName("time")]
    public double Time { get; set; }

    /// <summary>预期持续秒数（为 0 或 null = 瞬间事件）</summary>
    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    /// <summary>开始校准 — 游戏事件匹配后记录实际开始时刻</summary>
    [JsonPropertyName("startSync")]
    public FactSyncDef? StartSync { get; set; }

    /// <summary>结束校准 — 游戏事件匹配后记录实际结束时刻</summary>
    [JsonPropertyName("endSync")]
    public FactSyncDef? EndSync { get; set; }

    [JsonPropertyName("actions")]
    public List<FactAction> Actions { get; set; } = [];

    // 运行时
    [JsonIgnore] public bool Reached { get; set; }
    [JsonIgnore] public double ActualStart { get; set; }
    [JsonIgnore] public double ActualEnd { get; set; }
    [JsonIgnore] public bool ActionsDone { get; set; }
}

/// <summary>
/// 阶段切换点 — 一个 Sync，匹配后择分支替换后续事件
/// </summary>
public sealed class FactPhaseSwitch
{
    [JsonPropertyName("sync")]
    public FactSyncDef Sync { get; set; } = new();

    [JsonPropertyName("actions")]
    public List<FactAction> Actions { get; set; } = [];

    /// <summary>分支列表 — 第一个条件满足的即为选中分支</summary>
    [JsonPropertyName("branches")]
    public List<FactSwitchBranch> Branches { get; set; } = [];
}

/// <summary>
/// 一个分支 — 满足条件后切到对应的事件列表 + 下一切换点
/// </summary>
public sealed class FactSwitchBranch
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>条件。null = 默认分支（无条件，兜底）</summary>
    [JsonPropertyName("condition")]
    public FactCondition? Condition { get; set; }

    /// <summary>分支选中后的事件列表</summary>
    [JsonPropertyName("events")]
    public List<FactEvent> Events { get; set; } = [];

    /// <summary>本分支结束时的下一切换点（null = 本分支就是阶段终点）</summary>
    [JsonPropertyName("switch")]
    public FactPhaseSwitch? Switch { get; set; }
}

#endregion

#region 同步 + 条件 + 动作

public sealed class FactSyncDef
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = ""; // "ability" | "startsUsing" | "inCombat"

    [JsonPropertyName("abilityIds")]
    public List<uint> AbilityIds { get; set; } = [];

    [JsonPropertyName("entering")]
    public bool Entering { get; set; } = true;

    public bool Match(SyncContext ctx) =>
        ctx.EventType == Type && (AbilityIds.Count == 0 || AbilityIds.Contains(ctx.AbilityId));
}

public sealed class SyncContext
{
    public string EventType { get; set; } = "";
    public uint AbilityId { get; set; }
    public bool EnteringCombat { get; set; }
}

public abstract class FactCondition
{
    public abstract bool Evaluate(Func<string, bool> lookup);
}

public sealed class VariableCondition : FactCondition
{
    [JsonPropertyName("variableName")]
    public string VariableName { get; set; } = "";
    [JsonPropertyName("expectedValue")]
    public bool ExpectedValue { get; set; } = true;
    public override bool Evaluate(Func<string, bool> lookup) => lookup(VariableName) == ExpectedValue;
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SetVariableAction), "setVariable")]
[JsonDerivedType(typeof(ToggleVariableAction), "toggleVariable")]
[JsonDerivedType(typeof(SkillSuggestionAction), "skillSuggestion")]
[JsonDerivedType(typeof(LogMessageAction), "logMessage")]
[JsonDerivedType(typeof(需求动作), "demand")]
public abstract class FactAction
{
    public abstract void Execute(FactTimeline timeline);
}

public sealed class SetVariableAction : FactAction
{
    [JsonPropertyName("variableName")]
    public string VariableName { get; set; } = "";
    [JsonPropertyName("value")]
    public bool Value { get; set; } = true;
    public override void Execute(FactTimeline timeline) => timeline.SetVariable(VariableName, Value);
}

public sealed class ToggleVariableAction : FactAction
{
    [JsonPropertyName("variableName")]
    public string VariableName { get; set; } = "";
    public override void Execute(FactTimeline timeline) => timeline.ToggleVariable(VariableName);
}

public sealed class SkillSuggestionAction : FactAction
{
    [JsonPropertyName("skillId")] public uint SkillId { get; set; }
    [JsonPropertyName("label")] public string Label { get; set; } = "";
    [JsonPropertyName("priority")] public string Priority { get; set; } = "normal";
    public override void Execute(FactTimeline timeline) { }
}

public sealed class LogMessageAction : FactAction
{
    [JsonPropertyName("message")] public string Message { get; set; } = "";
    public override void Execute(FactTimeline timeline) =>
        DService.Instance().Log.Information($"[FactAxis] {Message}");
}

/// <summary>需求动作 — 告知决策层此处需要多少减伤/治疗</summary>
public sealed class 需求动作 : FactAction
{
    [JsonPropertyName("需求减伤")]
    public int 需求减伤 { get; set; }

    [JsonPropertyName("需求治疗")]
    public int 需求治疗 { get; set; }

    public override void Execute(FactTimeline timeline) { }
}

#endregion

#region 运行时状态

public sealed class FactState
{
    public bool IsRunning { get; set; }
    public string TimelineName { get; set; } = "";
    public string PhaseName { get; set; } = "";
    public double PhaseTime { get; set; }
    public double TotalTime { get; set; }
    public FactEvent? CurrentEvent { get; set; }
    public string Status { get; set; } = "";  // "running" | "waiting_sync" | "switching"
    public double? NextEventTime { get; set; }
    public List<SkillSuggestionAction> Suggestions { get; set; } = [];
    public Dictionary<string, bool> Variables { get; set; } = [];
    public string LastSyncInfo { get; set; } = "";

    public void Clear()
    {
        IsRunning = false; PhaseName = ""; PhaseTime = 0; TotalTime = 0;
        CurrentEvent = null; Status = ""; NextEventTime = null;
        Suggestions.Clear(); Variables.Clear(); LastSyncInfo = "";
    }
}

#endregion
