using HiAuRo.FactAxis;

namespace HiAuRo.Runtime.Intelligence;

/// <summary>
/// 智能层引擎——根据事实轴释放积压的移动需求
/// </summary>
public sealed class IntelligenceEngine
{
    public static IntelligenceEngine Instance { get; } = new();

    private string? _lastEventId;

    /// <summary>获取当前玩家的职责（供 TargetRole 匹配），暂空置</summary>
    private string CurrentRole
    {
        get
        {
            try
            {
                return "";
            }
            catch { return ""; }
        }
    }

    /// <summary>每帧由 AIRunner 调用</summary>
    public void Update(FactTimeline timeline)
    {
        var currentEvent = timeline.State.CurrentEvent;
        var eventId = currentEvent?.Id;
        if (eventId == null || eventId == _lastEventId) return;
        _lastEventId = eventId;

        var grouped = DemandBuffer.GetGrouped();
        if (!grouped.Contains(eventId)) return;

        var demands = grouped[eventId]
            .Where(d => string.IsNullOrEmpty(d.TargetRole) || d.TargetRole == "All" || d.TargetRole == CurrentRole)
            .OrderBy(d => d.AddedOrder);

        var released = new List<string>();
        foreach (var d in demands)
        {
            if (CanExecute(d))
            {
                Release(d);
                released.Add(d.Id);
            }
        }

        if (released.Count > 0)
            DemandBuffer.Remove(released);
    }

    /// <summary>判断当前是否可以执行（暂空置，未来加读条/机制检测）</summary>
    private static bool CanExecute(MovementDemand demand)
    {
        if (demand.Type == DemandType.TP) return true;

        // TODO: 检查玩家是否在读条中
        // TODO: 检查是否有重叠机制

        return true;
    }

    /// <summary>释放需求——通过 IPC 发给外部移动插件</summary>
    private static void Release(MovementDemand demand)
    {
        DService.Instance().Log.Debug($"[Intelligence] 释放需求: {demand.Id} type={demand.Type} node={demand.FactNodeId} role={demand.TargetRole}");
        // 暂空置：未来通过 IpcDemandService.Execute(demand);
    }
}
