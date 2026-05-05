namespace HiAuRo.ACR;

/// <summary>
/// 技能执行单元 —— 包含连续多个 SlotAction，按顺序执行
/// </summary>
public sealed class Slot
{
    /// <summary>要按顺序执行的技能列表</summary>
    public List<SlotAction> Actions { get; } = [];

    /// <summary>整体失败尝试时间 (ms)，默认 600</summary>
    public int MaxDuration { get; set; } = 600;

    /// <summary>强制延后到下个 GCD</summary>
    public bool Wait2NextGcd { get; set; }

    /// <summary>Slot 快结束时追加的序列</summary>
    public ISlotSequence? AppendedSequence
    { get; private set; }

    // === 构造函数 ===
    public Slot() { }

    public Slot(int maxDuration)
    {
        MaxDuration = maxDuration;
    }

    public Slot(Spell spell, int maxDuration = 600)
    {
        MaxDuration = maxDuration;
        Add(spell);
    }

    // === 添加方法（fluent 链式，返回 Slot） ===

    /// <summary>添加一个技能</summary>
    public Slot Add(Spell spell)
    {
        Actions.Add(new SlotAction { Spell = spell });
        return this;
    }

    /// <summary>添加一个已配置的 SlotAction</summary>
    public Slot Add(SlotAction action)
    {
        Actions.Add(action);
        return this;
    }

    /// <summary>在指定位置插入（默认队首）</summary>
    public Slot Insert(SlotAction action, int index = 0)
    {
        Actions.Insert(Math.Clamp(index, 0, Actions.Count), action);
        return this;
    }

    /// <summary>在第二个能力技窗口添加</summary>
    public Slot Add2NdWindowAbility(Spell spell)
    {
        Actions.Add(new SlotAction
        {
            Spell = spell,
            Wait = WaitType.WaitForSndHalfWindow
        });
        return this;
    }

    /// <summary>延迟后添加技能</summary>
    public Slot AddDelaySpell(int delayMs, Spell spell)
    {
        Actions.Add(new SlotAction
        {
            Spell = spell,
            Wait = WaitType.WaitInMs,
            TimeInMs = delayMs
        });
        return this;
    }

    /// <summary>追加序列（可指定是否强制等待下个 GCD）</summary>
    public void AppendSequence(ISlotSequence? sequence, bool wait2NextGcd = true)
    {
        AppendedSequence = sequence;
        if (wait2NextGcd) Wait2NextGcd = true;
    }

    // === 调试 ===
    public override string ToString()
    {
        var spells = string.Join(" → ", Actions.Select(a => a.Spell.Name));
        return $"Slot[{MaxDuration}ms]: {spells}";
    }
}
