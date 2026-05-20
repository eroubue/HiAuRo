using OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds;

namespace HiAuRo.ACR;

/// <summary>
/// Buff / DoT 检测辅助
/// </summary>
public static class AuraHelper
{
    /// <summary>指定对象是否存在 buff</summary>
    public static bool HasAura(IGameObject? target, uint buffId)
    {
        if (target is not IBattleChara bc) return false;
        var list = bc.StatusList;
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i].StatusID == buffId) return true;
        }
        return false;
    }

    /// <summary>指定对象是否存在任意 buff</summary>
    public static bool HasAnyAura(IGameObject? target, params uint[] buffIds)
    {
        if (target is not IBattleChara bc) return false;
        var list = bc.StatusList;
        for (int i = 0; i < list.Length; i++)
        {
            var sid = list[i].StatusID;
            for (int j = 0; j < buffIds.Length; j++)
            {
                if (sid == buffIds[j]) return true;
            }
        }
        return false;
    }

    /// <summary>指定对象上 buff 剩余时间 (ms)。不存在时返回 0</summary>
    public static float GetAuraTimeLeft(IGameObject? target, uint buffId, uint sourceId = 0xE0000000)
    {
        if (target is not IBattleChara bc) return 0;
        var list = bc.StatusList;
        for (int i = 0; i < list.Length; i++)
        {
            var s = list[i];
            if (s.StatusID == buffId && (sourceId == 0xE0000000 || s.SourceID == sourceId))
                return s.RemainingTime;
        }
        return 0;
    }

    /// <summary>自身是否有 buff</summary>
    public static bool HasSelfAura(uint buffId) =>
        HasAura(Data.Me.Object, buffId);

    /// <summary>目标上是否有 buff</summary>
    public static bool HasTargetAura(uint buffId) =>
        HasAura(Data.Target.Current, buffId);
}
