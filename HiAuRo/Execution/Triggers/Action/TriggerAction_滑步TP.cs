using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Action;

[TriggerDisplay("滑步TP", "滑步传送（HiAuRo 不做传送功能，此操作为占位）")]
[TriggerTypeName("TriggerAction_OnCastingTP")]

/// <summary>
/// 滑步传送 —— HiAuRo 不做传送功能，Handle() 为 no-op
/// </summary>
public sealed class TriggerAction_滑步TP : ITriggerAction
{
    private readonly int _waitTillTime;

    public TriggerAction_滑步TP(int waitTillTime = 0)
    {
        _waitTillTime = waitTillTime;
    }

    public bool Handle()
    {
        return true;
    }
}
