using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Action;

/// <summary>
/// 模拟发送按键输入
/// </summary>
public sealed class TriggerAction_发送按键 : ITriggerAction
{
    private readonly string _key;

    /// <param name="key">按键名称（如 "F1", "Ctrl+1"）</param>
    public TriggerAction_发送按键(string key)
    {
        _key = key;
    }

    public bool Handle()
    {
        // 通过 HotkeyHelper 分发按键
        HotkeyHelper.HandleKeyPress(_key);
        return true;
    }
}
