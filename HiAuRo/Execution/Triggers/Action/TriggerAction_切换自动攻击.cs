using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Action;

[TriggerDisplay("切换自动攻击", "控制 ACR 是否主动拉怪/攻击（true=自动攻击, false=停手）")]
[TriggerTypeName("TriggerActionSwitchPull")]

/// <summary>
/// 切换自动攻击开关 —— 控制 ACR 的主动拉怪行为
/// </summary>
public sealed class TriggerAction_切换自动攻击 : ITriggerAction
{
    private readonly bool _enable;

    /// <param name="enable">true=开启自动攻击, false=关闭</param>
    public TriggerAction_切换自动攻击(bool enable)
    {
        _enable = enable;
    }

    public bool Handle()
    {
        ExecutionAxis.Instance.IsPullEnabled = _enable;
        return true;
    }
}
