using System.Diagnostics;
using System.Text.Json;

namespace HiAuRo.FactAxis;

/// <summary>
/// 事实轴 — 阶段内纯时间推进，Sync 校准事件开始/结束
/// </summary>
public sealed class FactTimeline
{
    public static FactTimeline Instance { get; } = new();

    public FactTimelineData? Data { get; private set; }
    public bool Initialized { get; private set; }
    public bool IsRunning { get; private set; }
    public FactState State { get; } = new();

    private readonly Dictionary<string, bool> _variables = [];
    private readonly Stopwatch _phaseClock = new();     // 本阶段经过秒数
    private readonly Stopwatch _totalClock = new();     // 战斗总秒数
    private FactPhase? _currentPhase;
    private List<FactEvent> _currentEvents = [];
    private int _eventIndex;
    private FactPhaseSwitch? _pendingSwitch;
    private FactEvent? _waitingStartSync;  // 等待开始 Sync 的事件
    private FactEvent? _waitingEndSync;    // 等待结束 Sync 的事件
    private bool _waitingSwitch;
    private uint _previousTerritoryId;

    private FactTimeline() { }

    #region 生命周期

    public void Init()
    {
        if (Initialized) return;
        Initialized = true;
        AutoLoadTimeline();
    }

    public void Start()
    {
        if (!Initialized || Data == null) return;
        Reset();
        IsRunning = true;

        // 进入第一个阶段
        if (Data.Phases.Count > 0)
            EnterPhase(Data.Phases[0]);

        _phaseClock.Restart();
        _totalClock.Restart();

        DService.Instance().Log.Information($"[FactAxis] 启动: {Data.Name}");
    }

    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;
        _phaseClock.Stop();
        _totalClock.Stop();
        DService.Instance().Log.Information($"[FactAxis] 停止: {State.PhaseName}, 阶段 {State.PhaseTime:F1}s");
    }

    public void Shutdown()
    {
        Stop();
        Reset();
        Data = null;
        Initialized = false;
    }

    private void Reset()
    {
        _phaseClock.Reset();
        _totalClock.Reset();
        _currentPhase = null;
        _currentEvents = [];
        _eventIndex = 0;
        _pendingSwitch = null;
        _waitingStartSync = null;
        _waitingEndSync = null;
        _waitingSwitch = false;
        _variables.Clear();
        State.Clear();

        if (Data != null)
        {
            foreach (var p in Data.Phases)
                ResetPhase(p);
        }
    }

    private static void ResetPhase(FactPhase phase)
    {
        foreach (var e in phase.Events) ResetEvent(e);
        if (phase.Switch != null)
        {
            foreach (var b in phase.Switch.Branches)
            {
                foreach (var e in b.Events) ResetEvent(e);
                if (b.Switch != null)
                {
                    foreach (var bb in b.Switch.Branches)
                        foreach (var e in bb.Events) ResetEvent(e);
                }
            }
        }
    }

    private static void ResetEvent(FactEvent e)
    {
        e.Reached = false;
        e.ActualStart = 0;
        e.ActualEnd = 0;
        e.ActionsDone = false;
    }

    #endregion

    #region 阶段与分支

    private void EnterPhase(FactPhase phase)
    {
        _currentPhase = phase;
        _currentEvents = phase.Events;
        _eventIndex = 0;
        _pendingSwitch = phase.Switch;
        _waitingSwitch = false;
        _phaseClock.Restart();

        DService.Instance().Log.Debug($"[FactAxis] 进入阶段: {phase.Name} ({phase.Events.Count} 事件)");
    }

    /// <summary>
    /// Sync 触发 → 评估分支 → 选中第一个满足条件的分支 → 替换事件列表 + 切换点
    /// </summary>
    private bool TrySwitchBranch()
    {
        if (_pendingSwitch == null || _pendingSwitch.Branches.Count == 0)
            return false;

        // 执行切换点动作
        foreach (var action in _pendingSwitch.Actions)
        {
            try { action.Execute(this); }
            catch (Exception ex) { DService.Instance().Log.Error($"[FactAxis] 切换动作异常: {ex}"); }
        }

        // 找第一个满足条件的分支
        FactSwitchBranch? selected = null;
        foreach (var branch in _pendingSwitch.Branches)
        {
            if (branch.Condition == null || branch.Condition.Evaluate(GetVariable))
            {
                selected = branch;
                break;
            }
        }

        if (selected == null)
        {
            DService.Instance().Log.Warning($"[FactAxis] 无匹配分支，停在当前状态");
            return false;
        }

        DService.Instance().Log.Debug($"[FactAxis] 分支切换: {selected.Name}");

        // 替换事件列表
        _currentEvents = selected.Events;
        foreach (var e in _currentEvents) ResetEvent(e);
        _eventIndex = 0;
        _pendingSwitch = selected.Switch;
        _waitingSwitch = false;
        _phaseClock.Restart();

        return true;
    }

    #endregion

    #region 变量

    public bool GetVariable(string name) => _variables.TryGetValue(name, out var v) && v;
    public void SetVariable(string name, bool value) => _variables[name] = value;
    public void ToggleVariable(string name) => _variables[name] = !GetVariable(name);

    #endregion

    #region 每帧更新

    public FactState Update(int battleTimeMs)
    {
        if (!Initialized || Data == null || !IsRunning)
        {
            State.IsRunning = false;
            return State;
        }

        // 副本切换
        var territory = OmenTools.OmenService.GameState.TerritoryType;
        if (territory != 0 && territory != _previousTerritoryId)
        {
            _previousTerritoryId = territory;
            AutoLoadTimeline();
        }

        return BuildState();
    }

    /// <summary>
    /// 推送同步事件 — 检查是否匹配当前等待的 Sync
    /// </summary>
    public void PushSyncEvent(SyncContext ctx)
    {
        if (!IsRunning) return;

        // 等待阶段切换 Sync
        if (_waitingSwitch && _pendingSwitch != null)
        {
            if (_pendingSwitch.Sync.Match(ctx))
            {
                _waitingSwitch = false;
                if (TrySwitchBranch())
                {
                    _waitingStartSync = null;
                    _waitingEndSync = null;
                }
                return;
            }
        }

        // 等待事件开始 Sync
        if (_waitingStartSync?.StartSync != null)
        {
            if (_waitingStartSync.StartSync.Match(ctx))
            {
                var ev = _waitingStartSync;
                ev.ActualStart = _totalClock.Elapsed.TotalSeconds;
                RunActions(ev);
                ev.Reached = true;
                _waitingStartSync = null;

                // 如果事件有持续时间，等结束
                if (ev.EndSync != null)
                    _waitingEndSync = ev;
                else if (ev.Duration.HasValue && ev.Duration.Value > 0)
                    _waitingEndSync = ev;
                else
                    ev.ActualEnd = ev.ActualStart; // 瞬间事件
                return;
            }
        }

        // 等待事件结束 Sync
        if (_waitingEndSync?.EndSync != null)
        {
            if (_waitingEndSync.EndSync.Match(ctx))
            {
                _waitingEndSync.ActualEnd = _totalClock.Elapsed.TotalSeconds;
                _waitingEndSync = null;
                return;
            }
        }
    }

    /// <summary>构建当前状态快照</summary>
    private FactState BuildState()
    {
        double phaseTime = _currentPhase != null ? _phaseClock.Elapsed.TotalSeconds : 0;

        State.IsRunning = true;
        State.TimelineName = Data?.Name ?? "";
        State.PhaseName = _currentPhase?.Name ?? "";
        State.PhaseTime = phaseTime;
        State.TotalTime = _totalClock.Elapsed.TotalSeconds;
        State.Variables = new Dictionary<string, bool>(_variables);

        // 先推进时间触发事件
        if (_waitingStartSync == null && _waitingEndSync == null && !_waitingSwitch)
        {
            AdvanceEvents(phaseTime);
        }

        // 当前状态描述
        if (_waitingSwitch)
            State.Status = "waiting_sync";
        else if (_waitingStartSync != null)
            State.Status = $"waiting_start: {_waitingStartSync.Name}";
        else if (_waitingEndSync != null)
            State.Status = $"waiting_end: {_waitingEndSync.Name}";
        else
            State.Status = "running";

        State.CurrentEvent = _waitingStartSync ?? _waitingEndSync;
        State.NextEventTime = GetNextEventTime(phaseTime);

        // 收集 SkillSuggestion
        State.Suggestions.Clear();
        var next = GetNextEvent(phaseTime);
        if (next != null)
        {
            foreach (var action in next.Actions)
            {
                if (action is SkillSuggestionAction sug)
                    State.Suggestions.Add(sug);
            }
        }

        return State;
    }

    /// <summary>推进到当前时间的下一个事件</summary>
    private void AdvanceEvents(double phaseTime)
    {
        while (_eventIndex < _currentEvents.Count)
        {
            var ev = _currentEvents[_eventIndex];

            if (!ev.Reached && phaseTime >= ev.Time)
            {
                // 检查是否切换点之前的事件都已处理
                if (ev.StartSync != null)
                {
                    _waitingStartSync = ev;
                    break; // 等 Sync 事件
                }
                else
                {
                    // 无 Sync，直接触发
                    ev.ActualStart = _totalClock.Elapsed.TotalSeconds;
                    RunActions(ev);
                    ev.Reached = true;

                    if (ev.Duration.HasValue && ev.Duration.Value > 0)
                    {
                        ev.ActualEnd = ev.ActualStart + ev.Duration.Value;
                    }
                    else
                    {
                        ev.ActualEnd = ev.ActualStart;
                    }
                }
            }

            _eventIndex++;
        }

        // 所有事件处理完 → 检查是否在等待切换
        if (_eventIndex >= _currentEvents.Count && _pendingSwitch != null)
        {
            _waitingSwitch = true;
        }
    }

    private void RunActions(FactEvent ev)
    {
        if (ev.ActionsDone) return;
        foreach (var action in ev.Actions)
        {
            try { action.Execute(this); }
            catch (Exception ex) { DService.Instance().Log.Error($"[FactAxis] 动作异常: {ex}"); }
        }
        ev.ActionsDone = true;
    }

    private double? GetNextEventTime(double phaseTime)
    {
        for (int i = _eventIndex; i < _currentEvents.Count; i++)
        {
            if (!_currentEvents[i].Reached && _currentEvents[i].Time > phaseTime)
                return _currentEvents[i].Time;
        }
        return null;
    }

    private FactEvent? GetNextEvent(double phaseTime)
    {
        for (int i = _eventIndex; i < _currentEvents.Count; i++)
        {
            if (!_currentEvents[i].Reached && _currentEvents[i].Time > phaseTime)
                return _currentEvents[i];
        }
        return null;
    }

    #endregion

    #region 加载

    public bool LoadFromJson(string json)
    {
        try
        {
            Data = JsonSerializer.Deserialize<FactTimelineData>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (Data == null) return false;
            DService.Instance().Log.Information($"[FactAxis] 已加载: {Data.Name} ({Data.Phases.Count} 阶段)");
            return true;
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[FactAxis] JSON 解析失败: {ex}");
            return false;
        }
    }

    public bool LoadFromFile(string path)
    {
        if (!File.Exists(path)) return false;
        return LoadFromJson(File.ReadAllText(path));
    }

    public void AutoLoadTimeline()
    {
        var tid = OmenTools.OmenService.GameState.TerritoryType;
        if (tid == 0) return;
        var dir = Path.Combine(DService.Instance().PI.ConfigDirectory.FullName, "FactTimelines");
        var path = Path.Combine(dir, $"{tid}.json");
        if (File.Exists(path)) LoadFromFile(path);
    }

    #endregion
}
