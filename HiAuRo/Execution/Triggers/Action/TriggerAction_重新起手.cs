using HiAuRo.ACR;
using HiAuRo.Runtime;

namespace HiAuRo.Execution.Triggers.Action;

/// <summary>
/// 重置起手序列状态，让起手可以重新执行
/// </summary>
[TriggerDisplay("重新起手", "重置并重新执行当前 Rotation 的起手序列")]
[TriggerTypeName("TriggerActionReplayOpener")]
public sealed class TriggerAction_重新起手 : ITriggerAction
{
    /// <summary>重置起手序列</summary>
    public bool Handle()
    {
        try
        {
            HiAuRo.Runtime.ACRLifecycle.Runner?.OpenerMgr?.Reset();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
