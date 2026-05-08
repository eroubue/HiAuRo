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
    private int _abilityCount;

    public AILoop_Normal(List<SlotResolverData> resolvers)
    {
        _resolvers = resolvers;
    }

    public Slot? GetNextSlot(bool blockBuild = false)
    {
        if (_resolvers.Count == 0) return null;

        bool isGcdReady = GCDHelper.IsGCDReady();
        bool isOffGcdWindow = GCDHelper.CanUseOffGcd();
        var maxAbility = 2;

        foreach (var data in _resolvers)
        {
            // Check() 不管窗口，全调
            int checkResult;
            try
            {
                checkResult = data.Resolver.Check();
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Error($"[AILoop] Check error: {ex.Message}");
                continue;
            }

            if (checkResult < 0) continue; // 该 Resolver 不想执行

            // 窗口判定：Mode 控制 Build/执行时机
            bool canExecute = data.Mode switch
            {
                SlotMode.Gcd    => isGcdReady,
                SlotMode.OffGcd => isOffGcdWindow && _abilityCount < maxAbility,
                SlotMode.Always => true,
                _              => false
            };

            if (!canExecute) continue;

            // 暂停/停止时只阻断 Build，Check 已执行完毕
            if (blockBuild) return null;

            // Build + 执行
            try
            {
                var slot = new Slot();
                data.Resolver.Build(slot);

                if (data.Mode == SlotMode.Gcd)
                    _abilityCount = 0; // GCD 重置能力技计数
                else if (slot.Actions.Any(a => a.Spell.IsAbility()))
                    _abilityCount++;

                return slot;
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Error($"[AILoop] Build error: {ex.Message}");
            }
        }

        return null;
    }
}
