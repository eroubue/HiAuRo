using System.Numerics;
using Dalamud.Interface.Windowing;
using HiAuRo.Infrastructure;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// 状态栏 + ACR 控制面板（折叠=紧凑条 / 展开=左侧Tab+右侧内容）
/// </summary>
public sealed class OverlayStatusBar : OverlayBase
{
    private readonly Action _saveConfig;
    private Vector2 _lastExpandedSize = new(420, 300);

    public OverlayStatusBar(PluginConfig config, Action saveConfig) : base("HiAuRoStatusBar##Overlay", config)
    {
        _saveConfig = saveConfig;
        Position = new Vector2(config.OverlayStatusBarX, config.OverlayStatusBarY);
        PositionCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(320, 52),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    protected override void DrawContent()
    {
        var expanded = _config.OverlayStatusBarExpanded;
        var currentSize = ImGui.GetWindowSize();

        // 折叠：保存当前尺寸，缩小到紧凑条
        if (!expanded)
        {
            if (currentSize.Y > 60) _lastExpandedSize = currentSize;
            DrawBar();
            ImGui.SetWindowSize(new Vector2(420, 52));
            return;
        }

        // 展开：保存当前尺寸用于下次折叠恢复
        if (currentSize.Y > 60) _lastExpandedSize = currentSize;

        DrawBar();
        ImGui.Spacing();

        var controls = ImGuiOverlayState.Controls;
        if (controls.Count == 0)
        {
            ImGui.TextColored(Theme.Colors.TextSecondary, "  等待 ACR 加载...");
            return;
        }

        var tabs = controls.Where(c => c.Type == "tab").ToList();

        if (tabs.Count == 0)
        {
            ImGuiWidgetRenderer.Render(controls, ImGuiOverlayState.ActiveTab);
            return;
        }

        var sidebarW = 72f;
        var contentHeight = ImGui.GetContentRegionAvail().Y - 4;

        ImGui.BeginChild("##sidebar", new Vector2(sidebarW, contentHeight), true,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground);

        var activeTabId = ImGuiOverlayState.ActiveTab;
        var dl = ImGui.GetWindowDrawList();

        foreach (var tab in tabs)
        {
            var isActive = tab.Id == activeTabId;
            var cursor = ImGui.GetCursorScreenPos();

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
            if (ImGui.Selectable(tab.Label, isActive, ImGuiSelectableFlags.None,
                    new Vector2(sidebarW - 16, 0)))
                ImGuiOverlayState.ActiveTab = tab.Id;
            ImGui.PopStyleColor();
        }

        ImGui.EndChild();
        ImGui.SameLine(0, 4);

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

        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Theme.Colors.TextSecondary, acrName);
        ImGui.SameLine(0, 4);
        ComponentLibrary.VSplit();
        ImGui.SameLine(0, 4);

        var btnSize = new Vector2(44, 30);
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
