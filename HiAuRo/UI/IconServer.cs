namespace HiAuRo.UI;

public static class IconServer
{
    private const string IconBaseUrl = "https://cafemaker.wakingsands.com/i";

    public static string? GetIconUrl(uint iconId)
    {
        if (iconId == 0) return null;
        var baseDir = iconId / 1000 * 1000;
        return $"{IconBaseUrl}/{baseDir:D6}/{iconId:D6}_hr1.png";
    }
}
