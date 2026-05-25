using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Cond;

[TriggerDisplay("倒计时开始", "检测副本战斗倒计时阶段")]
[TriggerTypeName("HiAuRo.Execution.Triggers.Cond.TriggerCond_倒计时开始, HiAuRo")]

public sealed class TriggerCond_倒计时开始 : ITriggerCond
{
    public int TimeLeftSec { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        try
        {
            var pi = DService.Instance().PI;
            var countdownIpc = pi.GetIpcSubscriber<float>("Countdown.CountdownTimer");
            var remaining = countdownIpc.InvokeFunc();

            if (remaining <= 0) return false;
            return Math.Abs(remaining - TimeLeftSec) <= 0.5f;
        }
        catch
        {
            return false;
        }
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("TimeLeftSec", TimeLeftSec);
    }
}
