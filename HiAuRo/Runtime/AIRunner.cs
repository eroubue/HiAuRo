using HiAuRo.ACR;
using HiAuRo.Data;
using HiAuRo.Decision;
using HiAuRo.Execution;
using HiAuRo.FactAxis;
using HiAuRo.Infrastructure;

namespace HiAuRo.Runtime;

/// <summary>
/// AI 主引擎 —— 加载 ACR、调度 IAILoop + SlotExecutor
/// </summary>
public sealed class AIRunner
{
    public IRotationEntry? CurrentEntry { get; private set; }
    public Rotation? CurrentRotation { get; private set; }
    public IAILoop? AiLoop { get; private set; }
    public IRotationEventHandler? EventHandler => CurrentRotation?.EventHandler;

    public SpellQueue SpellQueue { get; } = new();
    public OpenerMgr OpenerMgr { get; } = new();
    public SlotExecutor SlotExecutor { get; private set; }
    public CountDownHandler CountDownHandler { get; } = new();

    private int _battleTimeMs;
    private bool _loaded;
    private CombatContext.State _prevFactAxisState;
    private CombatContext.State _prevExecAxisState; // 执行轴战斗状态追踪
    private CombatContext.State _prevAssistAxisState; // 辅助轴战斗状态追踪
    private CombatContext.State _prevState; // 用于检测战斗状态切换
    private uint _lastTerritoryId; // 用于检测切图

    public AIRunner()
    {
        SlotExecutor = new SlotExecutor(this);
    }

    /// <summary>加载 ACR</summary>
    public void Load(IRotationEntry entry, string settingFolder)
    {
        Unload();

        CurrentEntry = entry;
        CurrentRotation = entry.Build(settingFolder);

        if (CurrentRotation != null)
        {
            AiLoop = new AILoop_Normal(CurrentRotation.SlotResolvers);
        }

        // 注册倒计时行为
        if (CurrentRotation?.Opener != null)
        {
            CurrentRotation.Opener.InitCountDown(CountDownHandler);
        }

        // 注册 Rotation 级热键处理器
        if (CurrentRotation?.HotkeyEventHandlers != null)
        {
            foreach (var handler in CurrentRotation.HotkeyEventHandlers)
                HotkeyHelper.RegisterHandler(handler);
        }

        CurrentEntry.OnEnterRotation();
        CurrentRotation?.EventHandler?.OnEnterRotation();

        _loaded = true;
    }

    /// <summary>卸载 ACR</summary>
    public void Unload()
    {
        if (!_loaded) return;

        CurrentRotation?.EventHandler?.OnExitRotation();
        CurrentEntry?.OnExitRotation();

        // 注销 Rotation 级热键处理器
        if (CurrentRotation?.HotkeyEventHandlers != null)
        {
            foreach (var handler in CurrentRotation.HotkeyEventHandlers)
                HotkeyHelper.UnregisterHandler(handler);
        }

        CurrentEntry?.Dispose();

        SpellQueue.Clear();
        OpenerMgr.Reset();
        CountDownHandler.Reset();
        Coroutine.Instance.Clear();

        AiLoop = null;
        CurrentRotation = null;
        CurrentEntry = null;
        _loaded = false;
        _battleTimeMs = 0;
    }

    /// <summary>每帧由 RuntimeCore 调用</summary>
    public void Update()
    {
        if (!_loaded || AiLoop == null) return;

        var stopped = !RuntimeCore.IsRunning;
        var paused = ACR.MainControlHelper.IsPaused;
        var blockBuild = stopped || paused;  // 停止/暂停都阻断 Build+执行，但 Check 继续

        try
        {
            var state = CombatContext.CurrentState;

            // 进入战斗时清掉非战斗攒下的热键队列，避免延迟爆发
            if (state != _prevState)
            {
                DService.Instance().Log.Information($"[AIRunner] 战斗状态切换: {_prevState} → {state}");
                if (state == CombatContext.State.InCombat)
                {
                    DService.Instance().Log.Information("[AIRunner] 进入战斗, 清空旧 SpellQueue");
                    SpellQueue.Clear();
                }
            }
            _prevState = state;

            // 切图检测
            var territoryId = Data.Combat.TerritoryType;
            if (territoryId != _lastTerritoryId)
            {
                if (_lastTerritoryId != 0)
                {
                    DService.Instance().Log.Information($"[AIRunner] 切图: {_lastTerritoryId} → {territoryId}");
                    Data.Combat.AbilityCountInGcd = 0;
                    Data.Combat.MaxAbilityTimesInGcd = PluginConfig.Instance.MaxAbilityTimesInGcd;
                    CurrentRotation?.EventHandler?.OnTerritoryChanged();
                }
                _lastTerritoryId = territoryId;
            }

            if (state == CombatContext.State.Idle || state == CombatContext.State.Zoning)
            {
                Data.Objects.Refresh();
                ProcessSpellQueue(blockBuild);
                return;
            }

            if (state != CombatContext.State.InCombat)
            {
                CurrentRotation?.EventHandler?.OnPreCombat();
                // 倒计时阶段行为检查
                UpdateCountDown();
                ProcessSpellQueue(blockBuild);
                return;
            }

            // 更新战斗数据和对象扫描
            Data.Objects.Refresh();
            Data.Party.Refresh();

            // 无目标 → 尝试通过 TargetResolvers 自动选择
            if (Data.Target.Current == null)
            {
                if (TryResolveTarget())
                {
                    DService.Instance().Log.Information($"[AIRunner] 自动选择目标: {Data.Target.Current?.Name}");
                    // 目标已选中，继续正常循环
                }
                else
                {
                    DService.Instance().Log.Debug("[AIRunner] 无目标且未能自动选择, 调用 OnNoTarget");
                    CurrentRotation?.EventHandler?.OnNoTarget();
                    return;
                }
            }

            // 战斗计时器
            _battleTimeMs += (int)(Data.Combat.DeltaTime * 1000);
            CurrentRotation?.EventHandler?.OnBattleUpdate(_battleTimeMs);

            // ACR 暂停检查
            if (CurrentRotation?.CanPauseACRCheck != null)
            {
                var pauseResult = CurrentRotation.CanPauseACRCheck();
                if (pauseResult > 0)
                {
                    DService.Instance().Log.Debug($"[AIRunner] ACR 暂停检查返回 {pauseResult}, 跳过本帧");
                    return;
                }
            }

            // --- 执行轴检查（Phase 6） ---
            if (ModeSwitch.CurrentMode == ModeSwitch.Mode.ExecutionAxis)
            {
                // 战斗状态变化 → 启停执行轴
                if (state != _prevExecAxisState)
                {
                    _prevExecAxisState = state;
                    if (state == CombatContext.State.InCombat)
                        ExecutionAxis.Instance.Start();
                    else if (state == CombatContext.State.OutOfCombat || state == CombatContext.State.Idle)
                        ExecutionAxis.Instance.Stop();
                }

                var execOutput = ExecutionAxis.Instance.Update(_battleTimeMs);
                if (execOutput != null)
                {
                    // 暂停 ACR
                    if (execOutput.PauseAcr)
                        return;

                    // 强制切换目标（不受 blockBuild 影响）
                    if (execOutput.ForceTarget != null)
                    {
                        OmenTools.OmenService.TargetManager.Target = execOutput.ForceTarget;
                    }

                    // 强制释放技能（受 blockBuild 影响）
                    if (execOutput.ForceSpell != null && !blockBuild)
                    {
                        if (CurrentRotation?.CanUseHighPrioritySlotCheck != null)
                        {
                            var canUse = CurrentRotation.CanUseHighPrioritySlotCheck();
                            if (canUse < 0) return;
                        }

                        var slot = new Slot();
                        slot.Add(execOutput.ForceSpell);
                        SlotExecutor.ExecuteSlot(slot);
                        return;
                    }

                    // 消费帧（跳过正常循环）
                    if (execOutput.ConsumeFrame)
                        return;
                }

                // 检查 Rotation 全局触发器（扁平列表）
                if (CurrentRotation?.TriggerConditions.Count > 0 &&
                    CurrentRotation?.TriggerActions.Count > 0)
                {
                    var minCount = Math.Min(CurrentRotation.TriggerConditions.Count,
                                            CurrentRotation.TriggerActions.Count);
                    for (int i = 0; i < minCount; i++)
                    {
                        if (CurrentRotation.TriggerConditions[i].Handle())
                        {
                            CurrentRotation.TriggerActions[i].Handle();
                            break;
                        }
                    }
                }
            }

            // --- 事实轴检查（Phase 7） ---
            if (ModeSwitch.CurrentMode == ModeSwitch.Mode.FactAxis)
            {
                UpdateFactAxis(state);
            }

            // --- 辅助轴（始终运行，独立于执行轴/事实轴） ---
            UpdateAssistAxis(state);

            // 起手序列（受 blockBuild 影响）
            if (!blockBuild && CurrentRotation?.Opener != null && OpenerMgr.CurrentState == OpenerMgr.State.NotStarted)
            {
                if (OpenerMgr.Start(CurrentRotation.Opener))
                {
                    var openerSlot = OpenerMgr.Update();
                    if (openerSlot != null)
                    {
                        SlotExecutor.ExecuteSlot(openerSlot);
                        return;
                    }
                }
            }

            if (!blockBuild && OpenerMgr.CurrentState == OpenerMgr.State.Running)
            {
                var openerSlot = OpenerMgr.Update();
                if (openerSlot != null)
                {
                    SlotExecutor.ExecuteSlot(openerSlot);
                    return;
                }
            }

            // 执行队列中待处理的 Slot（受 blockBuild 影响，优先于 AI 循环）
            if (ProcessSpellQueue(blockBuild))
                return;

            // 从 AI 循环获取下一个 Slot（Check 总是执行，Build 受 blockBuild 影响）
            var nextSlot = AiLoop.GetNextSlot(blockBuild);
            if (nextSlot != null)
                SlotExecutor.ExecuteSlot(nextSlot);
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[AIRunner] Update 异常: {ex}");
        }
    }

    /// <summary>重置战斗状态</summary>
    public void Reset()
    {
        SpellQueue.Clear();
        OpenerMgr.Reset();
        CountDownHandler.Reset();
        Coroutine.Instance.Clear();
        _battleTimeMs = 0;
        Data.Combat.AbilityCountInGcd = 0;
        Data.Combat.MaxAbilityTimesInGcd = PluginConfig.Instance.MaxAbilityTimesInGcd;
        CurrentRotation?.EventHandler?.OnResetBattle();
    }

    /// <summary>倒计时阶段检查（通过 IPC 获取游戏倒计时）</summary>
    private void UpdateCountDown()
    {
        if (!CountDownHandler.HasPending) return;

        try
        {
            var pi = DService.Instance().PI;
            var countdownIpc = pi.GetIpcSubscriber<float>("Countdown.CountdownTimer");
            var remaining = countdownIpc.InvokeFunc();
            CountDownHandler.Update(remaining);
        }
        catch
        {
            // IPC 不可用，跳过
        }
    }

    /// <summary>
    /// 事实轴更新（Phase 7）—— 不控制 ACR，只输出当前战斗事实状态
    /// </summary>
    /// <summary>
    /// 辅助轴更新 — 始终运行，独立于执行轴/事实轴
    /// </summary>
    private void UpdateAssistAxis(CombatContext.State state)
    {
        if (state != _prevAssistAxisState)
        {
            _prevAssistAxisState = state;
            if (state == CombatContext.State.InCombat)
                AssistAxis.Instance.Start();
            else if (state == CombatContext.State.OutOfCombat || state == CombatContext.State.Idle)
                AssistAxis.Instance.Stop();
        }

        if (state != CombatContext.State.InCombat) return;

        var output = AssistAxis.Instance.Update(_battleTimeMs);
        if (output?.ForceSpell != null)
        {
            var slot = new Slot();
            slot.Add(output.ForceSpell);
            SlotExecutor.ExecuteSlot(slot);
        }
    }

    private void UpdateFactAxis(CombatContext.State state)
    {
        // 战斗状态变化 → 启停事实轴
        if (state != _prevFactAxisState)
        {
            _prevFactAxisState = state;
            if (state == CombatContext.State.InCombat)
                FactTimeline.Instance.Start();
            else if (state == CombatContext.State.OutOfCombat || state == CombatContext.State.Idle)
                FactTimeline.Instance.Stop();
        }

        if (state != CombatContext.State.InCombat) return;

        // 推送同步事件
        var lastAction = EventSystem.LastCompletedActionId;
        if (lastAction != 0)
        {
            FactTimeline.Instance.PushSyncEvent(new FactAxis.SyncContext
            {
                EventType = "ability",
                AbilityId = lastAction
            });
        }

        // 检测 Boss 读条（StartsUsing 同步）
        UpdateFactAxisStartsUsing();

        // 推进时间轴
        FactTimeline.Instance.Update(_battleTimeMs);

        // Phase 8: 消费事实轴需求 → 决策分配
        UpdateDecisions();
    }

    /// <summary>Phase 8 — 从事实轴当前事件提取需求，运行决策引擎</summary>
    private void UpdateDecisions()
    {
        var state = FactTimeline.Instance.State;
        var ev = state.CurrentEvent;
        if (ev == null || ev.Actions.Count == 0) return;

        int 需求减伤 = 0, 需求治疗 = 0;
        foreach (var action in ev.Actions)
        {
            if (action is 需求动作 demand)
            {
                需求减伤 = demand.需求减伤;
                需求治疗 = demand.需求治疗;
            }
        }

        if (需求减伤 == 0 && 需求治疗 == 0) return;

        var output = DecisionEngine.Instance.计算(需求减伤, 需求治疗);
        if (output.执行技能IDs.Count > 0)
        {
            DService.Instance().Log.Information(
                $"[Decision] {state.PhaseName}: 减伤={output.减伤合计}/{需求减伤} " +
                $"治疗={output.治疗合计}/{需求治疗} " +
                $"分配={string.Join(",", output.减伤分配.Select(m => $"{m.技能名称}({m.减伤值}%)"))}");

            // 强制全部分配技能依次发（ACR 不能跳过）
            foreach (var skillId in output.执行技能IDs)
            {
                var slot = new Slot();
                slot.Add(new ACR.Spell { Id = skillId, Name = "决策技能", Type = ACR.SpellType.Ability, TargetType = ACR.SpellTargetType.Self });
                SlotExecutor.ExecuteSlot(slot);
            }
        }
    }

    /// <summary>
    /// 检测敌方单位读条变化，推送 StartsUsing 同步事件
    /// </summary>
    private void UpdateFactAxisStartsUsing()
    {
        var enemies = global::HiAuRo.Data.Objects.Enemies;
        if (enemies == null) return;

        foreach (var enemy in enemies)
        {
            if (enemy is not IBattleChara battleChara) continue;
            if (!battleChara.IsCasting) continue;

            var castId = battleChara.CastActionID;
            if (castId == 0) continue;

            FactTimeline.Instance.PushSyncEvent(new FactAxis.SyncContext
            {
                EventType = "startsUsing",
                AbilityId = castId
            });
            break; // 一次只推送一个
        }
    }

    /// <summary>通过 Rotation.TargetResolvers 自动选择目标</summary>
    private bool TryResolveTarget()
    {
        if (CurrentRotation?.TargetResolvers == null || CurrentRotation.TargetResolvers.Count == 0)
            return false;

        foreach (var resolver in CurrentRotation.TargetResolvers)
        {
            try
            {
                if (resolver.ResolveTarget(out var target))
                {
                    OmenTools.OmenService.TargetManager.Target = target;
                    return Data.Target.Current != null;
                }
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Error($"[AIRunner] TargetResolver 异常: {ex}");
            }
        }

        return false;
    }

    internal bool ProcessSpellQueue(bool blockBuild)
    {
        if (blockBuild || !SpellQueue.HasPending()) return false;
        var queued = SpellQueue.GetNext();
        if (queued != null)
        {
            DService.Instance().Log.Information($"[AIRunner] ProcessSpellQueue: executing {queued.Actions.Count} spell(s)");
            SlotExecutor.ExecuteSlot(queued);
            return true;
        }
        return false;
    }
}
