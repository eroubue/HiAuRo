using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using HiAuRo.Authoring;
using HiAuRo.Command;
using HiAuRo.Execution;
using HiAuRo.Execution.Events;
using HiAuRo.Infrastructure;
using HiAuRo.Runtime;
using HiAuRo.Setting;
using HiAuRo.UI;
using HiAuRo.Decision;
using OmenTools;

namespace HiAuRo;

public partial class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly PluginConfig _config;
    internal readonly WebUiBridge _uiBridge;
    internal IDalamudPluginInterface PluginInterface => _pluginInterface;
    private readonly WebUiServer _uiServer;
    private readonly MainWindow _mainWindow;
    private readonly WindowSystem _windowSystem;

    public static Plugin Instance { get; private set; } = null!;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        Instance = this;
        _pluginInterface = pluginInterface;

        DService.Init(pluginInterface);
        _config = LoadConfig();
        BrowsingwayPluginInit(pluginInterface);

        _ = HelperUpdater.CheckAndUpdateAsync();

        var webRoot = Path.Combine(_pluginInterface.ConfigDirectory.FullName, "web");
        var sourceWebRoot = Path.Combine(_pluginInterface.AssemblyLocation.Directory?.FullName ?? ".", "UI", "web");

        // 始终覆盖更新 web 前端文件
        if (Directory.Exists(sourceWebRoot))
            CopyDirectory(sourceWebRoot, webRoot);

        SettingMgr.Init(_pluginInterface.ConfigDirectory.FullName);
        CommandMgr.Init();
        EventSystem.Init();
        GameEventHook.Instance.Init();
        ExecutionAxis.Instance.Init();
        RuntimeCore.Start();
        DecisionEngine.Instance.Init();
        AssistAxis.Instance.Init();
        ModeSwitch.TryAutoSwitchToExecutionAxis();
        CombatContext.StateChanged += OnCombatStateChanged;

        _uiBridge = new WebUiBridge();
        RegisterUiHandlers(_uiBridge);
        AuthoringServer.Instance.Register(_uiBridge);
        _uiServer = new WebUiServer(webRoot, _uiBridge);
        _uiServer.Start();

        ACR.HotkeyHelper.OnExecuted += OnHotkeyExecuted;
        ACR.QTHelper.OnChanged += OnQtChanged;

        _windowSystem = new WindowSystem("HiAuRo");
        _mainWindow = new MainWindow(_config, () => _pluginInterface.SavePluginConfig(_config));
        _windowSystem.AddWindow(_mainWindow);
        _pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        _pluginInterface.UiBuilder.OpenMainUi += () => _mainWindow.IsOpen = !_mainWindow.IsOpen;
        _pluginInterface.UiBuilder.OpenConfigUi += () => _mainWindow.IsOpen = !_mainWindow.IsOpen;

        // 加载外部 ACR
        ACRLifecycle.Init(_pluginInterface.ConfigDirectory.FullName);
        ACRLoader.LoadAll(_pluginInterface.AssemblyLocation.Directory?.FullName ?? ".");

        try
        {
            var merged = new TriggerCatalog();
            TriggerCatalogBuilder.MergeInto(merged,
                TriggerCatalogBuilder.BuildFromAssembly(typeof(Execution.Triggers.Cond.TriggerCond_敌人读条).Assembly, "builtin"));

            foreach (var acrAsm in ACRLoader.LoadedAcrAssemblies)
            {
                var acrCatalog = TriggerCatalogBuilder.BuildFromAssembly(acrAsm, "acr");
                TriggerCatalogBuilder.MergeInto(merged, acrCatalog);
            }

            var catalogPath = Path.Combine(_pluginInterface.ConfigDirectory.FullName, "trigger-catalog.json");
            var catalogJson = System.Text.Json.JsonSerializer.Serialize(merged,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
            File.WriteAllText(catalogPath, catalogJson);
            DService.Instance().Log.Information($"[TriggerCatalog] 已生成 ({merged.Conditions.Count}C {merged.Actions.Count}A {merged.Scripts.Count}S)");
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[TriggerCatalog] 生成失败: {ex.Message}");
        }

        DService.Instance().Chat.Print("[HiAuRo] /hi on|off|toggle|status|panel|reload  悬浮窗: localhost:5678/jobview.html");
        DService.Instance().Log.Information($"[Lifecycle] HiAuRo 宿主已加载。版本: {_config.LastSeenPluginVersion}");
    }

    private static void OnCombatStateChanged(CombatContext.State oldState, CombatContext.State newState)
    {
        if (newState is CombatContext.State.OutOfCombat or CombatContext.State.InCombat)
            ModeSwitch.TryAutoSwitchToExecutionAxis();
    }

    internal async Task UploadCatalogAsync()
    {
        var config = _config;
        if (string.IsNullOrWhiteSpace(config.GitHubToken))
        {
            DService.Instance().Chat.Print("[HiAuRo] 请先在设置中配置 GitHubToken");
            return;
        }

        var catalogPath = Path.Combine(_pluginInterface.ConfigDirectory.FullName, "trigger-catalog.json");
        if (!File.Exists(catalogPath))
        {
            DService.Instance().Chat.Print("[HiAuRo] 目录未生成");
            return;
        }

        var json = await File.ReadAllTextAsync(catalogPath);
        var result = await Events.CatalogSync.UploadToGitHubAsync(
            json, config.CatalogRepo, config.CatalogBranch, config.GitHubToken, onlyCloudSync: true);

        DService.Instance().Chat.Print(result.Success
            ? $"[HiAuRo] 目录已上传 → {result.CommitUrl}"
            : $"[HiAuRo] 上传失败: {result.Message}");
    }

    public void Dispose()
    {
        CombatContext.StateChanged -= OnCombatStateChanged;
        ACR.HotkeyHelper.OnExecuted -= OnHotkeyExecuted;
        ACR.QTHelper.OnChanged -= OnQtChanged;

        Instance = null!;

        RuntimeCore.Shutdown();
        CombatContext.Reset();
        ExecutionAxis.Instance.Shutdown();
        AssistAxis.Instance.Shutdown();
        GameEventHook.Instance.Shutdown();
        EventSystem.Shutdown();
        CommandMgr.Shutdown();

        _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
        _windowSystem.RemoveAllWindows();

        _uiServer.Stop();
        _uiBridge.Dispose();
        BrowsingwayDispose();
        DService.Instance().Log.Information("[Lifecycle] HiAuRo 宿主已释放。");
        DService.Uninit();
    }

    private static void RegisterUiHandlers(WebUiBridge bridge)
    {
        bridge.On("toggleACR", _d =>
        {
            if (RuntimeCore.IsRunning) RuntimeCore.Stop();
            else RuntimeCore.Start();
            _ = SendStatusState();
        });

        bridge.On("pause", _d =>
        {
            HiAuRo.ACR.MainControlHelper.TogglePause();
            _ = SendPauseState();
        });

        bridge.On("saveACR", _ =>
        {
            HiAuRo.ACR.MainControlHelper.Save();
        });

        bridge.On("hotkey", data =>
        {
            if (data == null) return;
            var id = data.Value.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (id != null) HiAuRo.ACR.HotkeyHelper.ExecuteById(id);
        });

        bridge.On("qttoggle", data =>
        {
            if (data == null) return;
            var id = data.Value.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (id != null) HiAuRo.ACR.QTHelper.Toggle(id);
        });

        bridge.On("setHkBinding", data =>
        {
            if (data == null) return;
            var id = data.Value.TryGetProperty("id", out var i1) ? i1.GetString() : null;
            var key = data.Value.TryGetProperty("key", out var i2) ? i2.GetString() : null;
            if (id != null && key != null) HiAuRo.ACR.HotkeyHelper.SetBinding(id, key);
        });

        bridge.On("saveUiSettings", data =>
        {
            if (data == null) { DService.Instance().Log.Debug("[UI] saveUiSettings: data is null"); return; }
            var json = data.Value.GetRawText();
            DService.Instance().Log.Debug($"[UI] saveUiSettings 收到: {json.Length} 字节");
            try
            {
                var s = System.Text.Json.JsonSerializer.Deserialize<HiAuRo.ACR.UiSettings>(json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (s != null)
                {
                    DService.Instance().Log.Debug($"[UI] 反序列化成功 qtCols={s.QtCols} hkCols={s.HkCols}");
                    HiAuRo.ACR.UiSettingsStore.Save(s);
                    _ = SendUiSettings(s);
                }
                else { DService.Instance().Log.Debug("[UI] 反序列化结果为 null"); }
            }
            catch (Exception ex) { DService.Instance().Log.Error($"[UI] saveUiSettings 异常: {ex}"); }
        });

        // 接收前端调试日志
        bridge.On("log", data =>
        {
            if (data == null) return;
            var msg = data.Value.TryGetProperty("msg", out var m) ? m.GetString() : "";
            var level = data.Value.TryGetProperty("level", out var l) ? l.GetString() : "info";
            var src = data.Value.TryGetProperty("src", out var s) ? s.GetString() : "web";
            var text = $"[Web:{src}] {msg}";
            switch (level)
            {
                case "error": DService.Instance().Log.Error(text); break;
                case "warn": DService.Instance().Log.Warning(text); break;
                default: DService.Instance().Log.Information(text); break;
            }
        });
    }

    private static async Task SendStatusState()
    {
        await Instance._uiBridge.SendAsync(new
        {
            type = "acrState",
            data = new { enabled = RuntimeCore.IsRunning }
        });
    }

    private static async Task SendUiSettings(HiAuRo.ACR.UiSettings s)
    {
        await Instance._uiBridge.SendAsync(new
        {
            type = "uiSettings",
            data = new
            {
                qtCols = s.QtCols,
                qtBtnW = s.QtBtnW,
                qtVisible = s.QtVisible,
                hkCols = s.HkCols,
                hkBtnSize = s.HkBtnSize,
                hkVisible = s.HkVisible,
                hkBindings = s.HkBindings
            }
        });
    }

    private static async Task SendPauseState()
    {
        await Instance._uiBridge.SendAsync(new
        {
            type = "pauseChanged",
            data = new { paused = HiAuRo.ACR.MainControlHelper.IsPaused }
        });
    }

    private static void OnHotkeyExecuted(string id, string label)
    {
        _ = Instance._uiBridge.SendAsync(new
        {
            type = "hotkeyExecuted",
            data = new { id, label }
        });
    }

    private static void OnQtChanged(string id, bool value)
    {
        _ = Instance._uiBridge.SendAsync(new
        {
            type = "qtChanged",
            data = new { id, value }
        });
    }

    #region 配置

    private PluginConfig LoadConfig()
    {
        var config = _pluginInterface.GetPluginConfig() as PluginConfig ?? new PluginConfig();
        PluginConfig.Instance = config;

        _pluginInterface.SavePluginConfig(config);

        DService.Instance().Log.Information(
            $"[Config] SchemaVersion={config.Version}, LoadCount={config.LoadCount}, DebugEnabled={config.DebugEnabled}");
        return config;
    }

    #endregion

    #region 日志

    public void LogDebug(string message)
    {
        if (_config.DebugEnabled)
            DService.Instance().Log.Debug($"[Debug] {message}");
    }

    public void LogDebug(string messageTemplate, params object[] args)
    {
        if (_config.DebugEnabled)
            DService.Instance().Log.Debug($"[Debug] {string.Format(messageTemplate, args)}");
    }

    #endregion

    #region 工具

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar);
            var dest = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, true);
        }
    }

    #endregion
}
