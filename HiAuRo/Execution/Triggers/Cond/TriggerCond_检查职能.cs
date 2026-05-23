using HiAuRo.ACR;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 检测自身是否属于指定队伍职能
/// </summary>
[TriggerDisplay("检查职能", "检测自身队伍职能（Tank/Healer/DPS 等）")]
[TriggerTypeName("TriggerCondCheckPartyRole")]
public sealed class TriggerCond_检查职能 : ITriggerCond
{
    private readonly JobsCategory _categoryType;

    /// <param name="categoryType">期望的职能分类</param>
    public TriggerCond_检查职能(JobsCategory categoryType)
    {
        _categoryType = categoryType;
    }

    /// <summary>检测自身是否属于指定队伍职能</summary>
    public bool Handle(ITriggerCondParams? condParams = null)
    {
        var jobId = Me.ClassJob;
        if (jobId == 0) return false;
        return JobsCategoryHelper.GetCategory(jobId) == _categoryType;
    }
}
