using HiAuRo.ACR;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 触发条件参数 —— 目标图标
/// </summary>
public sealed class TriggerCondParams_检查目标图标 : ITriggerCondParams
{
    /// <summary>目标身上的图标 ID（NamePlateIconID）</summary>
    public uint IconId;
}

/// <summary>
/// 检测指定目标（或任意敌人）身上是否有特定图标
/// </summary>
[TriggerDisplay("检查目标图标", "检测目标是否有指定图标")]
[TriggerTypeName("HiAuRo.Execution.Triggers.Cond.TriggerCond_检查目标图标, HiAuRo")]

public sealed class TriggerCond_检查目标图标 : ITriggerCond
{
    private readonly uint _iconId;

    /// <param name="iconId">图标 ID</param>
    public TriggerCond_检查目标图标(uint iconId)
    {
        _iconId = iconId;
    }

    /// <summary>检测目标是否有指定图标</summary>
    public bool Handle(ITriggerCondParams? condParams = null)
    {
        // 先查当前目标
        var target = Data.Target.Current;
        if (target != null && target.NamePlateIconID == _iconId)
            return true;

        // 再查所有敌人
        foreach (var enemy in Objects.Enemies)
        {
            if (enemy.NamePlateIconID == _iconId)
                return true;
        }

        return false;
    }
}
