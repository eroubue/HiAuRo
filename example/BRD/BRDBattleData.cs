namespace HiAuRo.Jobs.BRD;

/// <summary>
/// 诗人战斗数据缓存
/// </summary>
public static class BRDBattleData
{
    public static string SettingFolder { get; private set; } = string.Empty;

    public static void Init(string settingFolder)
    {
        SettingFolder = settingFolder;
    }

    public static void Reset()
    {
        SettingFolder = string.Empty;
    }
}
