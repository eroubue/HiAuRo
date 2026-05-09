using HiAuRo.Infrastructure;

namespace HiAuRo.Setting;

/// <summary>
/// 全局 + 职业 + ACR 设置管理器
/// 目录结构: {configDir}/ACR/{author}/  → {jobId}.json, _global.json
/// </summary>
public static class SettingMgr
{
    private static string? _configDir;

    public static string ConfigDirectory => _configDir ?? string.Empty;

    public static void Init(string configDir)
    {
        _configDir = configDir;
        if (!Directory.Exists(configDir))
            Directory.CreateDirectory(configDir);
    }

    #region 全局 / 职业设置

    public static T GetSetting<T>() where T : class, new()
    {
        var path = GetGlobalSettingPath<T>();
        return Load<T>(path) ?? new T();
    }

    public static T GetJobSetting<T>(string jobName) where T : class, new()
    {
        var path = GetJobSettingPath<T>(jobName);
        return Load<T>(path) ?? new T();
    }

    public static void SaveSetting<T>(T setting) where T : class
    {
        var path = GetGlobalSettingPath<T>();
        Save(path, setting);
    }

    public static void SaveJobSetting<T>(string jobName, T setting) where T : class
    {
        var path = GetJobSettingPath<T>(jobName);
        Save(path, setting);
    }

    #endregion

    #region ACR 设置

    /// <summary>获取 ACR 作者配置目录: {configDir}/ACR/{author}/</summary>
    public static string GetAcrDir(string author) =>
        Path.Combine(ConfigDirectory, "ACR", author);

    /// <summary>获取 ACR 职业设置路径: {configDir}/ACR/{author}/{jobId}.json</summary>
    public static string GetAcrJobPath(string author, uint jobId) =>
        Path.Combine(GetAcrDir(author), $"{jobId}.json");

    /// <summary>获取 ACR 全局设置路径: {configDir}/ACR/{author}/_global.json</summary>
    public static string GetAcrGlobalPath(string author) =>
        Path.Combine(GetAcrDir(author), "_global.json");

    /// <summary>读取 ACR 职业设置</summary>
    public static T GetAcrJobSetting<T>(string author, uint jobId) where T : class, new()
    {
        var path = GetAcrJobPath(author, jobId);
        return Load<T>(path) ?? new T();
    }

    /// <summary>保存 ACR 职业设置</summary>
    public static void SaveAcrJobSetting<T>(string author, uint jobId, T setting) where T : class
    {
        var path = GetAcrJobPath(author, jobId);
        Save(path, setting);
    }

    /// <summary>读取 ACR 全局设置（跨职业共享）</summary>
    public static T GetAcrGlobalSetting<T>(string author) where T : class, new()
    {
        var path = GetAcrGlobalPath(author);
        return Load<T>(path) ?? new T();
    }

    /// <summary>保存 ACR 全局设置（跨职业共享）</summary>
    public static void SaveAcrGlobalSetting<T>(string author, T setting) where T : class
    {
        var path = GetAcrGlobalPath(author);
        Save(path, setting);
    }

    /// <summary>保存 UI 设置（QtVisible / HkBindings / HkVisible）到 ACR 职业路径</summary>
    public static void SaveAcrUiSettings(string author, uint jobId, ACR.UiSettings settings)
    {
        var path = GetAcrJobPath(author, jobId);
        Save(path, settings);
    }

    /// <summary>加载 UI 设置，合并到现有 ACR.UiSettings</summary>
    public static ACR.UiSettings LoadAcrUiSettings(string author, uint jobId)
    {
        var path = GetAcrJobPath(author, jobId);
        var loaded = Load<ACR.UiSettings>(path);
        return loaded ?? new ACR.UiSettings();
    }

    #endregion

    #region 底层持久化

    private static string GetGlobalSettingPath<T>() =>
        Path.Combine(ConfigDirectory, $"{typeof(T).Name}.json");

    private static string GetJobSettingPath<T>(string jobName) =>
        Path.Combine(ConfigDirectory, jobName, $"{typeof(T).Name}.json");

    private static T? Load<T>(string path) where T : class, new()
    {
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return System.Text.Json.JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[Setting] 加载失败: {path}, {ex.Message}");
            return null;
        }
    }

    private static void Save<T>(string path, T setting) where T : class
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = System.Text.Json.JsonSerializer.Serialize(setting, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[Setting] 保存失败: {path}, {ex.Message}");
        }
    }

    #endregion
}
