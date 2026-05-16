using Dalamud.Configuration;

namespace HiAuRo.Infrastructure;

/// <summary>UI 渲染模式</summary>
public enum UIMode { WebUI = 0, ImGui = 1 }

/// <summary>ImGui 主题模式</summary>
public enum ImGuiThemeMode { Light = 0, Dark = 1 }

/// <summary>
/// HiAuRo 主配置对象 —— 走 Dalamud 原生 IPluginConfiguration 序列化
/// </summary>
public sealed class PluginConfig : IPluginConfiguration
{
    /// <summary>全局配置实例</summary>
    public static PluginConfig Instance { get; internal set; } = null!;

    public int Version { get; set; } = 1;
    public bool DebugEnabled { get; set; }
    public string? LastSeenPluginVersion { get; set; }
    public int LoadCount { get; set; }

    /// <summary>技能队列窗口 (ms)</summary>
    public int ActionQueueInMs { get; set; } = 400;

    /// <summary>GCD 内能力技最大次数</summary>
    public int MaxAbilityTimesInGcd { get; set; } = 2;

    /// <summary>AOE 判定的敌人数</summary>
    public int AoeCount { get; set; } = 3;

    /// <summary>攻击距离</summary>
    public float AttackRange { get; set; } = 25f;

    /// <summary>UI 渲染模式</summary>
    public UIMode UIMode { get; set; } = UIMode.WebUI;

    /// <summary>ImGui 主题模式 (亮色/暗色)</summary>
    public ImGuiThemeMode ImGuiThemeMode { get; set; } = ImGuiThemeMode.Light;

    /// <summary>ImGui 模式 — StatusBar overlay X 位置</summary>
    public float OverlayStatusBarX { get; set; } = 100f;

    /// <summary>ImGui 模式 — StatusBar overlay Y 位置</summary>
    public float OverlayStatusBarY { get; set; } = 100f;

    /// <summary>ImGui 模式 — StatusBar 展开状态</summary>
    public bool OverlayStatusBarExpanded { get; set; } = true;

    /// <summary>ImGui 模式 — QT Panel overlay X 位置</summary>
    public float OverlayQtPanelX { get; set; } = 100f;

    /// <summary>ImGui 模式 — QT Panel overlay Y 位置</summary>
    public float OverlayQtPanelY { get; set; } = 300f;

    /// <summary>ImGui 模式 — Hotkey Panel overlay X 位置</summary>
    public float OverlayHotkeyPanelX { get; set; } = 100f;

    /// <summary>ImGui 模式 — Hotkey Panel overlay Y 位置</summary>
    public float OverlayHotkeyPanelY { get; set; } = 420f;

    /// <summary>CEF 悬浮窗设置</summary>
    public OverlayWindowSetting[] Overlays { get; set; } =
    [
        new() { Name = "MainWindow", Url = "http://localhost:5678/main.html", Width = 310, Height = 480 },
        new() { Name = "QtWindow", Url = "http://localhost:5678/qt.html", Width = 320, Height = 80 },
        new() { Name = "HotkeyWindow", Url = "http://localhost:5678/hotkey.html", Width = 320, Height = 100 },
    ];

    /// <summary>GitHub 个人访问令牌（repo 权限，用于上传触发器目录）</summary>
    public string? GitHubToken { get; set; }

    /// <summary>GitHub 仓库路径（owner/repo）</summary>
    public string CatalogRepo { get; set; } = "denghaoxuan991876906/CatalogData";

    /// <summary>GitHub 分支名</summary>
    public string CatalogBranch { get; set; } = "main";

    public FactAxisFlags FactAxis { get; set; } = new();
    public AutoSwitchMode AutoSwitch { get; set; } = AutoSwitchMode.Execution优先;
}

public sealed class OverlayWindowSetting
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public int Width { get; set; } = 640;
    public int Height { get; set; } = 480;
    public float Zoom { get; set; } = 100f;
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; } = true;
}

#region FactAxis

public sealed class FactAxisFlags
{
    public bool Observe = true;          // 时间线观测
    public bool QtControl;               // QT 调控
    public bool TeamMitigation;          // 团队减伤分配
    public bool PersonalMitigation;      // 单人减伤分配
    public bool TeamHealing;             // 团队治疗分配
    public bool ForceExecute;            // 技能强制释放
    public bool MoveTo;                  // NavMesh 移动
    public bool TP;                      // 传送
    public bool Hold;                    // 站位保持
    public MovementMode MovementMode = MovementMode.NavMesh_TP兜底;
}

public enum MovementMode { NavMesh, TP, NavMesh_TP兜底 }

public enum AutoSwitchMode { None, Execution优先, Fact优先 }

#endregion
