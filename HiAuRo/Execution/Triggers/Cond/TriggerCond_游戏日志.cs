using HiAuRo.ACR;
using HiAuRo.Execution.Events;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 触发条件参数 —— 游戏日志
/// </summary>
public sealed class TriggerCondParams_游戏日志 : ITriggerCondParams
{
    /// <summary>日志消息匹配模式</summary>
    public string MessagePattern = string.Empty;
}

/// <summary>
/// 检测游戏聊天日志中是否出现指定消息
/// 事件驱动：匹配 ChatMessageParams；轮询：检查近期聊天消息缓冲区
/// </summary>
[TriggerDisplay("游戏日志", "检测游戏日志消息匹配")]
[TriggerTypeName("HiAuRo.Execution.Triggers.Cond.TriggerCond_游戏日志, HiAuRo")]

public sealed class TriggerCond_游戏日志 : ITriggerCond
{
    private readonly string _messagePattern;

    /// <param name="messagePattern">日志消息匹配模式</param>
    public TriggerCond_游戏日志(string messagePattern)
    {
        _messagePattern = messagePattern;
    }

    /// <summary>检测游戏日志中是否出现指定消息</summary>
    public bool Handle(ITriggerCondParams? condParams = null)
    {
        if (!string.IsNullOrEmpty(_messagePattern) &&
            condParams is ChatMessageParams chat &&
            chat.Message.Contains(_messagePattern, System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }
}
