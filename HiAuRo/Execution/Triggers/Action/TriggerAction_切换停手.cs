using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Action;

/// <summary>
/// 控制 ACR 停手 / 恢复
/// </summary>
public sealed class TriggerAction_切换停手 : ITriggerAction
{
    private readonly bool _stop;

    /// <param name="stop">true=停手, false=恢复</param>
    public TriggerAction_切换停手(bool stop)
    {
        _stop = stop;
    }

    public bool Handle()
    {
        ExecutionAxis.Instance.SetPause(_stop);
        return true;
    }
}
