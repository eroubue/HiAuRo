using HiAuRo.Execution;
using HiAuRo.FactAxis;

namespace HiAuRo.Runtime;

/// <summary>
/// 模式切换骨架
/// </summary>
public static class ModeSwitch
{
    public enum Mode
    {
        /// <summary>无轴模式（MVP 默认）</summary>
        None,
        /// <summary>执行轴模式（Phase 6）</summary>
        ExecutionAxis,
        /// <summary>事实轴模式（Phase 7）</summary>
        FactAxis
    }

    public static Mode CurrentMode { get; private set; } = Mode.None;

    /// <summary>切换模式（先清理旧模式状态）</summary>
    public static void SetMode(Mode newMode)
    {
        if (CurrentMode == newMode) return;

        // 清理旧模式状态
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

        // 初始化新模式状态
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
}
