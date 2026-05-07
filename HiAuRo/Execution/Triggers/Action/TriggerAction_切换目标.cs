using HiAuRo.ACR;
using HiAuRo.Data;
using OmenTools.OmenService;

namespace HiAuRo.Execution.Triggers.Action;

[TriggerDisplay("切换目标", "切换当前目标到指定敌人")]
[TriggerTypeName("TriggerActionSelectenemy")]
/// <summary>
/// 切换目标的配置
/// </summary>
public sealed class TriggerAction_切换目标 : ITriggerAction
{
    /// <summary>指定敌人 DataId（null = 按距离选）</summary>
    private readonly uint? _targetDataId;
    /// <summary>是否选最近的目标</summary>
    private readonly bool _nearest;

    public TriggerAction_切换目标(uint? targetDataId = null, bool nearest = true)
    {
        _targetDataId = targetDataId;
        _nearest = nearest;
    }

    public bool Handle()
    {
        IBattleChara? target = null;

        if (_targetDataId.HasValue)
        {
            // 按 DataId 查找
            foreach (var enemy in Objects.Enemies)
            {
                if (enemy is IBattleNPC npc && npc.DataID == _targetDataId.Value)
                {
                    target = npc;
                    break;
                }
            }
        }
        else if (_nearest && Objects.Enemies.Count > 0)
        {
            // 选最近的可攻击敌人
            float nearestDist = float.MaxValue;
            foreach (var enemy in Objects.Enemies)
            {
                if (enemy is not IBattleChara bc) continue;
                var dist = Data.Me.DistanceToObject3D(bc);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    target = bc;
                }
            }
        }

        if (target == null) return false;

        TargetManager.Target = target;
        return true;
    }
}
