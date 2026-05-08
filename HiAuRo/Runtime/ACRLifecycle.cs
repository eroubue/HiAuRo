using System.Runtime.Loader;
using HiAuRo.ACR;

namespace HiAuRo.Runtime;

/// <summary>
/// ACR 生命周期管理 —— 职业切换自动加载
/// </summary>
public static class ACRLifecycle
{
    public static AIRunner Runner { get; } = new();
    public static IRotationEntry? CurrentEntry { get; private set; }
    public static string CurrentAcrName => CurrentEntry?.AuthorName ?? "无ACR";

    /// <summary>外部 ACR: JobId → (Factory, SettingDir)</summary>
    private static readonly Dictionary<uint, (Func<IRotationEntry> Factory, string SettingDir)> _acrRegistry = [];
    /// <summary>外部 ALC 引用（用于 Reload 卸载）</summary>
    private static readonly List<AssemblyLoadContext> _externalAlcs = [];

    private static uint _lastJob;
    private static bool _resetCalled;

    /// <summary>注册外部 ACR</summary>
    public static void RegisterExternal(uint jobId, Func<IRotationEntry> factory, string settingDir)
    {
        _acrRegistry[jobId] = (factory, settingDir);
    }

    /// <summary>注册外部 ALC</summary>
    public static void RegisterContext(AssemblyLoadContext alc)
    {
        _externalAlcs.Add(alc);
    }

    /// <summary>初始化</summary>
    public static void Init(string settingRoot) { }

    /// <summary>每帧由 RuntimeCore 调用</summary>
    public static void Update()
    {
        CheckJobSwitch();

        var state = CombatContext.CurrentState;
        if (state == CombatContext.State.Idle || state == CombatContext.State.Zoning)
        {
            _resetCalled = false;
            Runner.ProcessSpellQueue(false); // 热键在所有状态都应消费
            return;
        }

        if (state == CombatContext.State.OutOfCombat)
        {
            if (!_resetCalled) { Runner.Reset(); _resetCalled = true; }
            Runner.ProcessSpellQueue(false); // 非战斗也能消费热键队列
            return;
        }

        _resetCalled = false;
        Runner.Update();
    }

    private static void CheckJobSwitch()
    {
        if (!HiAuRo.Data.Data.IsReady) return;

        var currentJob = Data.Me.ClassJob;
        if (currentJob == _lastJob && currentJob != 0) return;
        _lastJob = currentJob;

        if (_acrRegistry.TryGetValue(currentJob, out var reg))
        {
            LoadRotation(reg.Factory(), reg.SettingDir);
        }
        else
        {
            UnloadRotation();
        }

        _ = Plugin.Instance._uiBridge.SendAsync(new
        {
            type = "status",
            data = new
            {
                job = CurrentAcrName,
                enabled = RuntimeCore.IsRunning,
                paused = ACR.MainControlHelper.IsPaused,
                hotkeys = ACR.HotkeyHelper.GetAll().Select(r => new
                {
                    id = r.Id,
                    label = r.Label,
                    available = r.Check() >= 0,
                    binding = ACR.HotkeyHelper.GetBinding(r.Id)
                }).ToList(),
                qts = ACR.QTHelper.GetAll().Select(q => new
                {
                    id = q.Id,
                    label = q.Label,
                    value = q.Value,
                    tooltip = q.Tooltip,
                    color = q.Color,
                    binding = q.HotkeyBinding
                }).ToList()
            }
        });
    }

    /// <summary>热重载</summary>
    public static void Reload()
    {
        UnloadRotation();

        _acrRegistry.Clear();

        // 卸载所有外部 ALC
        foreach (var alc in _externalAlcs)
        {
            try { alc.Unload(); }
            catch (Exception ex) { DService.Instance().Log.Error($"[ACR] 卸载 ALC 失败: {ex.Message}"); }
        }
        _externalAlcs.Clear();

        // 重新扫描
        ACRLoader.UnloadAll();
        var pluginDir = Plugin.Instance.PluginInterface.AssemblyLocation.Directory?.FullName ?? ".";
        ACRLoader.LoadAll(pluginDir);

        _lastJob = 0;
        CheckJobSwitch();
    }

    private static void LoadRotation(IRotationEntry entry, string settingFolder)
    {
        UnloadRotation();
        Runner.Load(entry, settingFolder);
        CurrentEntry = entry;

        // 注册 ACR 自定义触发类型
        if (Runner.CurrentRotation != null)
            HiAuRo.Execution.ExecutionJsonLoader.RegisterFromRotation(Runner.CurrentRotation);

        // 恢复 UI 设置
        ACR.UiSettingsStore.Init(settingFolder);
        var settings = ACR.UiSettingsStore.Load();

        // 恢复热键绑定
        foreach (var (id, key) in settings.HkBindings)
            ACR.HotkeyHelper.SetBinding(id, key);

        // 收集 ACR 作者声明的 UI 控件 → 推送到 Web 前端动态渲染
        var ui = entry.GetRotationUI();
        if (ui != null)
        {
            var builder = new HiAuRo.UI.UiBuilderImpl();
            ui.RegisterControls(builder);
            var controls = builder.GetControls();
            _ = Plugin.Instance._uiBridge.SendAsync(new
            {
                type = "controls",
                data = controls
            });
        }

        // 推送 UI 设置
        _ = Plugin.Instance._uiBridge.SendAsync(new
        {
            type = "uiSettings",
            data = new
            {
                qtCols = settings.QtCols,
                qtBtnW = settings.QtBtnW,
                qtVisible = settings.QtVisible,
                hkCols = settings.HkCols,
                hkBtnSize = settings.HkBtnSize,
                hkVisible = settings.HkVisible,
                hkBindings = settings.HkBindings
            }
        });

        // 推送完整状态（qt + hotkey 数据）
        _ = Plugin.Instance._uiBridge.SendAsync(new
        {
            type = "status",
            data = new
            {
                job = CurrentAcrName,
                enabled = RuntimeCore.IsRunning,
                paused = ACR.MainControlHelper.IsPaused,
                hotkeys = ACR.HotkeyHelper.GetAll().Select(r => new
                {
                    id = r.Id,
                    label = r.Label,
                    available = r.Check() >= 0,
                    binding = ACR.HotkeyHelper.GetBinding(r.Id)
                }).ToList(),
                qts = ACR.QTHelper.GetAll().Select(q => new
                {
                    id = q.Id,
                    label = q.Label,
                    value = q.Value,
                    tooltip = q.Tooltip,
                    color = q.Color,
                    binding = q.HotkeyBinding
                }).ToList()
            }
        });
    }

    private static void UnloadRotation()
    {
        Runner.Unload();
        CurrentEntry = null;
        ACR.HotkeyHelper.Clear();
        ACR.QTHelper.Clear();
        ACR.MainControlHelper.Reset();
    }
}
