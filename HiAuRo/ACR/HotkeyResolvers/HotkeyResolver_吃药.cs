using FFXIVClientStructs.FFXIV.Client.Game;
using OmenTools.Interop.Game.Lumina;

namespace HiAuRo.ACR.HotkeyResolvers;

/// <summary>
/// 热键使用爆发药
/// </summary>
public sealed class HotkeyResolver_吃药 : IHotkeyResolver
{
    private readonly uint _itemId;

    public string Id { get; }
    public string Label { get; }
    public string DefaultKey { get; }
    public uint IconId => LuminaWrapper.GetItemIconID(_itemId);

    /// <param name="id">热键 ID</param>
    /// <param name="label">显示名称</param>
    /// <param name="itemId">物品 ID（如刚力爆发药 39727）</param>
    /// <param name="defaultKey">默认绑定键</param>
    public HotkeyResolver_吃药(string id, string label, uint itemId, string defaultKey = "")
    {
        Id = id;
        Label = label;
        _itemId = itemId;
        DefaultKey = defaultKey;
    }

    public void Execute()
    {
        OmenTools.OmenService.UseActionManager.Instance().UseAction(
            ActionType.Item, _itemId, 0, 0, 0, 0);
    }
}
