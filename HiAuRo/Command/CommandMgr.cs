using Dalamud.Game.Command;

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
            HelpMessage = "HiAuRo 控制: /hi on|off|toggle|status|panel|reload"
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
            default:
                DService.Instance().Chat.Print("[HiAuRo] 用法: /hi on|off|toggle|status|panel|reload");
                break;
        }
    }
}
