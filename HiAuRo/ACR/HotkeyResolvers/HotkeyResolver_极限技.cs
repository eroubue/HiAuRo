using FFXIVClientStructs.FFXIV.Client.Game;
using OmenTools.Interop.Game.Lumina;

namespace HiAuRo.ACR.HotkeyResolvers;

/// <summary>
/// 热键释放极限技
/// </summary>
public sealed class HotkeyResolver_极限技 : IHotkeyResolver
{
    public string Id => "Hotkey_LB";
    public string Label => "极限技";
    public string DefaultKey => "";
    public uint IconId => LuminaWrapper.GetActionIconID(198);

    public void Execute()
    {
        // 极限技：ActionType.LimitBreak
        var self = Data.Me.Object;
        if (self == null) return;

        // 通过 Lumina 查询当前职业的 LB Action
        var job = self.ClassJob.ValueNullable;
        if (job == null) return;

        // 使用 GeneralAction 类型触发 LB
        OmenTools.OmenService.UseActionManager.Instance().UseAction(
            ActionType.GeneralAction, 4, 0, 0, 0, 0);
    }
}
