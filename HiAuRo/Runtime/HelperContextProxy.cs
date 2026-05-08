using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OmenTools.OmenService;

namespace HiAuRo.Runtime;

/// <summary>
/// HelperRuntime 上下文实现 —— 通过 DService 提供 HasStatus/HasStatusOnTarget/GetGauge
/// 通过 Roslyn 在 Helper 的 ALC 中编译一个实现 IHelperContext 的适配类，
/// 避免 DispatchProxy 跨 ALC 类型等效性问题
/// </summary>
sealed class HiAuRoContextImpl
{
    public bool HasStatus(uint statusId)
    {
        return LocalPlayerState.HasStatus(statusId, out _);
    }

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

    public T? GetGauge<T>() where T : class
    {
        return (T?)GetGauge(typeof(T));
    }

    public object? GetGauge(Type t)
    {
        var gauges = DService.Instance().JobGauges;
        var method = gauges.GetType().GetMethod(nameof(gauges.Get))?.MakeGenericMethod(t);
        return method?.Invoke(gauges, null);
    }
}

/// <summary>
/// Roslyn 编译适配器 —— 在 Helper ALC 中编译一个普通 C# 类实现 IHelperContext，
/// 通过静态委托字段桥接 HiAuRoContextImpl（跨 ALC 安全）
/// </summary>
static class RoslynCompiledProxy
{
    private const string AdapterSource = @"
using System.Runtime.CompilerServices;
using HiAuRo.Helper;
using System;

[assembly: IgnoresAccessChecksTo(""HiAuRo.Helper"")]

public sealed class HelperContextAdapter : IHelperContext
{
    public static Func<uint, bool> OnHasStatus = null!;
    public static Func<uint, bool> OnHasStatusOnTarget = null!;
    public static Func<Type, object?> OnGetGauge = null!;

    public bool HasStatus(uint statusId) => OnHasStatus(statusId);
    public bool HasStatusOnTarget(uint statusId) => OnHasStatusOnTarget(statusId);
    public T? GetGauge<T>() where T : class => (T?)OnGetGauge(typeof(T));
}
";

    private static readonly CSharpCompilationOptions _compileOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        .WithOptimizationLevel(OptimizationLevel.Release);

    private static List<MetadataReference>? _refCache;
    private static readonly object _refLock = new();

    /// <summary>收集编译引用（首次运行时收集，后续复用缓存）</summary>
    private static List<MetadataReference> GetReferences(string helperDllPath)
    {
        lock (_refLock)
        {
            if (_refCache != null) return _refCache;

            _refCache = [];
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic || string.IsNullOrEmpty(asm.Location)) continue;
                try { _refCache.Add(MetadataReference.CreateFromFile(asm.Location)); }
                catch { }
            }
            // 确保 Helper DLL 引用存在
            try { _refCache.Add(MetadataReference.CreateFromFile(helperDllPath)); }
            catch { }
            return _refCache;
        }
    }

    /// <summary>创建 Helper ALC 内的 IHelperContext 实现实例</summary>
    public static object Create(HiAuRoContextImpl impl, string dllPath, AssemblyLoadContext helperAlc)
    {
        var compilation = CSharpCompilation.Create(
            "HiAuRo.ContextAdapter",
            [CSharpSyntaxTree.ParseText(AdapterSource)],
            GetReferences(dllPath),
            _compileOptions);

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);
        if (!result.Success)
        {
            var errors = string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            throw new InvalidOperationException($"编译 ContextAdapter 失败:\n{errors}");
        }

        ms.Position = 0;

        // 在 Helper ALC 中加载编译出的程序集
        Assembly adapterAsm;
        using (helperAlc.EnterContextualReflection())
        {
            adapterAsm = helperAlc.LoadFromStream(ms);
        }

        // 设置静态委托字段
        var adapterType = adapterAsm.GetType("HelperContextAdapter")!;
        adapterType.GetField("OnHasStatus")!.SetValue(null, (Func<uint, bool>)impl.HasStatus);
        adapterType.GetField("OnHasStatusOnTarget")!.SetValue(null, (Func<uint, bool>)impl.HasStatusOnTarget);
        adapterType.GetField("OnGetGauge")!.SetValue(null, (Func<Type, object?>)impl.GetGauge);

        return Activator.CreateInstance(adapterType)!;
    }
}
