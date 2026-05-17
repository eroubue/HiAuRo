using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Action;

/// <summary>
/// 简易传送 —— HiAuRo 不做传送功能，Handle() 为 no-op
/// </summary>
[TriggerDisplay("TP", "简易传送（HiAuRo 不做传送功能，此操作为占位）")]
[TriggerTypeName("TriggerAction_SimpleTP")]
public sealed class TriggerAction_TP : ITriggerAction
{
    /// <summary>执行简易传送（当前为 no-op）</summary>
    public bool Handle()
    {
        return true;
    }
}
