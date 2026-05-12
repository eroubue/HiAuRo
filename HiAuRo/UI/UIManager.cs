using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using HiAuRo.Infrastructure;
using HiAuRo.ImGuiLib;

namespace HiAuRo.UI;

/// <summary>
/// UI 模式管理器 —— 集中管理 WebUI (CEF) 和 ImGui 两种悬浮窗模式
/// </summary>
internal class UIManager : IDisposable
{
    private readonly PluginConfig _config;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly WindowSystem _windowSystem;
    private readonly Action _saveConfig;
    private readonly string _webRoot;

    private WebUiServer? _uiServer;
    private WebUiBridge? _uiBridge;

    private OverlayStatusBar? _overlayStatusBar;
    private OverlayQtPanel? _overlayQtPanel;
    private OverlayHotkeyPanel? _overlayHotkeyPanel;
    private DemoWindow? _demoWindow;

    /// <summary>当前是否为 WebUI 模式（CEF 已启用且当前选中 WebUI）</summary>
    public bool IsWebUI => _config.UIMode == UIMode.WebUI && !_config.DisableCEF;

    /// <summary>WebSocket 桥接，低配模式下为 null</summary>
    public WebUiBridge? Bridge => _uiBridge;

    public UIManager(PluginConfig config, IDalamudPluginInterface pluginInterface,
                     WindowSystem windowSystem, Action saveConfig, string webRoot)
    {
        _config = config;
        _pluginInterface = pluginInterface;
        _windowSystem = windowSystem;
        _saveConfig = saveConfig;
        _webRoot = webRoot;
    }

    /// <summary>根据当前配置初始化 UI 资源</summary>
    public void Init()
    {
        if (!_config.DisableCEF)
        {
            // 创建 CEF BrowserHost（渲染器进程异步启动）
            var browserHost = new Browsingway.BrowserHost(_pluginInterface);
            Plugin.Instance._browserHost = browserHost;

            // 创建 WebSocket 桥接 + HTTP 服务器
            _uiBridge = new WebUiBridge();
            _uiServer = new WebUiServer(_webRoot, _uiBridge);
            _uiServer.Start();

            // ImGui 模式下隐藏 CEF overlay
            if (_config.UIMode == UIMode.ImGui)
                browserHost.OverlaysVisible = false;
        }

        // ImGui 模式下创建 ImGui overlay 窗口
        if (_config.UIMode == UIMode.ImGui)
            CreateImGuiOverlays();
    }

    /// <summary>动态切换 UI 模式</summary>
    public void SwitchTo(UIMode mode)
    {
        if (mode == _config.UIMode) return;
        if (mode == UIMode.WebUI && _config.DisableCEF) return;

        _config.UIMode = mode;

        if (mode == UIMode.WebUI)
        {
            RemoveImGuiOverlays();
            if (Plugin.BrowserHost != null)
                Plugin.BrowserHost.OverlaysVisible = true;
        }
        else
        {
            if (Plugin.BrowserHost != null)
                Plugin.BrowserHost.OverlaysVisible = false;
            CreateImGuiOverlays();
        }

        _saveConfig();
        DService.Instance().Chat.Print($"[HiAuRo] UI 模式已切换为: {(mode == UIMode.WebUI ? "WebUI" : "ImGui")}");
    }

    private void CreateImGuiOverlays()
    {
        if (_overlayStatusBar != null) return; // 防止重复创建

        _demoWindow = new DemoWindow();
        _overlayStatusBar = new OverlayStatusBar(_config, _saveConfig);
        _overlayQtPanel = new OverlayQtPanel(_config, _saveConfig);
        _overlayHotkeyPanel = new OverlayHotkeyPanel(_config, _saveConfig);

        _windowSystem.AddWindow(_demoWindow);
        _windowSystem.AddWindow(_overlayStatusBar);
        _windowSystem.AddWindow(_overlayQtPanel);
        _windowSystem.AddWindow(_overlayHotkeyPanel);
    }

    private void RemoveImGuiOverlays()
    {
        if (_demoWindow != null) { _windowSystem.RemoveWindow(_demoWindow); _demoWindow = null; }
        if (_overlayStatusBar != null) { _windowSystem.RemoveWindow(_overlayStatusBar); _overlayStatusBar = null; }
        if (_overlayQtPanel != null) { _windowSystem.RemoveWindow(_overlayQtPanel); _overlayQtPanel = null; }
        if (_overlayHotkeyPanel != null) { _windowSystem.RemoveWindow(_overlayHotkeyPanel); _overlayHotkeyPanel = null; }
    }

    public void ShowDemoWindow()
    {
        if (_demoWindow != null)
        {
            _demoWindow.IsOpen = true;
        }
        else
        {
            _demoWindow = new DemoWindow();
            _windowSystem.AddWindow(_demoWindow);
            _demoWindow.IsOpen = true;
        }
    }

    public void Dispose()
    {
        RemoveImGuiOverlays();
        _uiServer?.Stop();
        _uiBridge?.Dispose();
        Plugin.Instance._browserHost?.Dispose();
        Plugin.Instance._browserHost = null;
    }
}
