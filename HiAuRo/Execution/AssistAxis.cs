using HiAuRo.ACR;

namespace HiAuRo.Execution;

/// <summary>
/// 辅助轴 — 独立于执行轴/事实轴的并行触发树
/// 复用 ExecutionNode AST + ScriptCompiler，从 .txt 加载
/// 对齐 AE TriggerlineAssistData
/// </summary>
public sealed class AssistAxis
{
    public static AssistAxis Instance { get; } = new();

    public TriggerCompositeNode? Root { get; private set; }
    public EvalContext Context { get; } = new();
    public ExecutionOutput CurrentOutput { get; } = new();
    public bool IsRunning { get; private set; }
    public bool Initialized { get; private set; }
    public string TimelineName { get; private set; } = "";

    internal Spell? _forceSpell;
    private bool _paused;
    private bool _autoLoadDisabled;

    /// <summary>是否允许自动加载</summary>
    public bool AutoLoadEnabled
    {
        get => !_autoLoadDisabled;
        set => _autoLoadDisabled = !value;
    }
    private uint _previousTerritoryId;
    private CancellationTokenSource? _cts;
    private readonly Dictionary<TreeCondNode, TaskCompletionSource<bool>> _waitingConds = [];

    private AssistAxis() { }

    public void Init()
    {
        if (Initialized) return;
        Initialized = true;
        AutoLoadTimeline();
    }

    public void Shutdown()
    {
        Stop();
        Root = null;
        Initialized = false;
        TimelineName = "";
        _waitingConds.Clear();
    }

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

    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;
        Context.IsDisposed = true;
        _cts?.Cancel();
        foreach (var (_, tcs) in _waitingConds)
            tcs.TrySetResult(false);
        _waitingConds.Clear();
    }

    private async Task RunTreeAsync(CancellationToken ct)
    {
        try
        {
            DService.Instance().Log.Debug($"[AssistAxis] 辅助树开始: {TimelineName}");
            await Root!.Execute(Context);
            DService.Instance().Log.Debug($"[AssistAxis] 辅助树结束: {TimelineName}");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { DService.Instance().Log.Error($"[AssistAxis] 辅助树异常: {ex}"); }
    }

    internal Task<bool> WaitCond(TreeCondNode node)
    {
        var tcs = new TaskCompletionSource<bool>();
        _waitingConds[node] = tcs;
        return tcs.Task;
    }

    private void CheckWaitingConds()
    {
        if (_waitingConds.Count == 0) return;
        var toWake = _waitingConds.Where(kv => kv.Key.EvaluateConds()).Select(kv => kv.Key).ToList();
        foreach (var node in toWake)
        {
            if (_waitingConds.TryGetValue(node, out var tcs))
            {
                tcs.TrySetResult(true);
                _waitingConds.Remove(node);
            }
        }
    }

    #region 加载 (.txt)

    public bool LoadFromJson(string json)
    {
        var data = ExecutionJsonLoader.FromJson(json);
        if (data == null) return false;
        Root = data.Root;
        TimelineName = data.Name;
        foreach (var kv in data.ExposedVars)
            Context.Variables[kv.Key] = kv.Value;
        DService.Instance().Log.Information($"[AssistAxis] 已加载: {data.Name}");
        return true;
    }

    public bool LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath)) return false;
        return LoadFromJson(File.ReadAllText(filePath));
    }

    public void AutoLoadTimeline()
    {
        if (_autoLoadDisabled) return;
        var territoryId = OmenTools.OmenService.GameState.TerritoryType;
        if (territoryId == 0) return;
        var dir = Path.Combine(DService.Instance().PI.ConfigDirectory.FullName, "AssistTimelines");
        var filePath = Path.Combine(dir, $"{territoryId}.txt");
        if (File.Exists(filePath)) LoadFromFile(filePath);
    }

    public void LoadAssistTimeline()
    {
        _autoLoadDisabled = false;
        AutoLoadTimeline();
    }

    public void UnloadAssistTimeline()
    {
        _autoLoadDisabled = true;
        Stop();
        Root = null;
        TimelineName = "";
    }

    #endregion

    #region 每帧

    public ExecutionOutput? Update(int battleTimeMs)
    {
        Context.BattleTimeMs = battleTimeMs;
        CurrentOutput.Clear();

        if (!Initialized || Root == null) return null;

        var territory = OmenTools.OmenService.GameState.TerritoryType;
        if (territory != 0 && territory != _previousTerritoryId)
        {
            _previousTerritoryId = territory;
            Stop();
            AutoLoadTimeline();
        }

        CheckWaitingConds();

        if (_forceSpell != null)
        {
            CurrentOutput.ConsumeFrame = true;
            CurrentOutput.ForceSpell = _forceSpell;
            CurrentOutput.Description = $"辅助轴强制技能: {_forceSpell.Name}";
            _forceSpell = null;
            return CurrentOutput;
        }

        if (_paused)
        {
            CurrentOutput.ConsumeFrame = true;
            CurrentOutput.PauseAcr = true;
            CurrentOutput.Description = "辅助轴暂停 ACR";
            return CurrentOutput;
        }

        return null;
    }

    #endregion

    internal void SetForceSpell(Spell spell) => _forceSpell = spell;
    internal void SetPause(bool paused) { _paused = paused; if (!paused) CurrentOutput.ResumeAcr = true; }
    public bool IsPaused => _paused;
}
