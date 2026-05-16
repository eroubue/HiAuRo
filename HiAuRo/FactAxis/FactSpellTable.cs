using HiAuRo.ACR;

namespace HiAuRo.FactAxis;

/// <summary>
/// 事实轴技能执行数据表 — 独立于 DecisionSkillRegistry
/// 存"技能怎么放"（TargetType/类别/类型），DecisionEngine 执行时构造完整 Spell
/// </summary>
public static class FactSpellTable
{
    private static readonly Dictionary<uint, SpellExecutionInfo> _table = new();

    public static void 注册(uint id, string name,
        SpellTargetType target = SpellTargetType.Self,
        SpellCategory category = SpellCategory.Default,
        SpellType type = SpellType.Ability)
    {
        _table[id] = new SpellExecutionInfo
        {
            Id = id,
            Name = name,
            TargetType = target,
            SpellCategory = category,
            Type = type
        };
    }

    public static Spell? 构造Spell(uint id)
    {
        if (!_table.TryGetValue(id, out var info)) return null;
        return new Spell
        {
            Id = info.Id,
            Name = info.Name,
            TargetType = info.TargetType,
            SpellCategory = info.SpellCategory,
            Type = info.Type
        };
    }

    public static void Clear() => _table.Clear();
}

public sealed record SpellExecutionInfo
{
    public uint Id { get; init; }
    public string Name { get; init; } = "";
    public SpellTargetType TargetType { get; init; } = SpellTargetType.Self;
    public SpellCategory SpellCategory { get; init; } = SpellCategory.Default;
    public SpellType Type { get; init; } = SpellType.Ability;
}
