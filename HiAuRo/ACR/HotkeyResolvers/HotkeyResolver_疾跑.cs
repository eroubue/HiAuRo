using FFXIVClientStructs.FFXIV.Client.Game;

namespace HiAuRo.ACR.HotkeyResolvers;

/// <summary>
/// 热键使用疾跑
/// </summary>
public sealed class HotkeyResolver_疾跑 : IHotkeyResolver
{
    private const uint SprintActionId = 3; // 疾跑 GeneralAction ID

    public string Id => "Hotkey_Sprint";
    public string Label => "疾跑";
    public string DefaultKey => "";

    public void Execute()
    {
        OmenTools.OmenService.UseActionManager.Instance().UseAction(
            ActionType.GeneralAction, SprintActionId, 0, 0, 0, 0);
    }
}
