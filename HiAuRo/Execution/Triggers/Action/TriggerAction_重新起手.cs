using HiAuRo.ACR;
using HiAuRo.Runtime;

namespace HiAuRo.Execution.Triggers.Action;

[TriggerDisplay("重新起手", "重置并重新执行当前 Rotation 的起手序列")]
[TriggerTypeName("TriggerActionReplayOpener")]

/// <summary>
/// 重置起手序列状态，让起手可以重新执行
/// </summary>
public sealed class TriggerAction_重新起手 : ITriggerAction
{
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
