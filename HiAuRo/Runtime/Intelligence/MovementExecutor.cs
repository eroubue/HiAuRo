using System.Numerics;
using HiAuRo.FactAxis;
using HiAuRo.Infrastructure;
using OmenTools;
using OmenTools.Interop.Game.Models;
using OmenTools.Interop.Game.Models.Packets.Downstream;

namespace HiAuRo.Runtime.Intelligence;

/// <summary>
/// 移动执行器 — 消费 ActiveDemands，通过 VNavmesh IPC 驱动角色移动。
/// MoveTo: 寻路 + deadline 调度 + TP 兜底
/// TP: 坐标瞬移（内部实现，通过 ActorSetPos 封包）
/// Hold: 停止 + 阻塞 duration 秒
/// </summary>
public sealed class MovementExecutor
{
    public static MovementExecutor Instance { get; } = new();
    private MovementExecutor() { }

    private readonly HashSet<string> _executedDemandIds = new();
    private readonly Dictionary<string, double> _startedMoveDemands = new();

    private static unsafe ActorSetPosPacket.Delegate? _actorSetPosFunc;
    private static readonly object _tpInitLock = new();
    private long _holdUntilMs;

    // 移动参数（参考 BossMod）
    private const float 基础移速 = 6.0f;
    private const float 安全缓冲 = 0.5f;

    public void Reset()
    {
        _executedDemandIds.Clear();
        _startedMoveDemands.Clear();
        _holdUntilMs = 0;
    }

    public void Update(FactState state)
    {
        var flags = PluginConfig.Instance.FactAxis;
        if (!flags.MoveTo && !flags.TP && !flags.Hold) return;
        if (!state.IsRunning) return;
        if (Environment.TickCount64 < _holdUntilMs) return;

        var demands = IntelligenceEngine.Instance.ActiveDemands;
        for (int i = demands.Count - 1; i >= 0; i--)
        {
            var demand = demands[i];
            if (_executedDemandIds.Contains(demand.Id)) continue;

            switch (demand.Type)
            {
                case DemandType.MoveTo when flags.MoveTo:
                    处理MoveTo(demand, state, flags);
                    break;
                case DemandType.TP when flags.TP:
                    处理TP(demand);
                    break;
                case DemandType.Hold when flags.Hold:
                    处理Hold(demand);
                    break;
            }
        }
    }

    private void 处理MoveTo(MovementDemand demand, FactState state, FactAxisFlags flags)
    {
        if (demand.TargetPos == null) return;
        var deadline = state.CurrentEvent?.Actions.OfType<站位需求动作>().FirstOrDefault()?.Deadline;
        if (deadline == null) return;

        var playerPos = Data.Me.Object?.Position;
        if (playerPos == null) return;

        // TP 兜底：已出发但来不及
        if (flags.MovementMode == MovementMode.NavMesh_TP兜底
            && _startedMoveDemands.TryGetValue(demand.Id, out var startedAt))
        {
            var remainingTravel = 计算移动耗时(playerPos.Value, demand.TargetPos.Value);
            if (deadline.Value - state.TotalTime < remainingTravel)
            {
                瞬移(demand.TargetPos.Value, demand.TargetHeading);
                _executedDemandIds.Add(demand.Id);
                _startedMoveDemands.Remove(demand.Id);
                return;
            }
        }

        // 检查出发时间
        var travelTime = 计算移动耗时(playerPos.Value, demand.TargetPos.Value);
        if (state.TotalTime >= deadline.Value - travelTime)
        {
            执行移动(demand, flags);
            _startedMoveDemands[demand.Id] = state.TotalTime;
        }
    }

    private void 执行移动(MovementDemand demand, FactAxisFlags flags)
    {
        if (flags.MovementMode == MovementMode.TP)
        {
            瞬移(demand.TargetPos!.Value, demand.TargetHeading);
            _executedDemandIds.Add(demand.Id);
        }
        else
        {
            try
            {
                var ipc = DService.Instance().PI.GetIpcSubscriber<Vector3, bool, bool>(
                    "vnavmesh.SimpleMove.PathfindAndMoveTo");
                ipc.InvokeFunc(demand.TargetPos!.Value, false);
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Debug($"[Movement] VNavmesh IPC 不可用: {ex.Message}");
            }
        }
    }

    private void 处理TP(MovementDemand demand)
    {
        if (demand.TargetPos == null) return;
        瞬移(demand.TargetPos.Value, demand.TargetHeading);
        _executedDemandIds.Add(demand.Id);
    }

    private void 处理Hold(MovementDemand demand)
    {
        try
        {
            DService.Instance().PI.GetIpcSubscriber<object>("vnavmesh.Path.Stop").InvokeAction();
        }
        catch { /* VNavmesh may not be installed */ }

        if (demand.Duration.HasValue && demand.Duration.Value > 0)
            _holdUntilMs = Environment.TickCount64 + (long)(demand.Duration.Value * 1000);
        _executedDemandIds.Add(demand.Id);
    }

    private float 计算移动耗时(Vector3 from, Vector3 to)
    {
        try
        {
            var ipc = DService.Instance().PI.GetIpcSubscriber<Vector3, Vector3, bool, List<Vector3>>(
                "vnavmesh.Nav.Pathfind");
            var waypoints = ipc.InvokeFunc(from, to, false);

            if (waypoints == null || waypoints.Count < 2)
                return Vector3.Distance(from, to) / 基础移速;

            float pathLength = 0;
            for (int i = 1; i < waypoints.Count; i++)
                pathLength += Vector3.Distance(waypoints[i - 1], waypoints[i]);

            return pathLength / 基础移速 + 安全缓冲;
        }
        catch
        {
            return Vector3.Distance(from, to) / 基础移速 + 安全缓冲;
        }
    }

    private static unsafe void 瞬移(Vector3 pos, float? heading)
    {
        var localPlayer = DService.Instance().ObjectTable.LocalPlayer;
        if (localPlayer == null) return;

        if (_actorSetPosFunc == null)
        {
            lock (_tpInitLock)
            {
                if (_actorSetPosFunc == null)
                {
                    _actorSetPosFunc = ActorSetPosPacket.Signature.GetDelegate<ActorSetPosPacket.Delegate>();
                }
            }
        }
        if (_actorSetPosFunc == null) return;

        try
        {
            var packet = new ActorSetPosPacket(pos);
            _actorSetPosFunc(localPlayer.EntityID, &packet);
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[Movement] TP 执行失败: {ex.Message}");
        }
    }
}
