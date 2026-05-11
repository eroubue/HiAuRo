using System.Numerics;
using Dalamud.Interface.Windowing;
using HiAuRo.Infrastructure;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// QT 芯片 + 热键网格面板
/// </summary>
public sealed class OverlayActionPanel : OverlayBase
{
    private readonly Action _saveConfig;

    public OverlayActionPanel(PluginConfig config, Action saveConfig) : base("HiAuRoActionPanel##Overlay", config)
    {
        _saveConfig = saveConfig;
        Position = new Vector2(config.OverlayActionPanelX, config.OverlayActionPanelY);
        PositionCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(200, 40),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    protected override void DrawContent()
    {
        var qts = ImGuiOverlayState.Qts;
        var hotkeys = ImGuiOverlayState.Hotkeys;

        // QT 芯片行
        if (qts.Count > 0)
        {
            foreach (var qt in qts)
            {
                var visible = ImGuiOverlayState.UiSettings.QtVisible.GetValueOrDefault(qt.Id, true);
                if (visible)
                {
                    ImGui.PushID(qt.Id);
                    TagWithClick(qt.Label, qt.Value, qt.Color);
                    if (ImGui.IsItemClicked())
                    {
                        HiAuRo.ACR.QTHelper.Toggle(qt.Id);
                    }
                    if (!string.IsNullOrEmpty(qt.Tooltip) && ImGui.IsItemHovered())
                        ImGui.SetTooltip(qt.Tooltip);
                    ImGui.PopID();
                    ImGui.SameLine();
                }
            }
            ImGui.NewLine();
            ImGui.Spacing();
        }

        // 热键网格
        if (hotkeys.Count > 0)
        {
            var cols = ImGuiOverlayState.UiSettings.HkCols;
            if (cols <= 0) cols = 5;
            var btnSize = ImGuiOverlayState.UiSettings.HkBtnSize > 0
                ? ImGuiOverlayState.UiSettings.HkBtnSize : 50;
            var index = 0;

            for (var i = 0; i < hotkeys.Count; i++)
            {
                var hk = hotkeys[i];
                var visible = ImGuiOverlayState.UiSettings.HkVisible.GetValueOrDefault(hk.Id, true);
                if (!visible) continue;

                ImGui.PushID(hk.Id);
                var available = hk.Check() >= 0;
                ImGui.BeginDisabled(!available);
                var binding = HiAuRo.ACR.HotkeyHelper.GetBinding(hk.Id) ?? hk.DefaultKey;
                if (ImGui.Button($"{hk.Label}\n{binding}", new Vector2(btnSize)))
                {
                    if (HiAuRo.Runtime.RuntimeCore.IsRunning)
                        HiAuRo.ACR.HotkeyHelper.ExecuteById(hk.Id);
                }
                ImGui.EndDisabled();

                if (!string.IsNullOrEmpty(hk.Label) && ImGui.IsItemHovered())
                    ImGui.SetTooltip(hk.Label);
                ImGui.PopID();

                index++;
                if (index % cols != 0)
                    ImGui.SameLine();
            }
        }
    }

    private static void TagWithClick(string label, bool active, string? colorHex)
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
        ComponentLibrary.Tag(label, active, activeColor);
    }

    protected override void SavePosition(Vector2 pos)
    {
        _config.OverlayActionPanelX = pos.X;
        _config.OverlayActionPanelY = pos.Y;
        _saveConfig();
    }
}
