using System.Text.Json;

namespace HiAuRo.ACR;

/// <summary>
/// QT/Hotkey UI 设置持久化存储
/// 每个 ACR 独立存储: {settingFolder}/ui_settings.json
/// </summary>
public static class UiSettingsStore
{
    private static string _settingFolder = string.Empty;

    public static void Init(string settingFolder) => _settingFolder = settingFolder;

    public static void Save(UiSettings settings)
    {
        if (string.IsNullOrEmpty(_settingFolder)) return;
        var path = Path.Combine(_settingFolder, "ui_settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public static UiSettings Load()
    {
        if (string.IsNullOrEmpty(_settingFolder)) return new();
        var path = Path.Combine(_settingFolder, "ui_settings.json");
        if (!File.Exists(path)) return new();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<UiSettings>(json) ?? new();
        }
        catch { return new(); }
    }
}

/// <summary>
/// UI 设置数据模型
/// </summary>
public sealed class UiSettings
{
    public int QtCols { get; set; } = 0; // 0=自动
    public int QtBtnW { get; set; } = 0; // 0=auto
    public Dictionary<string, bool> QtVisible { get; set; } = [];

    public int HkCols { get; set; } = 0; // 0=自动
    public int HkBtnSize { get; set; } = 52;
    public Dictionary<string, bool> HkVisible { get; set; } = [];
    public Dictionary<string, string> HkBindings { get; set; } = [];
}
