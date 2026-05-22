using HiAuRo.ACR;
using HiAuRo.Execution.Events;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 触发条件参数 —— 敌人读条
/// </summary>
public sealed class TriggerCondParams_敌人读条 : ITriggerCondParams
{
    /// <summary>需要检测的读条技能 ID</summary>
    public uint SpellId;
    /// <summary>指定敌人 DataId（null = 任意敌人）</summary>
    public uint? EnemyDataId;
}

/// <summary>
/// 检测敌人是否在读指定技能
/// 事件驱动：匹配 ActorCastParams 快速唤醒；轮询：遍历 Objects.Enemies 检查 CastActionID
/// </summary>
[TriggerDisplay("敌人读条", "检测指定敌人是否在读指定技能")]
[TriggerTypeName("TriggerCondEnemyCastSpell")]
public sealed class TriggerCond_敌人读条 : ITriggerCond
{
    private readonly uint _spellId;
    private readonly uint? _enemyDataId;

    /// <param name="spellId">读条技能 ID</param>
    /// <param name="enemyDataId">指定敌人 DataId（null = 任意敌人）</param>
    public TriggerCond_敌人读条(uint spellId, uint? enemyDataId = null)
    {
        _spellId = spellId;
        _enemyDataId = enemyDataId;
    }

    /// <summary>检测敌人是否在读指定技能</summary>
    public bool Handle(ITriggerCondParams? condParams = null)
    {
        if (condParams is ActorCastParams cast)
        {
            if (cast.ActionID != _spellId) return false;

            if (_enemyDataId.HasValue && cast.SourceID != 0)
            {
                var obj = DService.Instance().ObjectTable?.SearchByID(cast.SourceID);
                if (obj is not IBattleNPC npc || npc.DataID != _enemyDataId.Value)
                    return false;
            }
            return true;
        }

        foreach (var enemy in Objects.Enemies)
        {
            if (enemy is not IBattleNPC battleNpc) continue;
            if (!battleNpc.IsCasting) continue;

            if (_enemyDataId.HasValue && battleNpc.DataID != _enemyDataId.Value) continue;
            if (battleNpc.CastActionID != _spellId) continue;

            return true;
        }
        return false;
    }
}
