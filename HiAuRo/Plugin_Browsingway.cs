using Browsingway;
using Dalamud.Plugin;

namespace HiAuRo;

partial class Plugin
{
    internal BrowserHost? _browserHost;
    public static BrowserHost? BrowserHost => Instance._browserHost;

    private void BrowsingwayPluginInit(IDalamudPluginInterface pluginInterface)
    {
        try
        {
            var pluginDir = pluginInterface.AssemblyLocation.DirectoryName ?? "?";
            DService.Instance().Log.Information($"[BW] BrowserHost 初始化开始 (pluginDir={pluginDir})");
            _browserHost = new BrowserHost(pluginInterface);
            DService.Instance().Log.Information("[BW] BrowserHost 初始化完成 (renderer进程将异步启动)");
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[BW] BrowserHost 初始化失败 (游戏内将无悬浮窗): {ex}");
        }
    }

    private void BrowsingwayDispose()
    {
        DService.Instance().Log.Information("[BW] BrowserHost 释放中...");
        _browserHost?.Dispose();
        _browserHost = null;
        DService.Instance().Log.Information("[BW] BrowserHost 已释放");
    }
}
