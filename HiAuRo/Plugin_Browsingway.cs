using Browsingway;
using Dalamud.Plugin;

namespace HiAuRo;

partial class Plugin
{
    private BrowserHost? _browserHost;
    public static BrowserHost? BrowserHost => Instance._browserHost;

    private void BrowsingwayPluginInit(IDalamudPluginInterface pluginInterface)
    {
        try
        {
            _browserHost = new BrowserHost(pluginInterface);
            DService.Instance().Log.Information("[Renderer] BrowserHost 已初始化");
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[Renderer] BrowserHost 初始化失败: {ex}");
        }
    }

    private void BrowsingwayDispose()
    {
        _browserHost?.Dispose();
        _browserHost = null;
    }
}
