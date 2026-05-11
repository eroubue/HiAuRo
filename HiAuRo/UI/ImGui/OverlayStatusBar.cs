using System.Numerics;
using Dalamud.Interface.Windowing;
using HiAuRo.Infrastructure;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// 状态栏 + ACR 控制面板（折叠=36px / 展开=左侧Tab+右侧内容）
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
        DrawBar();

        if (!_config.OverlayStatusBarExpanded) return;

        var controls = ImGuiOverlayState.Controls;
        if (controls.Count == 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(Theme.Colors.TextSecondary, "  等待 ACR 加载...");
            return;
        }

        ImGui.Spacing();

        var tabs = controls.Where(c => c.Type == "tab").ToList();

        if (tabs.Count == 0)
        {
            // 无 Tab — 直接渲染
            ImGuiWidgetRenderer.Render(controls, ImGuiOverlayState.ActiveTab);
            return;
        }

        // ── 左侧垂直 Tab 栏 ──
        var sidebarW = 72f;
        var contentHeight = ImGui.GetContentRegionAvail().Y - 4;

        ImGui.BeginChild("##sidebar", new Vector2(sidebarW, contentHeight), true,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground);

        var activeTabId = ImGuiOverlayState.ActiveTab;
        var dl = ImGui.GetWindowDrawList();

        foreach (var tab in tabs)
        {
            var isActive = tab.Id == activeTabId;
            var label = tab.Label;
            var cursor = ImGui.GetCursorScreenPos();

            // 选中态：背景高亮 + 左侧蓝色竖线
            if (isActive)
            {
                var rowMax = cursor + new Vector2(sidebarW, ImGui.GetTextLineHeight() + 6);
                dl.AddRectFilled(cursor, rowMax,
                    ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 0.06f)));
                dl.AddLine(
                    cursor + new Vector2(1, 2),
                    cursor + new Vector2(1, ImGui.GetTextLineHeight() + 4),
                    ImGui.ColorConvertFloat4ToU32(Theme.Colors.AccentBlue), 2f);
            }

            ImGui.SetCursorPosX(12);
            ImGui.SetCursorPosY(cursor.Y + 3);
            ImGui.PushStyleColor(ImGuiCol.Text,
                isActive ? Theme.Colors.AccentBlue : Theme.Colors.TextSecondary);
            if (ImGui.Selectable(label, isActive, ImGuiSelectableFlags.None,
                    new Vector2(sidebarW - 16, 0)))
            {
                ImGuiOverlayState.ActiveTab = tab.Id;
            }
            ImGui.PopStyleColor();
        }

        ImGui.EndChild();
        ImGui.SameLine(0, 4);

        // ── 右侧内容区 ──
        ImGui.BeginChild("##content", new Vector2(-1, contentHeight), false,
            ImGuiWindowFlags.NoBackground);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Theme.PaddingSM);
        ImGuiWidgetRenderer.Render(controls, activeTabId);
        ImGui.PopStyleVar();
        ImGui.EndChild();
    }

    private void DrawBar()
    {
        var isRunning = ImGuiOverlayState.IsRunning;
        var isPaused = ImGuiOverlayState.IsPaused;
        var acrName = ImGuiOverlayState.AcrName;

        // ── 状态段 ──
        string statusText;
        Vector4 statusColor;
        if (isRunning && !isPaused) { statusColor = Theme.Colors.AccentGreen; statusText = "运行中"; }
        else if (isPaused) { statusColor = Theme.Colors.AccentOrange; statusText = "已暂停"; }
        else { statusColor = Theme.Colors.TextTertiary; statusText = "已停止"; }

        ComponentLibrary.Badge(isRunning && !isPaused, isPaused ? Theme.Colors.AccentOrange :
            isRunning ? Theme.Colors.AccentGreen : null);
        ImGui.SameLine(0, 4);
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(statusColor, statusText);
        ImGui.SameLine(0, 4);
        ComponentLibrary.VSplit();
        ImGui.SameLine(0, 4);

        // ── ACR 名称段 ──
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Theme.Colors.TextSecondary, acrName);
        ImGui.SameLine(0, 4);
        ComponentLibrary.VSplit();
        ImGui.SameLine(0, 4);

        // ── 操作按钮段 ──
        var btnSize = new Vector2(24, 22);
        if (isRunning && !isPaused)
        {
            if (ComponentLibrary.IconButton(ComponentLibrary.IconType.Stop,
                    Theme.Colors.AccentRed * 0.85f, btnSize))
                Runtime.RuntimeCore.Stop();
            ImGui.SameLine(0, 2);
            if (ComponentLibrary.IconButton(ComponentLibrary.IconType.Pause,
                    Theme.Colors.AccentOrange, btnSize))
                HiAuRo.ACR.MainControlHelper.TogglePause();
        }
        else if (isPaused)
        {
            if (ComponentLibrary.IconButton(ComponentLibrary.IconType.Stop,
                    Theme.Colors.AccentRed * 0.85f, btnSize))
                Runtime.RuntimeCore.Stop();
            ImGui.SameLine(0, 2);
            if (ComponentLibrary.IconButton(ComponentLibrary.IconType.Play,
                    Theme.Colors.AccentOrange, btnSize))
                HiAuRo.ACR.MainControlHelper.TogglePause();
        }
        else
        {
            if (ComponentLibrary.IconButton(ComponentLibrary.IconType.Play,
                    Theme.Colors.AccentGreen, btnSize))
                Runtime.RuntimeCore.Start();
            ImGui.SameLine(0, 2);
            ImGui.BeginDisabled();
            ComponentLibrary.IconButton(ComponentLibrary.IconType.Pause,
                Theme.Colors.TextTertiary, btnSize, outline: true);
            ImGui.EndDisabled();
        }

        ImGui.SameLine(0, 2);
        if (ComponentLibrary.IconButton(ComponentLibrary.IconType.Save,
                Theme.Colors.Border, btnSize, outline: true))
            HiAuRo.ACR.MainControlHelper.Save();

        ImGui.SameLine(0, 4);
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
