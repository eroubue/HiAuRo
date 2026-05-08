using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 触发条件参数 —— 连线（暂存，等待 Tether 读取基础设施）
/// </summary>
public sealed class TriggerCondParams_连线 : ITriggerCondParams
{
    /// <summary>连线 ID</summary>
    public uint TetherId;
}

/// <summary>
/// 检测是否有指定连线生效（暂存，等待 Tether 内存读取基础设施）
/// </summary>
[TriggerDisplay("连线", "检测是否存在指定连线")]
[TriggerTypeName("HiAuRo.Execution.Triggers.Cond.TriggerCond_连线, HiAuRo")]

public sealed class TriggerCond_连线 : ITriggerCond
{
    private readonly uint _tetherId;

    public TriggerCond_连线(uint tetherId)
    {
        _tetherId = tetherId;
    }

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        // 需要 Tether 内存读取，暂未实现
        return false;
    }
}
