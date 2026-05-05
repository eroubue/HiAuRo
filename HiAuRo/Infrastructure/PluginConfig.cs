using Dalamud.Configuration;

namespace HiAuRo.Infrastructure;

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

    /// <summary>CEF 悬浮窗设置</summary>
    public OverlayWindowSetting[] Overlays { get; set; } =
    [
        new() { Name = "MainWindow", Url = "http://localhost:5678/main.html", Width = 310, Height = 480 },
        new() { Name = "QtWindow", Url = "http://localhost:5678/qt.html", Width = 200, Height = 50 },
        new() { Name = "HotkeyWindow", Url = "http://localhost:5678/hotkey.html", Width = 260, Height = 130 },
    ];
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
