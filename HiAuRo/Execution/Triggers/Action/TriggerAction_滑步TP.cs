using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Action;

/// <summary>
/// 滑步传送 —— HiAuRo 不做传送功能，Handle() 为 no-op
/// </summary>
[TriggerDisplay("滑步TP", "滑步传送（HiAuRo 不做传送功能，此操作为占位）")]
[TriggerTypeName("TriggerAction_OnCastingTP")]
public sealed class TriggerAction_滑步TP : ITriggerAction
{
    private readonly int _waitTillTime;

    /// <param name="waitTillTime">等待时间</param>
    public TriggerAction_滑步TP(int waitTillTime = 0)
    {
        _waitTillTime = waitTillTime;
    }

    /// <summary>执行滑步传送（当前为 no-op）</summary>
    public bool Handle()
    {
        return true;
    }
}
