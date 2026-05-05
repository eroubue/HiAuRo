using HiAuRo.ACR;
using OmenTools.OmenService;
using ActionTypeFF = FFXIVClientStructs.FFXIV.Client.Game.ActionType;

namespace HiAuRo.Runtime;

/// <summary>
/// Slot/SlotAction 执行引擎
/// </summary>
public sealed class SlotExecutor
{
    private readonly AIRunner _runner;

    public SlotExecutor(AIRunner runner)
    {
        _runner = runner;
    }

    public void ExecuteSlot(Slot slot)
    {
        var handler = _runner.EventHandler;

        foreach (var action in slot.Actions)
        {
            var spell = action.Spell;

            if (spell.Id == 0)
            {
                Coroutine.Instance.WaitAsync(100);
                continue;
            }

            switch (action.Wait)
            {
                case WaitType.WaitInMs:
                    Coroutine.Instance.WaitAsync(action.TimeInMs);
                    break;
                case WaitType.WaitForSndHalfWindow:
                    var remain = GCDHelper.GetGCDCooldown();
                    var duration = GCDHelper.GetGCDDuration();
                    var waitMs = (long)(remain - duration * 0.5f);
                    if (waitMs > 0) Coroutine.Instance.WaitAsync(waitMs);
                    break;
            }

            handler?.BeforeSpell(slot, spell);

            var targetId = ResolveTarget(spell);
            var actionType = SpellCategoryToActionType(spell.SpellCategory);

            UseActionManager.Instance().UseAction(actionType, spell.Id, targetId, 0, 0, 0);

            handler?.AfterSpell(slot, spell);
        }

        if (slot.AppendedSequence != null && slot.AppendedSequence.StartCheck() >= 0)
        {
            var seqSlot = new Slot();
            foreach (var action in slot.AppendedSequence.Sequence)
                action(seqSlot);
            if (seqSlot.Actions.Count > 0)
                _runner.SpellQueue.Enqueue(seqSlot);
        }
    }

    private static ulong ResolveTarget(Spell spell)
    {
        switch (spell.TargetType)
        {
            case SpellTargetType.Self:
                return Data.Me.Object?.GameObjectID ?? 0;
            case SpellTargetType.Target:
                return Data.Target.Current?.GameObjectID ?? 0;
            case SpellTargetType.TargetTarget:
                if (Data.Target.Current is IBattleChara bc)
                    return bc.TargetObjectID;
                return 0;
            case SpellTargetType.SpecifyTarget:
                if (spell.SpecifyTarget is IGameObject go)
                    return go.GameObjectID;
                return 0;
            case SpellTargetType.DynamicTarget:
                if (spell.GetDynamicTarget?.Invoke() is IGameObject dgo)
                    return dgo.GameObjectID;
                return 0;
            case SpellTargetType.Pm1: case SpellTargetType.Pm2:
            case SpellTargetType.Pm3: case SpellTargetType.Pm4:
            case SpellTargetType.Pm5: case SpellTargetType.Pm6:
            case SpellTargetType.Pm7: case SpellTargetType.Pm8:
                var idx = (int)spell.TargetType - (int)SpellTargetType.Pm1;
                if (idx >= 0 && idx < Data.Party.All.Count)
                    return Data.Party.All[idx].Player?.GameObjectID ?? 0;
                return 0;
            default:
                return Data.Target.Current?.GameObjectID ?? 0;
        }
    }

    private static ActionTypeFF SpellCategoryToActionType(SpellCategory cat)
    {
        return cat switch
        {
            SpellCategory.Sprint or SpellCategory.Potion or SpellCategory.Item => ActionTypeFF.Item,
            SpellCategory.LimitBreak => ActionTypeFF.GeneralAction,
            _ => ActionTypeFF.Action
        };
    }
}
