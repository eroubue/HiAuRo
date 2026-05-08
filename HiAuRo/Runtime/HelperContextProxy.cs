using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using OmenTools.OmenService;

namespace HiAuRo.Runtime;

/// <summary>
/// HelperRuntime 上下文实现 —— 通过 DService 提供 HasStatus/HasStatusOnTarget/GetGauge
/// 在 Helper 的 ALC 内用 Reflection.Emit 生成名为 "HiAuRo" 的程序集实现 IHelperContext，
/// 利用 InternalsVisibleTo("HiAuRo") 授权访问 internal 接口
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
/// Reflection.Emit 适配器 —— 在 Helper ALC 内生成程序集（名称 "HiAuRo"），
/// 匹配 Helper 的 InternalsVisibleTo，实现 IHelperContext 并委托到 HiAuRoContextImpl
/// </summary>
static class EmitProxy
{
    /// <summary>创建 Helper ALC 内的 IHelperContext 实现实例</summary>
    public static object Create(HiAuRoContextImpl impl, Assembly helperAsm, AssemblyLoadContext helperAlc)
    {
        var ctxType = helperAsm.GetType("HiAuRo.Helper.IHelperContext")!;

        using (helperAlc.EnterContextualReflection())
        {
            // 程序集名必须为 "HiAuRo" 以匹配 InternalsVisibleTo
            var asmName = new AssemblyName("HiAuRo");
            var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
            var modBuilder = asmBuilder.DefineDynamicModule("Main");
            var typeBuilder = modBuilder.DefineType("HelperContextAdapter", TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed);
            typeBuilder.AddInterfaceImplementation(ctxType);

            // 静态委托字段
            var fldHasStatus = typeBuilder.DefineField("OnHasStatus", typeof(Func<uint, bool>),
                FieldAttributes.Public | FieldAttributes.Static);
            var fldHasStatusOnTarget = typeBuilder.DefineField("OnHasStatusOnTarget", typeof(Func<uint, bool>),
                FieldAttributes.Public | FieldAttributes.Static);
            var fldGetGauge = typeBuilder.DefineField("OnGetGauge", typeof(Func<Type, object?>),
                FieldAttributes.Public | FieldAttributes.Static);

            var invokeFunc = typeof(Func<uint, bool>).GetMethod("Invoke")!;
            var invokeFunc2 = typeof(Func<Type, object?>).GetMethod("Invoke")!;
            var getTypeFromHandle = typeof(Type).GetMethod("GetTypeFromHandle")!;

            // --- HasStatus ---
            {
                var method = typeBuilder.DefineMethod("HasStatus",
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    typeof(bool), [typeof(uint)]);
                var il = method.GetILGenerator();
                il.Emit(OpCodes.Ldsfld, fldHasStatus);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Callvirt, invokeFunc);
                il.Emit(OpCodes.Ret);
                typeBuilder.DefineMethodOverride(method, ctxType.GetMethod("HasStatus")!);
            }

            // --- HasStatusOnTarget ---
            {
                var method = typeBuilder.DefineMethod("HasStatusOnTarget",
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    typeof(bool), [typeof(uint)]);
                var il = method.GetILGenerator();
                il.Emit(OpCodes.Ldsfld, fldHasStatusOnTarget);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Callvirt, invokeFunc);
                il.Emit(OpCodes.Ret);
                typeBuilder.DefineMethodOverride(method, ctxType.GetMethod("HasStatusOnTarget")!);
            }

            // --- GetGauge<T> ---
            {
                var method = typeBuilder.DefineMethod("GetGauge",
                    MethodAttributes.Public | MethodAttributes.Virtual);
                var gParams = method.DefineGenericParameters("T");
                gParams[0].SetGenericParameterAttributes(GenericParameterAttributes.ReferenceTypeConstraint);
                method.SetReturnType(gParams[0]);

                var il = method.GetILGenerator();
                il.Emit(OpCodes.Ldsfld, fldGetGauge);
                il.Emit(OpCodes.Ldtoken, gParams[0]);
                il.Emit(OpCodes.Call, getTypeFromHandle);
                il.Emit(OpCodes.Callvirt, invokeFunc2);
                il.Emit(OpCodes.Unbox_Any, gParams[0]);
                il.Emit(OpCodes.Ret);
                typeBuilder.DefineMethodOverride(method, ctxType.GetMethod("GetGauge")!);
            }

            var proxyType = typeBuilder.CreateType()!;

            // 注入委托
            proxyType.GetField("OnHasStatus")!.SetValue(null, (Func<uint, bool>)impl.HasStatus);
            proxyType.GetField("OnHasStatusOnTarget")!.SetValue(null, (Func<uint, bool>)impl.HasStatusOnTarget);
            proxyType.GetField("OnGetGauge")!.SetValue(null, (Func<Type, object?>)impl.GetGauge);

            return Activator.CreateInstance(proxyType)!;
        }
    }
}
