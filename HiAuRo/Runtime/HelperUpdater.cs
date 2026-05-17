using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace HiAuRo.Runtime;

/// <summary>
/// Helper DLL 自动更新 — 从 GitHub Release 拉取最新 HiAuRo.Helper.dll
/// </summary>
public static class HelperUpdater
{
    private const string RepoOwner = "denghaoxuan991876906";
    private const string RepoName = "HiAuRo.Helper";
    private const string DllName = "HiAuRo.Helper.dll";
    private const string VersionFileName = "version.txt";

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
        DefaultRequestHeaders = { { "User-Agent", "HiAuRo" } }
    };

    private static string StoreDir => Path.Combine(
        DService.Instance().PI.ConfigDirectory.FullName, "Helper");

    private static AssemblyLoadContext? _alc;
    private static Assembly? _helperAsm;
    private static HiAuRoContextImpl? _contextImpl;
    private static byte[]? _cachedDllBytes;

    /// <summary>Helper DLL 是否已加载</summary>
    public static bool Loaded { get; private set; }

    /// <summary>HelperUpdater 已加载并注入 _ctx 的 HiAuRo.Helper 程序集（供 ACRLoader 共享，避免 ALC 隔离）</summary>
    public static Assembly? HelperAssembly => _helperAsm;

    /// <summary>尝试从本地缓存同步加载 Helper DLL（供 ACRLoader 时序竞争回退）</summary>
    public static bool TryLoadLocalSync()
    {
        if (_helperAsm != null) return true;

        var localDll = Path.Combine(StoreDir, DllName);
        if (!File.Exists(localDll)) return false;

        try
        {
            LoadDll(localDll);
            DService.Instance().Log.Information($"[HelperUpdater] 从本地缓存同步加载: {localDll}");
            return true;
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[HelperUpdater] 本地缓存加载失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>检查更新，下载并加载最新 DLL（异步，不阻塞启动）</summary>
    public static async Task CheckAndUpdateAsync()
    {
        try
        {
            var latestTag = await FetchLatestReleaseTag();
            if (latestTag == null) return;

            var localDll = Path.Combine(StoreDir, DllName);
            var localTag = GetLocalTag();

            if (latestTag == localTag && File.Exists(localDll))
            {
                LoadDll(localDll);
                return;
            }

            var downloaded = await DownloadRelease(latestTag, localDll);
            if (downloaded)
            {
                WriteLocalTag(latestTag);
                DService.Instance().Log.Information($"[HelperUpdater] 已更新 {RepoName} → {latestTag}");
                LoadDll(localDll);
            }
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[HelperUpdater] 更新失败: {ex.Message}");
            TryLoadLocal();
        }
    }

    private static async Task<string?> FetchLatestReleaseTag()
    {
        var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
        using var resp = await _http.GetAsync(url);

        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return json.TryGetProperty("tag_name", out var tag) ? tag.GetString() : null;
    }

    private static async Task<bool> DownloadRelease(string tag, string destPath)
    {
        var url = $"https://github.com/{RepoOwner}/{RepoName}/releases/download/{tag}/{DllName}";
        using var resp = await _http.GetAsync(url);

        if (!resp.IsSuccessStatusCode) return false;

        Directory.CreateDirectory(StoreDir);
        await using var fs = File.Create(destPath);
        await resp.Content.CopyToAsync(fs);
        return true;
    }

    private static string? GetLocalTag()
    {
        var path = Path.Combine(StoreDir, VersionFileName);
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch { return null; }
    }

    private static void WriteLocalTag(string tag)
    {
        try
        {
            Directory.CreateDirectory(StoreDir);
            File.WriteAllText(Path.Combine(StoreDir, VersionFileName), tag);
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[HelperUpdater] 写版本文件失败: {ex.Message}");
        }
    }

        /// <summary>解析 Helper ALC 中宿主程序集依赖（Dalamud、OmenTools、HiAuRo 等）</summary>
        private static Assembly? ResolveHelperDependency(AssemblyLoadContext ctx, AssemblyName name)
        {
            var hostPrefixes = new[] { "HiAuRo", "OmenTools", "Dalamud", "FFXIVClientStructs",
                "Lumina", "ImGuiNET", "TerraFX", "System.", "Microsoft." };

            if (!hostPrefixes.Any(p => name.Name?.StartsWith(p) == true))
                return null;

            // 先在 Default ALC 中查找（System.*、Microsoft.*）
            foreach (var asm in AssemblyLoadContext.Default.Assemblies)
            {
                if (asm.GetName().Name == name.Name)
                    return asm;
            }

            // 再在宿主 ALC（HiAuRo 所在）中查找
            var hostAlc = AssemblyLoadContext.GetLoadContext(typeof(HelperUpdater).Assembly);
            if (hostAlc != null)
            {
                foreach (var asm in hostAlc.Assemblies)
                {
                    if (asm.GetName().Name == name.Name)
                        return asm;
                }

                try
                {
                    return hostAlc.LoadFromAssemblyName(name);
                }
                catch { }
            }

            return null;
        }

        private static void LoadDll(string dllPath)
    {
        if (!File.Exists(dllPath)) return;

        // 读入内存后立即释放文件句柄，避免下次更新时 DLL 被占用
        _cachedDllBytes = File.ReadAllBytes(dllPath);

        _alc?.Unload();
        _alc = new AssemblyLoadContext("HiAuRo.Helper", isCollectible: true);
        _alc.Resolving += ResolveHelperDependency;

        using var ms = new MemoryStream(_cachedDllBytes, writable: false);
        _helperAsm = _alc.LoadFromStream(ms);
        Loaded = true;

        InitializeHelperRuntime();
    }

    /// <summary>通过 Reflection.Emit 生成 IHelperContext 实现 + 反射调用 HelperRuntime.Initialize</summary>
    private static void InitializeHelperRuntime()
    {
        if (_helperAsm == null || _alc == null) return;

        try
        {
            _contextImpl = new HiAuRoContextImpl();
            var proxy = EmitProxy.Create(_contextImpl, _helperAsm, _alc);

            var runtimeType = _helperAsm.GetType("HiAuRo.Helper.HelperRuntime")!;
            var initMethod = runtimeType.GetMethod("Initialize",
                BindingFlags.NonPublic | BindingFlags.Static)!;
            initMethod.Invoke(null, [proxy]);

            DService.Instance().Log.Information("[HelperUpdater] HelperRuntime 上下文已注入");
        }
        catch (Exception ex)
        {
            var msg = ex is System.Reflection.TargetInvocationException tie && tie.InnerException != null
                ? tie.InnerException.ToString()
                : ex.ToString();
            DService.Instance().Log.Warning($"[HelperUpdater] InitializeHelperRuntime 失败: {msg}");
        }
    }

    private static void TryLoadLocal()
    {
        var localDll = Path.Combine(StoreDir, DllName);
        if (File.Exists(localDll))
            LoadDll(localDll);
    }
}
