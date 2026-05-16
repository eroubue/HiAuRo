namespace HiAuRo.Runtime.Intelligence;

/// <summary>
/// 移动执行器——消费 IntelligenceEngine 释放的 MovementDemand，
/// 通过 NavMesh/TP 外挂执行实际移动
/// </summary>
public sealed class MovementExecutor
{
    public static MovementExecutor Instance { get; } = new();

    private bool _active;

    private MovementExecutor() { }

    /// <summary>
    /// 每帧由 AIRunner 调用，消费 ActiveDemands 执行移动/传送/站位
    /// </summary>
    public void Update(FactAxis.FactState state)
    {
        if (!_active) return;

        var demands = IntelligenceEngine.Instance.ActiveDemands;
        for (int i = demands.Count - 1; i >= 0; i--)
        {
            var d = demands[i];
            switch (d.Type)
            {
                case DemandType.MoveTo when d.TargetPos.HasValue:
                    // 由外部分发插件（NavMesh/TP）负责实际移动
                    demands.RemoveAt(i);
                    break;
                case DemandType.Hold:
                    // 站位保持
                    demands.RemoveAt(i);
                    break;
                case DemandType.TP when d.TargetPos.HasValue:
                    demands.RemoveAt(i);
                    break;
            }
        }
    }

    public void Start() => _active = true;

    public void Stop()
    {
        _active = false;
        IntelligenceEngine.Instance.ActiveDemands.Clear();
    }

    public void Reset()
    {
        _active = false;
        IntelligenceEngine.Instance.ActiveDemands.Clear();
    }
}
