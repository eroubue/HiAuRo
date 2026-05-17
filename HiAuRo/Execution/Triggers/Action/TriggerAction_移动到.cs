using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Action;

/// <summary>
/// 移动到指定坐标 —— HiAuRo 不做自动跑位，Handle() 为 no-op
/// </summary>
[TriggerDisplay("移动到", "移动到指定位置（HiAuRo 设计不做自动跑位，此操作为占位）")]
[TriggerTypeName("TriggerAction_MoveTo")]
public sealed class TriggerAction_移动到 : ITriggerAction
{
    private readonly float _x;
    private readonly float _y;
    private readonly float _z;

    /// <param name="x">X 坐标</param>
    /// <param name="y">Y 坐标</param>
    /// <param name="z">Z 坐标</param>
    public TriggerAction_移动到(float x, float y, float z)
    {
        _x = x; _y = y; _z = z;
    }

    /// <summary>执行移动（当前为 no-op）</summary>
    public bool Handle()
    {
        return true;
    }
}
