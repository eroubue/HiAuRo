using System.Diagnostics;
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

    public static bool Loaded { get; private set; }

    /// <summary>检查更新，下载并加载最新 DLL（异步，不阻塞启动）</summary>
    public static async Task CheckAndUpdateAsync()
    {
        try
        {
            var latestTag = await FetchLatestReleaseTag();
            if (latestTag == null) return;

            var localDll = Path.Combine(StoreDir, DllName);
            var localVersion = GetLocalVersion(localDll);

            if (latestTag == localVersion && File.Exists(localDll))
            {
                LoadDll(localDll);
                return;
            }

            var downloaded = await DownloadRelease(latestTag, localDll);
            if (downloaded)
            {
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

    private static string? GetLocalVersion(string dllPath)
    {
        if (!File.Exists(dllPath)) return null;
        try
        {
            var info = FileVersionInfo.GetVersionInfo(dllPath);
            return info.FileVersion is { Length: > 0 } v ? $"v{v}" : null;
        }
        catch { return null; }
    }

    private static void LoadDll(string dllPath)
    {
        if (!File.Exists(dllPath)) return;

        _alc?.Unload();
        _alc = new AssemblyLoadContext("HiAuRo.Helper", isCollectible: true);
        _alc.Resolving += (ctx, name) =>
        {
            if (name.Name?.StartsWith("HiAuRo.Helper") == true)
                return ctx.LoadFromAssemblyPath(dllPath);
            return null;
        };

        _helperAsm = _alc.LoadFromAssemblyPath(dllPath);
        Loaded = true;

        InitializeHelperRuntime();
    }

    /// <summary>通过反射调用 HelperRuntime.Initialize，注入上下文实现</summary>
    private static void InitializeHelperRuntime()
    {
        if (_helperAsm == null) return;

        try
        {
            _contextImpl = new HiAuRoContextImpl();
            var proxy = HelperDispatchProxy.CreateProxy(_contextImpl, _helperAsm);

            var runtimeType = _helperAsm.GetType("HiAuRo.Helper.HelperRuntime")!;
            var initMethod = runtimeType.GetMethod("Initialize",
                BindingFlags.NonPublic | BindingFlags.Static)!;
            initMethod.Invoke(null, [proxy]);

            DService.Instance().Log.Information("[HelperUpdater] HelperRuntime 上下文已注入");
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[HelperUpdater] InitializeHelperRuntime 失败: {ex.Message}");
        }
    }

    private static void TryLoadLocal()
    {
        var localDll = Path.Combine(StoreDir, DllName);
        if (File.Exists(localDll))
            LoadDll(localDll);
    }
}
