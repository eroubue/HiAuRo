using HiAuRo.ACR;
using HiAuRo.Data;

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
/// </summary>
public sealed class TriggerCond_敌人读条 : ITriggerCond
{
    private readonly uint _spellId;
    private readonly uint? _enemyDataId;

    public TriggerCond_敌人读条(uint spellId, uint? enemyDataId = null)
    {
        _spellId = spellId;
        _enemyDataId = enemyDataId;
    }

    public bool Handle(ITriggerCondParams? condParams = null)
    {
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
