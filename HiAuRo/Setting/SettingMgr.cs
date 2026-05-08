using HiAuRo.Infrastructure;

namespace HiAuRo.Setting;

/// <summary>
/// 全局 + 职业设置管理器
/// </summary>
public static class SettingMgr
{
    private static string? _configDir;

    /// <summary>配置目录</summary>
    public static string ConfigDirectory => _configDir ?? string.Empty;

    public static void Init(string configDir)
    {
        _configDir = configDir;
        if (!Directory.Exists(configDir))
            Directory.CreateDirectory(configDir);
    }

    #region 公共方法

    /// <summary>获取全局设置</summary>
    public static T GetSetting<T>() where T : class, new()
    {
        var path = GetGlobalSettingPath<T>();
        return Load<T>(path) ?? new T();
    }

    /// <summary>获取职业设置</summary>
    public static T GetJobSetting<T>(string jobName) where T : class, new()
    {
        var path = GetJobSettingPath<T>(jobName);
        return Load<T>(path) ?? new T();
    }

    /// <summary>保存全局设置</summary>
    public static void SaveSetting<T>(T setting) where T : class
    {
        var path = GetGlobalSettingPath<T>();
        Save(path, setting);
    }

    /// <summary>保存职业设置</summary>
    public static void SaveJobSetting<T>(string jobName, T setting) where T : class
    {
        var path = GetJobSettingPath<T>(jobName);
        Save(path, setting);
    }

    #endregion

    #region ACR 设置

    /// <summary>获取 ACR 作者配置目录</summary>
    public static string GetAcrDir(string author) =>
        Path.Combine(ConfigDirectory, "ACR", author);

    /// <summary>获取 ACR 职业设置</summary>
    public static T GetAcrSetting<T>(string author, ACR.Jobs job) where T : class, new()
    {
        var dir = GetAcrDir(author);
        var path = Path.Combine(dir, $"{job}.json");
        return Load<T>(path) ?? new T();
    }

    /// <summary>保存 ACR 职业设置</summary>
    public static void SaveAcrSetting<T>(string author, ACR.Jobs job, T setting) where T : class
    {
        var dir = GetAcrDir(author);
        var path = Path.Combine(dir, $"{job}.json");
        Save(path, setting);
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
