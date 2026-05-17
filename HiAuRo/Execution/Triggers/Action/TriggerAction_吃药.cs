using HiAuRo.ACR;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace HiAuRo.Execution.Triggers.Action;

/// <summary>
/// 使用消耗品（爆发药/回复药）
/// </summary>
[TriggerDisplay("吃药", "使用指定物品")]
[TriggerTypeName("TriggerActionUsePotion")]
public sealed class TriggerAction_吃药 : ITriggerAction
{
    private readonly uint _itemId;

    /// <param name="itemId">物品 ID（如爆发药 39727）</param>
    public TriggerAction_吃药(uint itemId)
    {
        _itemId = itemId;
    }

    /// <summary>使用消耗品</summary>
    public bool Handle()
    {
        OmenTools.OmenService.UseActionManager.Instance().UseAction(
            ActionType.Item, _itemId, 0, 0, 0, 0);
        return true;
    }
}
