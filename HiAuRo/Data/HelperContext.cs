using HiAuRo.Helper;
using OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds;

namespace HiAuRo.Data;

public sealed class HelperContext : IHelperContext
{
    public static HelperContext Instance { get; } = new();

    public bool HasStatus(uint statusId)
    {
        return Me.HasStatus(statusId, out _);
    }

    public IPlayerCharacter? GetTarget()
    {
        return Target.Current as IPlayerCharacter;
    }

    public bool HasStatusOnTarget(uint statusId)
    {
        var target = GetTarget();
        if (target is not IBattleChara bc) return false;
        return bc.StatusList.Any(s =>
            s.StatusID == statusId
            && s.SourceID == Me.Object?.EntityID
            && s.RemainingTime > 0);
    }
}
