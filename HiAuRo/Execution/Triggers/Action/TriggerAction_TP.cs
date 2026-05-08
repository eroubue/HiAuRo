using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Action;

[TriggerDisplay("TP", "简易传送（HiAuRo 不做传送功能，此操作为占位）")]
[TriggerTypeName("TriggerAction_SimpleTP")]

/// <summary>
/// 简易传送 —— HiAuRo 不做传送功能，Handle() 为 no-op
/// </summary>
public sealed class TriggerAction_TP : ITriggerAction
{
    public bool Handle()
    {
        return true;
    }
}
