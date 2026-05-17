using HiAuRo.ACR;

namespace HiAuRo.Runtime;

/// <summary>
/// PVE 正常循环 —— 对齐 AE
/// Check() 不区分 GCD/oGCD 窗口，所有 Resolver 每帧都调用
/// SlotMode 只控制 Build/执行时机，不控制 Check()
/// </summary>
public sealed class AILoop_Normal : IAILoop
{
    private readonly List<SlotResolverData> _resolvers;
    private readonly List<ResolverDebugInfo> _debugInfos;

    /// <summary>ACR Debug 面板数据（每帧刷新，只读访问）</summary>
    public IReadOnlyList<ResolverDebugInfo> DebugResolvers => _debugInfos;

    /// <summary>Initializes a new instance of the <see cref="AILoop_Normal"/> class</summary>
    public AILoop_Normal(List<SlotResolverData> resolvers)
    {
        _resolvers = resolvers;
        _debugInfos = new List<ResolverDebugInfo>(resolvers.Count);
        foreach (var data in resolvers)
            _debugInfos.Add(new ResolverDebugInfo
            {
                Name = data.Resolver.GetType().Name,
                Mode = data.Mode
            });
    }

    /// <summary>获取下一个待执行的 Slot</summary>
    public Slot? GetNextSlot(bool blockBuild = false)
    {
        // 每帧重置 debug 数据
        foreach (var info in _debugInfos)
        {
            info.CheckResult = -99;
            info.CheckThrew = false;
            info.CheckError = "";
            info.PassedWindow = false;
            info.BuiltSlot = false;
            info.BuiltSkills = "";
        }

        if (_resolvers.Count == 0)
        {
            DService.Instance().Log.Error("[AILoop] 没有已注册的 SlotResolver");
            return null;
        }

        // 无目标时不调 Check，ACR 作者在 OnNoTarget 中自行处理
        if (Data.Target.Current == null)
        {
            foreach (var info in _debugInfos)
            {
                info.CheckResult = -99;
                info.CheckThrew = false;
                info.CheckError = "";
                info.PassedWindow = false;
                info.BuiltSlot = false;
                info.BuiltSkills = "";
            }
            return null;
        }

        bool isGcdReady = GCDHelper.IsGCDReady();
        bool isOffGcdWindow = GCDHelper.CanUseOffGcd();
        float gcdRemain = isGcdReady ? 0 : GCDHelper.GetGCDCooldown();

        for (int i = 0; i < _resolvers.Count; i++)
        {
            var data = _resolvers[i];
            var info = _debugInfos[i];

            // Check() 不管窗口，全调
            int checkResult;
            try
            {
                checkResult = data.Resolver.Check();
                info.CheckResult = checkResult;
            }
            catch (Exception ex)
            {
                info.CheckResult = -99;
                info.CheckThrew = true;
                info.CheckError = ex.Message;
                DService.Instance().Log.Error($"[AILoop] Check#{data.Resolver.GetType().Name} 异常: {ex}");
                continue;
            }

            if (checkResult < 0) continue; // 该 Resolver 不想执行

            // 窗口判定：Mode 控制 Build/执行时机
            bool canExecute = data.Mode switch
            {
                SlotMode.Gcd    => isGcdReady,
                SlotMode.OffGcd => isOffGcdWindow && Data.Combat.AbilityCountInGcd < Data.Combat.MaxAbilityTimesInGcd,
                SlotMode.Always => true,
                _              => false
            };

            if (!canExecute)
            {
                info.PassedWindow = false;
                continue;
            }

            info.PassedWindow = true;

            // 暂停/停止时只阻断 Build，Check 已执行完毕
            if (blockBuild) return null;

            // Build + 执行
            try
            {
                var slot = new Slot();
                data.Resolver.Build(slot);
                var resolverName = data.Resolver.GetType().Name;

                info.BuiltSlot = true;
                info.BuiltSkills = string.Join(",", slot.Actions.Select(a => a.Spell.Name));

                DService.Instance().Log.Information($"[AILoop] Build: {resolverName} → {slot.Actions.Count}个技能 = [{string.Join(", ", slot.Actions.Select(a => $"{a.Spell.Name}({a.Spell.Id})"))}] (GCD={isGcdReady} oGCD={isOffGcdWindow} GCDr={gcdRemain:F0}ms ab={Data.Combat.AbilityCountInGcd}/{Data.Combat.MaxAbilityTimesInGcd})");

                if (data.Mode == SlotMode.Gcd)
                    Data.Combat.AbilityCountInGcd = 0;
                else if (slot.Actions.Any(a => a.Spell.IsAbility()))
                    Data.Combat.AbilityCountInGcd++;

                return slot;
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Error($"[AILoop] Build error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        return null;
    }
}
