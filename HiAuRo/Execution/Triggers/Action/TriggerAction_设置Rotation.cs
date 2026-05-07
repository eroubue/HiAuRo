using HiAuRo.ACR;
using HiAuRo.Runtime;

namespace HiAuRo.Execution.Triggers.Action;

[TriggerDisplay("设置Rotation", "切换当前Rotation到指定职业")]
[TriggerTypeName("TriggerActionSetRotation")]
/// <summary>
/// 切换当前 ACR Rotation（停当前 Rotation → 启动新 Rotation）
/// </summary>
public sealed class TriggerAction_设置Rotation : ITriggerAction
{
    private readonly uint _targetJobId;

    /// <param name="targetJobId">目标职业 JobId（如 Jobs.黑魔法师 = 25）</param>
    public TriggerAction_设置Rotation(uint targetJobId)
    {
        _targetJobId = targetJobId;
    }

    public bool Handle()
    {
        // 通知 ACRLifecycle 切换职业（会触发 on/off 事件）
        // Phase 6+: 简化实现，切换由上层处理
        return false;
    }
}
