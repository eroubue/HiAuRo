using System.Reflection;
using System.Runtime.Loader;
using HiAuRo.ACR;

namespace HiAuRo.Runtime;

/// <summary>
/// 动态 ACR 加载器 —— 扫描 ACR/ 目录, 独立 ALC 加载 dll, 发现 IRotationEntry
/// </summary>
public static class ACRLoader
{
    private static readonly Dictionary<string, AssemblyLoadContext> _authorContexts = [];
    private static readonly List<Assembly> _loadedAcrAssemblies = [];
    private static readonly HashSet<string> _hostPrefixes = ["HiAuRo", "OmenTools", "Dalamud", "FFXIVClientStructs", "Lumina", "ImGuiNET", "TerraFX", "System.", "Microsoft."];

    public static IReadOnlyList<Assembly> LoadedAcrAssemblies => _loadedAcrAssemblies.AsReadOnly();

    /// <summary>扫描并加载所有外部 ACR，注册到 ACRLifecycle</summary>
    public static void LoadAll(string pluginDir)
    {
        var acrDir = Path.Combine(pluginDir, "ACR");
        if (!Directory.Exists(acrDir)) return;

        foreach (var authorDir in Directory.GetDirectories(acrDir))
        {
            var authorName = Path.GetFileName(authorDir);
            if (authorName == null) continue;

            var alc = new AssemblyLoadContext($"ACR_{authorName}", isCollectible: true);
            alc.Resolving += (ctx, name) => ResolveAssembly(ctx, name, authorDir);

            var found = false;
            foreach (var dllPath in Directory.GetFiles(authorDir, "*.dll"))
            {
                try
                {
                    var asm = alc.LoadFromAssemblyPath(dllPath);
                    _loadedAcrAssemblies.Add(asm);
                    foreach (var type in asm.GetExportedTypes())
                    {
                        if (type is { IsAbstract: false, IsInterface: false } &&
                            typeof(IRotationEntry).IsAssignableFrom(type))
                        {
                            if (Activator.CreateInstance(type) is IRotationEntry entry)
                            {
                                var settingDir = Setting.SettingMgr.GetAcrDir(authorName);
                                foreach (var job in entry.TargetJobs)
                                {
                                    ACRLifecycle.RegisterExternal((uint)job, () =>
                                    {
                                        // 每次工厂调用时创建新实例（支持 ALC 内多职业）
                                        return (IRotationEntry)Activator.CreateInstance(type)!;
                                    }, settingDir);
                                }

                                DService.Instance().Log.Information($"[ACRLoader] {authorName}/{Path.GetFileName(dllPath)} → {type.Name} [{string.Join(",", entry.TargetJobs)}]");
                                found = true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    DService.Instance().Log.Error($"[ACRLoader] 加载失败 {dllPath}: {ex.Message}");
                }
            }

            if (found)
            {
                _authorContexts[authorName] = alc;
                ACRLifecycle.RegisterContext(alc);
            }
            else
            {
                alc.Unload();
            }
        }
    }

    /// <summary>程序集解析</summary>
    private static Assembly? ResolveAssembly(AssemblyLoadContext ctx, AssemblyName name, string authorDir)
    {
        DService.Instance().Log.Debug($"[ACRLoader] 解析程序集: {name.Name} v{name.Version} (请求者:{ctx.Name})");

        if (_hostPrefixes.Any(p => name.Name?.StartsWith(p) == true))
        {
            // HiAuRo 自身一定已加载（我们正在执行的代码就是它）
            if (name.Name == "HiAuRo")
            {
                var selfAsm = typeof(ACRLoader).Assembly;
                DService.Instance().Log.Debug($"[ACRLoader]   → 返回自身 HiAuRo 程序集: {selfAsm.GetName().Name} v{selfAsm.GetName().Version}");
                return selfAsm;
            }

            // 其它宿主程序集（OmenTools, Dalamud 等）：查找 Default ALC 中已加载的版本
            foreach (var asm in AssemblyLoadContext.Default.Assemblies)
            {
                if (asm.GetName().Name == name.Name)
                {
                    DService.Instance().Log.Debug($"[ACRLoader]   → 找到已加载: {asm.GetName().Name} v{asm.GetName().Version}");
                    return asm;
                }
            }

            // Fallback: 让 Default ALC 自行解析
            try
            {
                return AssemblyLoadContext.Default.LoadFromAssemblyName(name);
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Error($"[ACRLoader] LoadFromAssemblyName 失败: {name.Name} — {ex.Message}");
            }
        }

        var path = Path.Combine(authorDir, name.Name + ".dll");
        if (File.Exists(path))
            return ctx.LoadFromAssemblyPath(path);

        return null;
    }

    /// <summary>卸载所有外部 ACR</summary>
    public static void UnloadAll()
    {
        foreach (var (name, alc) in _authorContexts)
        {
            try { alc.Unload(); }
            catch (Exception ex) { DService.Instance().Log.Error($"[ACRLoader] 卸载 ALC 失败: {name}: {ex.Message}"); }
        }
        _authorContexts.Clear();
    }
}
