using FFXIVClientStructs.FFXIV.Client.Game;
using OmenTools.Interop.Game.Lumina;

namespace HiAuRo.ACR.HotkeyResolvers;

/// <summary>
/// 热键使用疾跑
/// </summary>
public sealed class HotkeyResolver_疾跑 : IHotkeyResolver
{
    private const uint SprintActionId = 3; // 疾跑 GeneralAction ID / Action ID

    public string Id => "Hotkey_Sprint";
    public string Label => "疾跑";
    public string DefaultKey => "";
    public uint IconId => LuminaWrapper.GetActionIconID(SprintActionId);

    public void Execute()
    {
        OmenTools.OmenService.UseActionManager.Instance().UseAction(
            ActionType.GeneralAction, SprintActionId, 0, 0, 0, 0);
    }
}
