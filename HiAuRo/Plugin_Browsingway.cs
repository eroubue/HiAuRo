using Browsingway;
using Dalamud.Plugin;

namespace HiAuRo;

partial class Plugin
{
    internal BrowserHost? _browserHost;
    public static BrowserHost? BrowserHost => Instance._browserHost;
}
