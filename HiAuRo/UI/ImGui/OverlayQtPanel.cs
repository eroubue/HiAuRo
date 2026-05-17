using System.Numerics;
using Dalamud.Interface.Windowing;
using HiAuRo.Infrastructure;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// QT 芯片面板 — 独立窗口，自适应内容大小
/// </summary>
public sealed class OverlayQtPanel : OverlayBase
{
    private readonly Action _saveConfig;

    /// <summary>不允许缩放</summary>
    protected override bool AllowResize => false;
    /// <summary>无边距</summary>
    protected override Vector2 ContentPadding => Vector2.Zero;
    /// <summary>内容起始偏移</summary>
    protected override Vector2 ContentOffset => new(6, 6);

    /// <summary>Initializes a new instance of the <see cref="OverlayQtPanel"/> class</summary>
    public OverlayQtPanel(PluginConfig config, Action saveConfig) : base("HiAuRoQtPanel##Overlay", config)
    {
        _saveConfig = saveConfig;
        Position = new Vector2(config.OverlayQtPanelX, config.OverlayQtPanelY);
        PositionCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(56, 38),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    /// <summary>预绘制时计算窗口尺寸</summary>
    protected override void OnPreDraw()
    {
        // 根据内容网格计算初始窗口大小
        var qts = ImGuiOverlayState.Qts;
        var visibleCount = qts.Count(q => ImGuiOverlayState.UiSettings.QtVisible.GetValueOrDefault(q.Id, true));
        var cols = ImGuiOverlayState.UiSettings.QtCols > 0 ? ImGuiOverlayState.UiSettings.QtCols : 4;
        if (visibleCount == 0) return;
        var rows = (visibleCount + cols - 1) / cols;
        var actualCols = Math.Min(visibleCount, cols);
        var pad = ContentOffset.X;       // 内容起始偏移
        var gapX = 6f;                   // ItemSpacing.X
        var gapY = 4f;                   // ItemSpacing.Y
        var btnW = 67f;                  // 按钮宽度
        var btnH = 46f;                  // 按钮高度
        var w = pad * 2 + btnW * actualCols + gapX * (actualCols - 1);
        var h = pad * 2 + btnH * rows + gapY * (rows - 1);
        ImGui.SetNextWindowSize(new Vector2(w, h), ImGuiCond.Always);
    }

    /// <summary>绘制 QT 面板内容</summary>
    protected override void DrawContent()
    {
        var qts = ImGuiOverlayState.Qts;
        if (qts.Count == 0)
        {
            BeginContent();
            ImGui.TextColored(Theme.Colors.TextTertiary, "无 QT 开关");
            return;
        }

        BeginContent();

        var cols = ImGuiOverlayState.UiSettings.QtCols;
        if (cols <= 0) cols = 4;
        var col = 0;

        foreach (var qt in qts)
        {
            var visible = ImGuiOverlayState.UiSettings.QtVisible.GetValueOrDefault(qt.Id, true);
            if (!visible) continue;

            ImGui.PushID(qt.Id);
            if (TagWithClick(qt.Label, qt.Value, qt.Color))
                HiAuRo.ACR.QTHelper.Toggle(qt.Id);
            if (!string.IsNullOrEmpty(qt.Tooltip) && ImGui.IsItemHovered())
                ImGui.SetTooltip(qt.Tooltip);
            ImGui.PopID();

            SameLineOrWrap(ref col, cols);
        }
    }

    private static bool TagWithClick(string label, bool active, string? colorHex)
    {
        var activeColor = Theme.Colors.AccentGreen;
        if (!string.IsNullOrEmpty(colorHex))
        {
            try
            {
                var hex = colorHex.StartsWith('#') ? colorHex[1..] : colorHex;
                var r = Convert.ToInt32(hex[..2], 16);
                var g = Convert.ToInt32(hex[2..4], 16);
                var b = Convert.ToInt32(hex[4..6], 16);
                activeColor = new Vector4(r / 255f, g / 255f, b / 255f, 1f);
            }
            catch { }
        }
        var color = active ? activeColor : Theme.Colors.FillSecondary;
        var textColor = active ? Theme.Colors.TagActiveText : Theme.Colors.TextSecondary;
        var btnSize = new Vector2(67, 46); // 56×38 × 1.2
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Theme.RadiusSM);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 4));
        ImGui.PushStyleColor(ImGuiCol.Button, color);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, color);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, color);
        ImGui.PushStyleColor(ImGuiCol.Text, textColor);
        var clicked = ImGui.Button(label, btnSize);
        ImGui.PopStyleColor(4);
        ImGui.PopStyleVar(2);
        return clicked;
    }

    /// <summary>保存窗口位置</summary>
    protected override void SavePosition(Vector2 pos)
    {
        _config.OverlayQtPanelX = pos.X;
        _config.OverlayQtPanelY = pos.Y;
        _saveConfig();
    }
}