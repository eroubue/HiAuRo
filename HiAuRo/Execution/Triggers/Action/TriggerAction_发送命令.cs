using HiAuRo.ACR;
using HiAuRo.Command;
using HiAuRo.Runtime;

namespace HiAuRo.Execution.Triggers.Action;

/// <summary>
/// 发送游戏聊天命令（/echo 或 /p 等）
/// </summary>
public sealed class TriggerAction_发送命令 : ITriggerAction
{
    private readonly string _command;

    /// <param name="command">聊天命令（如 "/p 注意AOE！"）</param>
    public TriggerAction_发送命令(string command)
    {
        _command = command;
    }

    public bool Handle()
    {
        // 使用 Dalamud Chat 发送消息
        DService.Instance().Chat.Print(_command);
        return true;
    }
}
