using HiAuRo.ACR;
using HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

[TriggerDisplay("Omega循环", "检测自身是否拥有绝欧米茄麻将标识（AuraId）")]
[TriggerTypeName("TriggerCondCheckOmegaLoop")]

/// <summary>
/// 检测自身是否拥有指定 Aura（用于绝欧米茄 TOP 麻将检测）
/// AuraId: 3004=一麻, 3005=二麻, 3006=三麻, 3007=四麻, 3008=五麻, 3451=潜能量
/// </summary>
public sealed class TriggerCond_Omega循环 : ITriggerCond
{
    private readonly uint _auraId;

    public TriggerCond_Omega循环(uint auraId)
    {
        _auraId = auraId;
    }

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        var self = Data.Me.Object;
        if (self == null) return false;
        return AuraHelper.HasAura(self, _auraId);
    }
}
