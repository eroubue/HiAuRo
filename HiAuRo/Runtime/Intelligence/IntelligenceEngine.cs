namespace HiAuRo.Runtime.Intelligence;

/// <summary>
/// 智能层——消费事实轴事件，释放对应的移动/站位需求
/// </summary>
public sealed class IntelligenceEngine
{
    public static IntelligenceEngine Instance { get; } = new();

    /// <summary>当前激活的需求（已释放待执行）</summary>
    public List<MovementDemand> ActiveDemands { get; } = [];

    private readonly HashSet<string> _releasedFactNodeIds = [];

    private IntelligenceEngine() { }

    /// <summary>
    /// 每帧由 AIRunner 调用——检查当前事实事件，
    /// 释放 DemandBuffer 中匹配的移动需求
    /// </summary>
    public void Update(FactAxis.FactTimeline timeline)
    {
        var state = timeline.State;
        var ev = state.CurrentEvent;
        if (ev == null) return;

        // 如果当前事件已释放过，跳过
        if (_releasedFactNodeIds.Contains(ev.Id)) return;

        // 从 DemandBuffer 按 FactNodeId 取需求
        var grouped = DemandBuffer.GetGrouped();
        if (!grouped.Contains(ev.Id)) return;

        var demands = grouped[ev.Id].ToList();
        if (demands.Count == 0) return;

        // 释放：加入 ActiveDemands，标记事件已处理
        foreach (var d in demands)
        {
            DService.Instance().Log.Information(
                $"[Intelligence] 释放移动需求: {d.Id} " +
                $"事实节点={d.FactNodeId} 类型={d.Type} " +
                $"来源={d.Source}");
            ActiveDemands.Add(d);
        }

        // 从 DemandBuffer 移除已释放的需求
        DemandBuffer.Remove(demands.Select(d => d.Id));
        _releasedFactNodeIds.Add(ev.Id);

        // 保持 _releasedFactNodeIds 不会无限增长
        if (_releasedFactNodeIds.Count > 1000)
            _releasedFactNodeIds.Clear();
    }

    /// <summary>战斗重置时清空状态</summary>
    public void Reset()
    {
        ActiveDemands.Clear();
        _releasedFactNodeIds.Clear();
    }
}
