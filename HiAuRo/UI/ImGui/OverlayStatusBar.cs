using System.Numerics;
using Dalamud.Interface.Windowing;
using HiAuRo.Infrastructure;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// 状态栏 + ACR 控制面板（可折叠）
/// </summary>
public sealed class OverlayStatusBar : OverlayBase
{
    private readonly Action _saveConfig;

    public OverlayStatusBar(PluginConfig config, Action saveConfig) : base("HiAuRoStatusBar##Overlay", config)
    {
        _saveConfig = saveConfig;
        Position = new Vector2(config.OverlayStatusBarX, config.OverlayStatusBarY);
        PositionCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(280, 40),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    protected override void DrawContent()
    {
        var expanded = _config.OverlayStatusBarExpanded;

        if (!expanded)
        {
            DrawCollapsedBar();
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - 20);
            if (ImGui.ArrowButton("##expandSB", ImGuiDir.Down))
            {
                _config.OverlayStatusBarExpanded = true;
                _saveConfig();
            }
            return;
        }

        DrawExpandedHeader();
        ImGui.Spacing();

        var controls = ImGuiOverlayState.Controls;
        if (controls.Count == 0)
        {
            ImGui.TextColored(Theme.Colors.TextSecondary, "等待 ACR 加载...");
            return;
        }

        var tabs = controls.Where(c => c.Type == "tab").ToList();
        if (tabs.Count > 0)
        {
            var tabNames = tabs.Select(t => t.Label).ToArray();
            var activeIdx = tabs.FindIndex(t => t.Id == ImGuiOverlayState.ActiveTab);
            if (activeIdx < 0) activeIdx = 0;
            if (ComponentLibrary.Tabs("statusbar", ref activeIdx, tabNames))
            {
                ImGuiOverlayState.ActiveTab = tabs[activeIdx].Id;
            }
        }

        ImGui.Spacing();
        ImGuiWidgetRenderer.Render(controls, ImGuiOverlayState.ActiveTab);
    }

    private static void DrawCollapsedBar()
    {
        ComponentLibrary.Badge(ImGuiOverlayState.IsRunning,
            ImGuiOverlayState.IsRunning ? (ImGuiOverlayState.IsPaused ? Theme.Colors.AccentOrange : Theme.Colors.AccentGreen) : null);
        ImGui.SameLine();
        var state = ImGuiOverlayState.IsRunning
            ? (ImGuiOverlayState.IsPaused ? "已暂停" : "运行中")
            : "已停止";
        ImGui.TextColored(Theme.Colors.TextPrimary, $"{state}  {ImGuiOverlayState.AcrName}");
    }

    private void DrawExpandedHeader()
    {
        ComponentLibrary.Badge(ImGuiOverlayState.IsRunning,
            ImGuiOverlayState.IsRunning ? (ImGuiOverlayState.IsPaused ? Theme.Colors.AccentOrange : Theme.Colors.AccentGreen) : null);
        ImGui.SameLine();
        var state = ImGuiOverlayState.IsRunning
            ? (ImGuiOverlayState.IsPaused ? "已暂停" : "运行中")
            : "已停止";
        ImGui.TextColored(Theme.Colors.TextPrimary, $"{state}  {ImGuiOverlayState.AcrName}");

        ImGui.SameLine();
        var avail = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, avail - 20));
        if (ImGui.ArrowButton("##collapseSB", ImGuiDir.Up))
        {
            _config.OverlayStatusBarExpanded = false;
            _saveConfig();
        }
    }

    protected override void SavePosition(Vector2 pos)
    {
        _config.OverlayStatusBarX = pos.X;
        _config.OverlayStatusBarY = pos.Y;
        _saveConfig();
    }
}
