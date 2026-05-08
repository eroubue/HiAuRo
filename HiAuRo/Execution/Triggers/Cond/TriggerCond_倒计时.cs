using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 触发条件参数 —— 倒计时
/// </summary>
public sealed class TriggerCondParams_倒计时 : ITriggerCondParams
{
    /// <summary>倒计时剩余秒数阈值（到达此值时触发）</summary>
    public int TimeLeftSec;
}

/// <summary>
/// 检测副本战斗倒计时是否达到指定剩余秒数
/// 通过 Dalamud CountdownTimer IPC 接口读取当前倒计时
/// </summary>
[TriggerDisplay("倒计时", "检测副本战斗倒计时剩余秒数")]
[TriggerTypeName("HiAuRo.Execution.Triggers.Cond.TriggerCond_倒计时, HiAuRo")]

public sealed class TriggerCond_倒计时 : ITriggerCond
{
    private readonly int _timeLeftSec;

    public TriggerCond_倒计时(int timeLeftSec)
    {
        _timeLeftSec = timeLeftSec;
    }

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        try
        {
            // 通过 IPC 获取倒计时（秒）
            var pi = DService.Instance().PI;
            var countdownIpc = pi.GetIpcSubscriber<float>("Countdown.CountdownTimer");
            var remaining = countdownIpc.InvokeFunc();

            if (remaining <= 0) return false;

            // 约等于目标值，允许 0.5 秒误差
            return Math.Abs(remaining - _timeLeftSec) <= 0.5f;
        }
        catch
        {
            // IPC 不可用时返回 false
            return false;
        }
    }
}
