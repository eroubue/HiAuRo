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

    /// <summary>Initializes a new instance of the <see cref="SlotExecutor"/> class</summary>
    public SlotExecutor(AIRunner runner)
    {
        _runner = runner;
    }

    /// <summary>执行 Slot</summary>
    public void ExecuteSlot(Slot slot)
    {
        var handler = _runner.EventHandler;

        DService.Instance().Log.Debug($"[SlotExec] 开始执行 Slot, Actions={slot.Actions.Count}");

        foreach (var action in slot.Actions)
        {
            var spell = action.Spell;

            if (spell.Id == 0)
            {
                DService.Instance().Log.Debug("[SlotExec] SpellId=0, 延迟100ms后继续");
                Coroutine.Instance.WaitAsync(100);
                continue;
            }

            switch (action.Wait)
            {
                case WaitType.WaitInMs:
                    DService.Instance().Log.Debug($"[SlotExec] WaitInMs={action.TimeInMs}ms ({spell.Name})");
                    Coroutine.Instance.WaitAsync(action.TimeInMs);
                    break;
                case WaitType.WaitForSndHalfWindow:
                    var remain = GCDHelper.GetGCDCooldown();
                    var duration = GCDHelper.GetGCDDuration();
                    var waitMs = (long)(remain - duration * 0.5f);
                    if (waitMs > 0)
                    {
                        DService.Instance().Log.Debug($"[SlotExec] WaitForSndHalfWindow={waitMs}ms (GCD剩余={remain:F0}ms) ({spell.Name})");
                        Coroutine.Instance.WaitAsync(waitMs);
                    }
                    break;
            }

            handler?.BeforeSpell(slot, spell);

            if (spell.IsAbility() && !Data.Combat.AbilityIntervalElapsed)
            {
                DService.Instance().Log.Debug($"[SlotExec] 能力技间隔未到, 跳过 {spell.Name} ({Environment.TickCount64 - Data.Combat.LastAbilityUseTime}ms < {PluginConfig.Instance.AbilityIntervalMs}ms)");
                continue;
            }

            var targetId = ResolveTarget(spell);
            var targetName = GetTargetNameById(targetId);
            var actionType = SpellCategoryToActionType(spell.SpellCategory);

            DService.Instance().Log.Debug($"[SlotExec] UseAction: {spell.Name}({spell.Id}) TargetType={spell.TargetType} TargetId={targetId:X}({targetName}) ActionType={actionType}");
            var useResult = UseActionManager.Instance().UseAction(actionType, spell.Id, targetId, 0, 0, 0);
            DService.Instance().Log.Debug($"[SlotExec] UseAction result={useResult}");

            if (useResult)
            {
                EventSystem.OnUseActionSuccess(spell.Id, spell.Type);
                handler?.OnSpellCastSuccess(slot, spell);
                if (spell.IsAbility())
                    Data.Combat.LastAbilityUseTime = Environment.TickCount64;
            }

            handler?.AfterSpell(slot, spell);
        }

        if (slot.AppendedSequence != null && slot.AppendedSequence.StartCheck() >= 0)
        {
            var seqSlot = new Slot();
            foreach (var action in slot.AppendedSequence.Sequence)
                action(seqSlot);
            if (seqSlot.Actions.Count > 0)
            {
                DService.Instance().Log.Debug($"[SlotExec] 追加序列, 入队 {seqSlot.Actions.Count}个技能");
                _runner.SpellQueue.Enqueue(seqSlot);
            }
        }
    }

    private static string GetTargetNameById(ulong id)
    {
        if (id == 0) return "无目标";
        if (Data.Me.Object?.GameObjectID == id) return "自己";
        if (Data.Target.Current?.GameObjectID == id) return Data.Target.Current.Name.ToString();
        // 检查队伍成员
        foreach (var pm in Data.Party.All)
        {
            if (pm.Player?.GameObjectID == id)
                return pm.Player.Name.ToString();
        }
        return "?";
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
