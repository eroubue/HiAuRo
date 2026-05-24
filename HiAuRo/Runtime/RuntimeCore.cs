namespace HiAuRo.Runtime;

/// <summary>
/// 主 Tick 循环入口
/// </summary>
public static class RuntimeCore
{
    /// <summary>是否正在运行</summary>
    public static bool IsRunning { get; private set; }

    /// <summary>启动 Tick 循环</summary>
    public static void Start()
    {
        if (IsRunning) return;
        ACR.MainControlHelper.Unpause();
        OmenTools.OmenService.FrameworkManager.Instance().Reg(OnTick);
        IsRunning = true;
    }

    /// <summary>停止 Tick 循环</summary>
    public static void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;
    }

    /// <summary>完全关闭，注销 tick（插件 Dispose 时调用）</summary>
    public static void Shutdown()
    {
        if (IsRunning) Stop();
        OmenTools.OmenService.FrameworkManager.Instance().Unreg(OnTick);
    }

    private static void OnTick(Dalamud.Plugin.Services.IFramework _)
    {
#if DEBUG
        long _pt0 = System.Diagnostics.Stopwatch.GetTimestamp();
        PerfMonitor.BeginFrame();
#endif
        ACRLifecycle.PushImGuiState(); // 无论是否运行，每帧同步 ImGui 悬浮窗状态
#if DEBUG
        PerfMonitor.Record("ImGuiState", _pt0); _pt0 = System.Diagnostics.Stopwatch.GetTimestamp();
#endif

        if (!IsRunning) return;
        try
        {
            if (!HiAuRo.Data.IsReady)
            {
                CombatContext.Reset();
                return;
            }

            Coroutine.Instance.Update();
            CombatContext.Check();
#if DEBUG
            PerfMonitor.Record("CombatContext", _pt0); _pt0 = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            EventSystem.CheckTargetChanged();
#if DEBUG
            PerfMonitor.Record("EventSystem", _pt0); _pt0 = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            ACR.HotkeyPoller.Update();
#if DEBUG
            PerfMonitor.Record("HotkeyPoller", _pt0); _pt0 = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            ACRLifecycle.Update();
#if DEBUG
            PerfMonitor.Record("ACRLifecycle", _pt0); _pt0 = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            PluginLifecycle.Update();
#if DEBUG
            PerfMonitor.Record("PluginLifecycle", _pt0);
#endif
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[RuntimeCore] OnTick 异常: {ex}");
        }
    }
}
