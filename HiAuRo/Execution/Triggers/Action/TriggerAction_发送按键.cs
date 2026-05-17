using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Action;

/// <summary>
/// 模拟发送按键输入
/// </summary>
[TriggerDisplay("发送按键", "模拟按键操作")]
[TriggerTypeName("TriggerAction_SendKey")]
public sealed class TriggerAction_发送按键 : ITriggerAction
{
    private readonly string _key;

    /// <param name="key">按键名称（如 "F1", "Ctrl+1"）</param>
    public TriggerAction_发送按键(string key)
    {
        _key = key;
    }

    /// <summary>模拟发送按键</summary>
    public bool Handle()
    {
        // 通过 HotkeyHelper 分发按键
        HotkeyHelper.HandleKeyPress(_key);
        return true;
    }
}
