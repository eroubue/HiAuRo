using System.Reflection;
using OmenTools.OmenService;

namespace HiAuRo.Runtime;

/// <summary>
/// HelperRuntime 上下文实现 —— 通过 DService 提供 HasStatus/HasStatusOnTarget/GetGauge
/// 由于 HiAuRo.Helper.dll 是运行时动态加载（非编译期引用），
/// 通过 DispatchProxy 创建适配 IHelperContext 接口的代理对象
/// </summary>
sealed class HiAuRoContextImpl
{
    /// <summary>检查玩家自身是否有指定状态</summary>
    public bool HasStatus(uint statusId)
    {
        return LocalPlayerState.HasStatus(statusId, out _);
    }

    /// <summary>检查当前目标是否有指定状态</summary>
    public bool HasStatusOnTarget(uint statusId)
    {
        var target = TargetManager.SoftTarget ?? TargetManager.Target;
        if (target is not IBattleChara battle) return false;

        foreach (var s in battle.StatusList)
        {
            if (s.StatusID == statusId)
                return true;
        }
        return false;
    }

    /// <summary>获取职业量谱（泛型版本，内部通过反射适配 JobGaugeBase 约束差异）</summary>
    public T? GetGauge<T>() where T : class
    {
        return (T?)GetGauge(typeof(T));
    }

    /// <summary>通过 Type 获取职业量谱（非泛型版本，供反射调用）</summary>
    public object? GetGauge(Type t)
    {
        var gauges = DService.Instance().JobGauges;
        var method = gauges.GetType().GetMethod(nameof(gauges.Get))?.MakeGenericMethod(t);
        return method?.Invoke(gauges, null);
    }
}

/// <summary>
/// DispatchProxy 适配器 —— 将 HiAuRoContextImpl 包装为 Helper DLL 的 IHelperContext 实现
/// 运行时动态加载的 Helper DLL 中 IHelperContext 为 internal，DispatchProxy 无需编译期类型引用
/// </summary>
sealed class HelperDispatchProxy : DispatchProxy
{
    private HiAuRoContextImpl _impl = null!;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null) return null;

        return targetMethod.Name switch
        {
            nameof(HiAuRoContextImpl.HasStatus) =>
                _impl.HasStatus((uint)args![0]!),
            nameof(HiAuRoContextImpl.HasStatusOnTarget) =>
                _impl.HasStatusOnTarget((uint)args![0]!),
            "GetGauge" =>
                _impl.GetGauge(targetMethod.GetGenericArguments().FirstOrDefault() ?? typeof(object)),
            _ => null
        };
    }

    /// <summary>创建适配 Helper DLL 中 IHelperContext 接口的代理实例</summary>
    public static object CreateProxy(HiAuRoContextImpl impl, Assembly helperAsm)
    {
        var ctxType = helperAsm.GetType("HiAuRo.Helper.IHelperContext")!;
        var createMethod = typeof(DispatchProxy)
            .GetMethod(nameof(DispatchProxy.Create), BindingFlags.Public | BindingFlags.Static)
            !.MakeGenericMethod(ctxType, typeof(HelperDispatchProxy));

        var proxy = createMethod.Invoke(null, null)!;
        // proxy 继承自 HelperDispatchProxy 的子类，可强制转换
        ((HelperDispatchProxy)proxy)._impl = impl;
        return proxy;
    }
}
