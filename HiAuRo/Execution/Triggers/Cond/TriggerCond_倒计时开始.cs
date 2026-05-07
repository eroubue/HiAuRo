using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 触发条件参数 —— 倒计时开始（战斗前倒计时达到指定秒数）
/// </summary>
public sealed class TriggerCondParams_倒计时开始 : ITriggerCondParams
{
    /// <summary>倒计时剩余秒数阈值</summary>
    public int TimeLeftSec;
}

/// <summary>
/// 检测副本倒计时在战斗开始前是否达到指定剩余秒数
/// 与 TriggerCond_倒计时 不同：此条件在战斗开始前触发，不是战斗中
/// </summary>
[TriggerDisplay("倒计时开始", "检测副本战斗倒计时阶段")]
[TriggerTypeName("HiAuRo.Execution.Triggers.Cond.TriggerCond_倒计时开始, HiAuRo")]

public sealed class TriggerCond_倒计时开始 : ITriggerCond
{
    private readonly int _timeLeftSec;

    public TriggerCond_倒计时开始(int timeLeftSec)
    {
        _timeLeftSec = timeLeftSec;
    }

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        try
        {
            var pi = DService.Instance().PI;
            var countdownIpc = pi.GetIpcSubscriber<float>("Countdown.CountdownTimer");
            var remaining = countdownIpc.InvokeFunc();

            if (remaining <= 0) return false;
            return Math.Abs(remaining - _timeLeftSec) <= 0.5f;
        }
        catch
        {
            return false;
        }
    }
}
