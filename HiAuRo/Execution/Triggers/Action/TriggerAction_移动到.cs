using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Action;

[TriggerDisplay("移动到", "移动到指定位置（HiAuRo 设计不做自动跑位，此操作为占位）")]
[TriggerTypeName("TriggerAction_MoveTo")]

/// <summary>
/// 移动到指定坐标 —— HiAuRo 不做自动跑位，Handle() 为 no-op
/// </summary>
public sealed class TriggerAction_移动到 : ITriggerAction
{
    private readonly float _x;
    private readonly float _y;
    private readonly float _z;

    public TriggerAction_移动到(float x, float y, float z)
    {
        _x = x; _y = y; _z = z;
    }

    public bool Handle()
    {
        return true;
    }
}
