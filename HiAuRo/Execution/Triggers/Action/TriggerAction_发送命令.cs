using HiAuRo.ACR;
using HiAuRo.Command;
using HiAuRo.Runtime;

namespace HiAuRo.Execution.Triggers.Action;

/// <summary>
/// 发送游戏聊天命令（/echo 或 /p 等）
/// </summary>
[TriggerDisplay("发送命令", "发送聊天命令")]
[TriggerTypeName("TriggerAction_SendCommand")]
public sealed class TriggerAction_发送命令 : ITriggerAction
{
    private readonly string _command;

    /// <param name="command">聊天命令（如 "/p 注意AOE！"）</param>
    public TriggerAction_发送命令(string command)
    {
        _command = command;
    }

    /// <summary>发送聊天命令</summary>
    public bool Handle()
    {
        OmenTools.OmenService.ChatManager.Instance().SendCommand(_command);
        return true;
    }
}
