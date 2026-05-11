using System.Numerics;
using Dalamud.Interface.Windowing;
using HiAuRo.Infrastructure;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// 状态栏 + ACR 控制面板（折叠=36px单行 / 展开=完整面板）
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
            MinimumSize = new Vector2(260, 36),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    protected override void DrawContent()
    {
        DrawBar(false);

        if (!_config.OverlayStatusBarExpanded) return;

        var controls = ImGuiOverlayState.Controls;
        if (controls.Count == 0)
        {
            ImGui.TextColored(Theme.Colors.TextSecondary, "  等待 ACR 加载...");
            return;
        }

        ImGui.Spacing();

        var tabs = controls.Where(c => c.Type == "tab").ToList();
        if (tabs.Count > 0)
        {
            var tabNames = tabs.Select(t => t.Label).ToArray();
            var activeIdx = tabs.FindIndex(t => t.Id == ImGuiOverlayState.ActiveTab);
            if (activeIdx < 0) activeIdx = 0;
            if (ComponentLibrary.Tabs("statusbar", ref activeIdx, tabNames))
                ImGuiOverlayState.ActiveTab = tabs[activeIdx].Id;
            ImGui.Spacing();
        }

        ImGuiWidgetRenderer.Render(controls, ImGuiOverlayState.ActiveTab);
    }

    private void DrawBar(bool compact)
    {
        var isRunning = ImGuiOverlayState.IsRunning;
        var isPaused = ImGuiOverlayState.IsPaused;
        var acrName = ImGuiOverlayState.AcrName;

        // ── 状态段 ──
        Vector4 statusColor;
        string statusText;
        if (isRunning && !isPaused) { statusColor = Theme.Colors.AccentGreen; statusText = "运行中"; }
        else if (isPaused) { statusColor = Theme.Colors.AccentOrange; statusText = "已暂停"; }
        else { statusColor = Theme.Colors.TextTertiary; statusText = "已停止"; }

        ComponentLibrary.Badge(isRunning && !isPaused, isPaused ? Theme.Colors.AccentOrange :
            isRunning ? Theme.Colors.AccentGreen : null);
        ImGui.SameLine(0, 4);
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(statusColor, statusText);
        ImGui.SameLine(0, 4);

        // ── 分割 1 ──
        ComponentLibrary.VSplit();
        ImGui.SameLine(0, 4);

        // ── ACR 名称段 ──
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Theme.Colors.TextSecondary, acrName);
        ImGui.SameLine(0, 4);

        // ── 分割 2 ──
        ComponentLibrary.VSplit();
        ImGui.SameLine(0, 4);

        // ── 操作按钮段 ──
        if (isRunning && !isPaused)
        {
            // 运行中: [■ 停止] [⏸ 暂停] [💾]
            if (ComponentLibrary.AccentButton("■", Theme.Colors.AccentRed * 0.8f, new Vector2(24, 0)))
                Runtime.RuntimeCore.Stop();
            ImGui.SameLine(0, 2);
            if (ComponentLibrary.AccentButton("⏸", Theme.Colors.AccentOrange, new Vector2(24, 0)))
                HiAuRo.ACR.MainControlHelper.TogglePause();
        }
        else if (isPaused)
        {
            // 已暂停: [■ 停止] [▶ 继续] [💾]
            if (ComponentLibrary.AccentButton("■", Theme.Colors.AccentRed * 0.8f, new Vector2(24, 0)))
                Runtime.RuntimeCore.Stop();
            ImGui.SameLine(0, 2);
            if (ComponentLibrary.AccentButton("▶", Theme.Colors.AccentOrange, new Vector2(24, 0)))
                HiAuRo.ACR.MainControlHelper.TogglePause();
        }
        else
        {
            // 已停止: [▶ 启动] [⏸] [💾]
            if (ComponentLibrary.AccentButton("▶", Theme.Colors.AccentGreen, new Vector2(24, 0)))
                Runtime.RuntimeCore.Start();
            ImGui.SameLine(0, 2);
            ImGui.BeginDisabled();
            ComponentLibrary.OutlineButton("⏸", new Vector2(24, 0));
            ImGui.EndDisabled();
        }

        ImGui.SameLine(0, 2);
        if (ComponentLibrary.OutlineButton("💾", new Vector2(24, 0)))
            HiAuRo.ACR.MainControlHelper.Save();

        ImGui.SameLine(0, 4);

        // ── 折叠箭头 ──
        ComponentLibrary.VSplit();
        ImGui.SameLine(0, 4);
        if (ImGui.ArrowButton("##sbFold", _config.OverlayStatusBarExpanded ? ImGuiDir.Up : ImGuiDir.Down))
        {
            _config.OverlayStatusBarExpanded = !_config.OverlayStatusBarExpanded;
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
