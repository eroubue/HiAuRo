using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 触发条件参数 —— 游戏日志（暂存，等待 ChatLog Hook 基础设施）
/// </summary>
public sealed class TriggerCondParams_游戏日志 : ITriggerCondParams
{
    /// <summary>日志消息匹配模式</summary>
    public string MessagePattern = string.Empty;
}

/// <summary>
/// 检测游戏聊天日志中是否出现指定消息（暂存，等待 ChatLog Hook 基础设施）
/// </summary>
[TriggerDisplay("游戏日志", "检测游戏日志消息匹配")]
[TriggerTypeName("HiAuRo.Execution.Triggers.Cond.TriggerCond_游戏日志, HiAuRo")]

public sealed class TriggerCond_游戏日志 : ITriggerCond
{
    private readonly string _messagePattern;

    public TriggerCond_游戏日志(string messagePattern)
    {
        _messagePattern = messagePattern;
    }

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        // 需要 ChatLog Hook，暂未实现
        return false;
    }
}
