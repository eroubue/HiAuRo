using HiAuRo.Runtime;

namespace HiAuRo.ACR.HotkeyResolvers;

/// <summary>
/// QT 热键解析器 —— 爆发药
/// </summary>
public sealed class HotkeyResolver_Potion : IHotkeyResolver
{
    public string Id => "potion";
    public string Label => "爆发药";
    public string DefaultKey => string.Empty;

    public int Check()
    {
        // 检查是否有可用的爆发药
        var potionId = GetPotionId();
        if (potionId == 0) return -1;
        if (!SpellHelper.CanUseSpell(potionId)) return -1;
        return 0;
    }

    public void Execute()
    {
        var potionId = GetPotionId();
        if (potionId == 0) return;

        var slot = new Slot();
        slot.Add(new Spell
        {
            Id = potionId,
            Name = "爆发药",
            TargetType = SpellTargetType.Self,
            Type = SpellType.Ability,
            SpellCategory = SpellCategory.Potion
        });
        ACRLifecycle.Runner.SpellQueue.Enqueue(slot);
    }

    /// <summary>获取当前可用爆发药 ID，没有则返回 0</summary>
    private static uint GetPotionId()
    {
        // 常见高等级爆发药（5.0~7.0），按品质从高到低
        uint[] potionIds = { 39731, 39730, 36109, 36108, 31892, 31891, 27996, 27995, 23164, 23163, 19886 };
        foreach (var id in potionIds)
        {
            if (HasItem(id))
                return id;
        }
        return 0;
    }

    private static unsafe bool HasItem(uint itemId)
    {
        var inv = FFXIVClientStructs.FFXIV.Client.Game.InventoryManager.Instance();
        if (inv == null) return false;
        return inv->GetInventoryItemCount(itemId) > 0;
    }
}
