using System.Collections.Concurrent;
using HiAuRo.ACR;

namespace HiAuRo.Execution;

/// <summary>
/// 执行轴 — 异步触发树驱动，对齐 AE TriggerlineData
/// </summary>
public sealed class ExecutionAxis
{
    public static ExecutionAxis Instance { get; } = new();

    public TriggerCompositeNode? Root { get; private set; }
    public EvalContext Context { get; } = new();
    public ExecutionOutput CurrentOutput { get; } = new();
    public bool IsRunning { get; private set; }
    public bool Initialized { get; private set; }
    public string TimelineName { get; private set; } = "";
    public uint TerritoryId { get; private set; }
    public ExecutionDebug Debug { get; } = new();
    public int BattleTimeMs => Context.BattleTimeMs;

    /// <summary>自动攻击开关（由 TriggerActionSwitchPull 控制）</summary>
    public bool IsPullEnabled { get; set; } = true;

    internal Spell? _forceSpell;
    private bool _paused;
    private uint _previousTerritoryId;
    private CancellationTokenSource? _cts;

    /// <summary>WaitCond 注册表（对齐 AE ActiveActionBase2TCS）</summary>
    private readonly ConcurrentDictionary<TriggerLeafNode, TaskCompletionSource<bool>> _waitingConds = new();

    private ExecutionAxis() { }

    #region 生命周期

    public void Init()
    {
        if (Initialized) return;
        Initialized = true;
        Events.GameEventHook.Instance.OnEventFired += OnEventFired;
        AutoLoadTimeline();
    }

    public void Shutdown()
    {
        Events.GameEventHook.Instance.OnEventFired -= OnEventFired;
        Stop();
        Root = null;
        Initialized = false;
        TimelineName = "";
        TerritoryId = 0;
        _waitingConds.Clear();
    }

    private void OnEventFired(ITriggerCondParams condParams)
    {
        UseCondParams(condParams);
    }

    /// <summary>战斗开始 — 启动触发树（async void，对齐 AE）</summary>
    public void Start()
    {
        if (Root == null || IsRunning) return;

        IsRunning = true;
        Context.Reset();
        _forceSpell = null;
        _paused = false;
        _waitingConds.Clear();
        _cts = new CancellationTokenSource();

        _ = RunTreeAsync(_cts.Token);
    }

    /// <summary>脱战 — 取消触发树</summary>
    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;
        Context.IsDisposed = true;
        _cts?.Cancel();

        // 取消所有等待中的条件
        foreach (var (_, tcs) in _waitingConds)
            tcs.TrySetResult(false);
        _waitingConds.Clear();
    }

    private async Task RunTreeAsync(CancellationToken ct)
    {
        try
        {
            DService.Instance().Log.Debug($"[ExecAxis] 触发树开始: {TimelineName}");
            await Root!.Execute(Context);
            DService.Instance().Log.Debug($"[ExecAxis] 触发树结束: {TimelineName}");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[ExecAxis] 触发树异常: {ex}");
        }
    }

    #endregion

    #region WaitCond — 事件驱动的条件等待（对齐 AE）

    /// <summary>
    /// 条件节点挂起等待。注册 TCS，由 CheckWaitingConds 或 UseCondParams 唤醒。
    /// </summary>
    internal Task<bool> WaitCond(TriggerLeafNode node)
    {
        var tcs = new TaskCompletionSource<bool>();
        _waitingConds[node] = tcs;
        return tcs.Task;
    }

    /// <summary>每帧检查所有挂起条件（由 AIRunner 调用）</summary>
    private void CheckWaitingConds()
    {
        if (_waitingConds.IsEmpty) return;

        var toWake = new List<TriggerLeafNode>();
        foreach (var (node, _) in _waitingConds)
        {
            try
            {
                bool met = node switch
                {
                    TreeCondNode condNode => condNode.EvaluateConds(),
                    TreeScriptNode scriptNode => scriptNode.EvaluateConds(),
                    _ => false
                };
                if (met) toWake.Add(node);
            }
            catch { }
        }

        foreach (var node in toWake)
        {
            if (_waitingConds.TryRemove(node, out var tcs))
                tcs.TrySetResult(true);
        }
    }

    public void UseCondParams(ITriggerCondParams condParams)
    {
        if (_waitingConds.IsEmpty) return;

        var toWake = new List<TriggerLeafNode>();
        foreach (var (node, _) in _waitingConds)
        {
            try
            {
                bool met = node switch
                {
                    TreeCondNode condNode => condNode.EvaluateForEvent(condParams),
                    TreeScriptNode scriptNode => scriptNode.EvaluateForEvent(condParams),
                    _ => false
                };
                if (met) toWake.Add(node);
            }
            catch { }
        }

        foreach (var node in toWake)
        {
            if (_waitingConds.TryRemove(node, out var tcs))
                tcs.TrySetResult(true);
        }
    }

    #endregion

    #region 加载

    public bool LoadFromJson(string json)
    {
        var data = ExecutionJsonLoader.FromJson(json);
        if (data == null) return false;

        Root = data.Root;
        TimelineName = data.Name;
        TerritoryId = data.TerritoryId;

        foreach (var kv in data.ExposedVars)
            Context.Variables[kv.Key] = kv.Value;

        DService.Instance().Log.Information($"[ExecAxis] 已加载触发树: {data.Name} (副本 {data.TerritoryId})");
        return true;
    }

    public bool LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath)) return false;
        return LoadFromJson(File.ReadAllText(filePath));
    }

    public void AutoLoadTimeline()
    {
        var territoryId = OmenTools.OmenService.GameState.TerritoryType;
        if (territoryId == 0) return;
        var dir = Path.Combine(DService.Instance().PI.ConfigDirectory.FullName, "ExecutionTimelines");
        var filePath = Path.Combine(dir, $"{territoryId}.json");
        if (File.Exists(filePath)) LoadFromFile(filePath);
    }

    #endregion

    #region 每帧

    /// <summary>每帧：检查副本切换 + 唤醒挂起条件 + 输出控制信号</summary>
    public ExecutionOutput? Update(int battleTimeMs)
    {
        Context.BattleTimeMs = battleTimeMs;
        CurrentOutput.Clear();

        if (!Initialized || Root == null) return null;

        // 副本切换 → 重新加载
        var territory = OmenTools.OmenService.GameState.TerritoryType;
        if (territory != 0 && territory != _previousTerritoryId)
        {
            _previousTerritoryId = territory;
            Stop();
            AutoLoadTimeline();
        }

        // 唤醒挂起的条件节点
        CheckWaitingConds();

        // 检查强制技能
        if (_forceSpell != null)
        {
            CurrentOutput.ConsumeFrame = true;
            CurrentOutput.ForceSpell = _forceSpell;
            CurrentOutput.Description = $"执行轴强制技能: {_forceSpell.Name}";
            _forceSpell = null;
            return CurrentOutput;
        }

        // 检查暂停
        if (_paused)
        {
            CurrentOutput.ConsumeFrame = true;
            CurrentOutput.PauseAcr = true;
            CurrentOutput.Description = "执行轴暂停 ACR";
            return CurrentOutput;
        }

        return null;
    }

    #endregion

    internal void SetForceSpell(Spell spell) => _forceSpell = spell;

    internal void SetPause(bool paused)
    {
        _paused = paused;
        if (!paused) CurrentOutput.ResumeAcr = true;
    }

    public bool IsPaused => _paused;
}

public sealed class ExecutionOutput
{
    public bool ConsumeFrame { get; set; }
    public Spell? ForceSpell { get; set; }
    public IBattleChara? ForceTarget { get; set; }
    public bool PauseAcr { get; set; }
    public bool ResumeAcr { get; set; }
    public string Description { get; set; } = "";

    public void Clear()
    {
        ConsumeFrame = false;
        ForceSpell = null;
        ForceTarget = null;
        PauseAcr = false;
        ResumeAcr = false;
        Description = "";
    }
}

public sealed class ExecutionDebug
{
    public int BattleTimeMs { get; set; }
    public bool PauseAcr { get; set; }
    public List<string> TriggerHistory { get; } = [];

    public void LogTrigger(string msg)
    {
        if (TriggerHistory.Count >= 20) TriggerHistory.RemoveAt(0);
        TriggerHistory.Add(msg);
    }

    public void Reset()
    {
        BattleTimeMs = 0;
        PauseAcr = false;
        TriggerHistory.Clear();
    }
}
