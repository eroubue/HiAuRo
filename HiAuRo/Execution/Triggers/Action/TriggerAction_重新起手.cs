using HiAuRo.ACR;
using HiAuRo.Runtime;

namespace HiAuRo.Execution.Triggers.Action;

[TriggerDisplay("重新起手", "重置并重新执行当前 Rotation 的起手序列")]
[TriggerTypeName("TriggerActionReplayOpener")]
public sealed class TriggerAction_重新起手 : ITriggerAction
{
    public string Remark { get; set; } = "";

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

    public void Draw(ACR.IUiBuilder builder) { }
}
