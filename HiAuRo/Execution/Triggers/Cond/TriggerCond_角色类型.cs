using HiAuRo.ACR;
using HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

[TriggerDisplay("角色类型", "检测自身职业类型分类（Tank/Healer/Melee/Ranged/Caster）")]
[TriggerTypeName("TriggerCond_CheckCharacterType")]

/// <summary>
/// 检测自身职业类型分类
/// </summary>
public sealed class TriggerCond_角色类型 : ITriggerCond
{
    private readonly JobsCategory _categoryType;

    public TriggerCond_角色类型(JobsCategory categoryType)
    {
        _categoryType = categoryType;
    }

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        var jobId = Me.ClassJob;
        if (jobId == 0) return false;
        return JobsCategoryHelper.GetCategory(jobId) == _categoryType;
    }
}
