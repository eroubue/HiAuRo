using HiAuRo.Execution;
using HiAuRo.FactAxis;
using OmenTools;

namespace HiAuRo.Runtime;

public static class ModeSwitch
{
    public enum Mode { None, ExecutionAxis, FactAxis }

    public static Mode CurrentMode { get; private set; } = Mode.None;

    public static void SetMode(Mode newMode)
    {
        if (CurrentMode == newMode) return;

        switch (CurrentMode)
        {
            case Mode.ExecutionAxis:
                ExecutionAxis.Instance.Shutdown();
                break;
            case Mode.FactAxis:
                FactTimeline.Instance.Shutdown();
                break;
        }

        CurrentMode = newMode;

        switch (newMode)
        {
            case Mode.None:
                break;
            case Mode.ExecutionAxis:
                ExecutionAxis.Instance.Init();
                ExecutionAxis.Instance.AutoLoadTimeline();
                break;
            case Mode.FactAxis:
                FactTimeline.Instance.Init();
                break;
        }
    }

    public static Mode GetMode() => CurrentMode;

    public static void ToggleFactAxis()
    {
        if (CurrentMode == Mode.FactAxis)
        {
            SetMode(Mode.None);
            DService.Instance().Chat.Print("[HiAuRo] 事实轴已关闭");
        }
        else
        {
            SetMode(Mode.FactAxis);
            DService.Instance().Chat.Print("[HiAuRo] 事实轴已启用");
        }
    }

    public static void TryAutoSwitchToExecutionAxis()
    {
        if (CurrentMode != Mode.None) return;

        var territoryId = OmenTools.OmenService.GameState.TerritoryType;
        if (territoryId == 0) return;

        var path = System.IO.Path.Combine(
            DService.Instance().PI.ConfigDirectory.FullName,
            "ExecutionTimelines",
            $"{territoryId}.json");

        if (System.IO.File.Exists(path))
        {
            SetMode(Mode.ExecutionAxis);
            DService.Instance().Log.Information($"[ModeSwitch] 自动切换执行轴: territoryId={territoryId}");
        }
    }
}
