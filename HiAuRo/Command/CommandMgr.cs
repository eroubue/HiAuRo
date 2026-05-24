using Dalamud.Game.Command;
using HiAuRo.Execution;
using HiAuRo.Runtime;

namespace HiAuRo.Command;

/// <summary>
/// /hi 命令行系统
/// </summary>
public static class CommandMgr
{
    private const string MainCommand = "/hi";

    public static void Init()
    {
        DService.Instance().Command.AddHandler(MainCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = "HiAuRo: /hi on|off|toggle|status|panel|reload|fact|assist [load|unload]|debug|gallery|catalog [export|upload]"
        });
    }

    public static void Shutdown()
    {
        DService.Instance().Command.RemoveHandler(MainCommand);
    }

    private static void OnCommand(string command, string arguments)
    {
        var args = arguments.Trim().ToLower();

        switch (args)
        {
            case "on":
                Runtime.RuntimeCore.Start();
                DService.Instance().Chat.Print("[HiAuRo] 已启用");
                break;
            case "off":
                Runtime.RuntimeCore.Stop();
                DService.Instance().Chat.Print("[HiAuRo] 已禁用");
                break;
            case "toggle":
                if (Runtime.RuntimeCore.IsRunning)
                    Runtime.RuntimeCore.Stop();
                else
                    Runtime.RuntimeCore.Start();
                DService.Instance().Chat.Print($"[HiAuRo] {(Runtime.RuntimeCore.IsRunning ? "已启用" : "已禁用")}");
                break;
            case "status":
                var state = Runtime.CombatContext.CurrentState;
                var running = Runtime.RuntimeCore.IsRunning;
                DService.Instance().Chat.Print($"[HiAuRo] 状态: {(running ? "运行中" : "已停止")}, 战斗: {state}");
                break;
            case "panel":
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "http://localhost:5678/jobview.html",
                    UseShellExecute = true
                });
                break;
            case "reload":
                Runtime.ACRLifecycle.Reload();
                DService.Instance().Chat.Print("[HiAuRo] ACR 已重新扫描");
                break;
            case "fact":
                ModeSwitch.ToggleFactAxis();
                break;
            case "assist":
            case "assist load":
                AssistAxis.Instance.LoadAssistTimeline();
                DService.Instance().Chat.Print("[HiAuRo] 辅助轴已加载");
                break;
            case "assist unload":
                AssistAxis.Instance.UnloadAssistTimeline();
                DService.Instance().Chat.Print("[HiAuRo] 辅助轴已卸载");
                break;
#if DEBUG
            case "debug":
                if (ImGuiLib.DebugPerfWindow.Instance is { } w)
                {
                    w.IsOpen = !w.IsOpen;
                    DService.Instance().Chat.Print($"[HiAuRo] 性能监控窗口已{(w.IsOpen ? "打开" : "关闭")}");
                }
                break;
#endif
            case "gallery":
            case "demo":
                Plugin.Instance.ShowDemoWindow();
                DService.Instance().Chat.Print("[HiAuRo] 组件展示窗口已打开");
                break;
            case "catalog export":
                {
                    var catalogPath = Path.Combine(DService.Instance().PI.ConfigDirectory.FullName, "trigger-catalog.json");
                    if (!File.Exists(catalogPath))
                    {
                        DService.Instance().Chat.Print("[HiAuRo] 目录未生成，请先加载 ACR");
                        break;
                    }
                    var json = File.ReadAllText(catalogPath);
                    ImGui.SetClipboardText(json);
                    DService.Instance().Chat.Print($"[HiAuRo] 触发器目录已复制到剪贴板 ({json.Length} 字节)");
                }
                break;
            case "catalog upload":
                Plugin.Instance.UploadCatalogAsync().ContinueWith(
                    t => { if (t.Exception != null) DService.Instance().Log.Error($"[Command] catalog upload 失败: {t.Exception.InnerException?.Message}"); },
                    TaskContinuationOptions.OnlyOnFaulted);
                break;
            default:
                DService.Instance().Chat.Print("[HiAuRo] 用法: /hi on|off|toggle|status|panel|reload|fact|assist [load|unload]|debug|gallery|catalog [export|upload]");
                break;
        }
    }
}
