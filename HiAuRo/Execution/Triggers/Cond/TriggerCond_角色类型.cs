using HiAuRo.ACR;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 检测自身职业类型分类
/// </summary>
[TriggerDisplay("角色类型", "检测自身职业类型分类（Tank/Healer/Melee/Ranged/Caster）")]
[TriggerTypeName("TriggerCond_CheckCharacterType")]
public sealed class TriggerCond_角色类型 : ITriggerCond
{
    private readonly JobsCategory _categoryType;

    /// <param name="categoryType">期望的职业分类</param>
    public TriggerCond_角色类型(JobsCategory categoryType)
    {
        _categoryType = categoryType;
    }

    /// <summary>检测自身职业类型是否匹配</summary>
    public bool Handle(ITriggerCondParams? condParams = null)
    {
        var jobId = Me.ClassJob;
        if (jobId == 0) return false;
        return JobsCategoryHelper.GetCategory(jobId) == _categoryType;
    }
}
