using System.Numerics;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using HiAuRo.Infrastructure;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// Hotkey 热键图标面板 — 绘制游戏技能图标，点击执行，自适应内容大小
/// </summary>
public sealed class OverlayHotkeyPanel : OverlayBase
{
    private readonly Action _saveConfig;

    protected override bool AllowResize => false;
    protected override Vector2 ContentPadding => Vector2.Zero;
    protected override Vector2 ContentOffset => new(6, 6);

    public OverlayHotkeyPanel(PluginConfig config, Action saveConfig) : base("HiAuRoHotkeyPanel##Overlay", config)
    {
        _saveConfig = saveConfig;
        Position = new Vector2(config.OverlayHotkeyPanelX, config.OverlayHotkeyPanelY);
        PositionCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(56, 38),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    protected override void OnPreDraw()
    {
        // 根据内容网格计算初始窗口大小
        var hotkeys = ImGuiOverlayState.Hotkeys;
        var visibleCount = hotkeys.Count(h => ImGuiOverlayState.UiSettings.HkVisible.GetValueOrDefault(h.Id, true));
        var cols = ImGuiOverlayState.UiSettings.HkCols > 0 ? ImGuiOverlayState.UiSettings.HkCols : 5;
        if (visibleCount == 0) return;
        var rows = (visibleCount + cols - 1) / cols;
        var actualCols = Math.Min(visibleCount, cols);
        var pad = ContentOffset.X;       // 内容起始偏移
        var gapX = 6f;                   // ItemSpacing.X
        var gapY = 4f;                   // ItemSpacing.Y
        var btnW = ImGuiOverlayState.UiSettings.HkBtnSize > 0 ? ImGuiOverlayState.UiSettings.HkBtnSize : 50f;
        var btnH = btnW;                 // 方形按钮
        var w = pad * 2 + btnW * actualCols + gapX * (actualCols - 1);
        var h = pad * 2 + btnH * rows + gapY * (rows - 1);
        ImGui.SetNextWindowSize(new Vector2(w, h), ImGuiCond.Always);
    }

    protected override void DrawContent()
    {
        var hotkeys = ImGuiOverlayState.Hotkeys;

        if (hotkeys.Count == 0)
        {
            BeginContent();
            ImGui.TextColored(Theme.Colors.TextTertiary, "无热键技能");
            return;
        }

        BeginContent();

        var cols = ImGuiOverlayState.UiSettings.HkCols;
        if (cols <= 0) cols = 5;
        var btnSize = ImGuiOverlayState.UiSettings.HkBtnSize > 0
            ? ImGuiOverlayState.UiSettings.HkBtnSize : 50;
        var col = 0;

        for (var i = 0; i < hotkeys.Count; i++)
        {
            var hk = hotkeys[i];
            var visible = ImGuiOverlayState.UiSettings.HkVisible.GetValueOrDefault(hk.Id, true);
            if (!visible) continue;

            ImGui.PushID(hk.Id);

            var available = hk.Check() >= 0;
            ImGui.BeginDisabled(!available);
            var binding = HiAuRo.ACR.HotkeyHelper.GetBinding(hk.Id) ?? hk.DefaultKey;

            var clicked = DrawIconButton(hk.IconId, new Vector2(btnSize));

            if (clicked && HiAuRo.Runtime.RuntimeCore.IsRunning)
                HiAuRo.ACR.HotkeyHelper.ExecuteById(hk.Id);

            ImGui.EndDisabled();

            if (ImGui.IsItemHovered())
            {
                var tip = string.IsNullOrEmpty(binding) ? hk.Label : $"{hk.Label}   {binding}";
                ImGui.SetTooltip(tip);
            }

            ImGui.PopID();

            SameLineOrWrap(ref col, cols);
        }
    }

    /// <summary>AntdUI 风格图标按钮 — 游戏技能图标 + 圆角边框</summary>
    private bool DrawIconButton(uint iconId, Vector2 btnSize)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Theme.RadiusMD);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4, 4));

        ImGui.PushStyleColor(ImGuiCol.Button, Theme.Colors.BgContainer);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.Colors.BgHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.Colors.FillPrimary);
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.Colors.BorderSecondary);

        var clicked = ImGui.Button($"##hkb_{iconId}_{btnSize.X}", btnSize);

        var rectMin = ImGui.GetItemRectMin();
        var rectMax = ImGui.GetItemRectMax();
        var pad = 4f;
        var iconPos = rectMin + new Vector2(pad, pad);
        var iconSize = btnSize - new Vector2(pad * 2, pad * 2);

        if (iconId != 0)
        {
            var handle = LoadIconTexture(iconId);
            if (handle != 0)
                ImGui.GetWindowDrawList().AddImage(
                    handle, iconPos, iconPos + iconSize);
        }

        ImGui.PopStyleColor(4);
        ImGui.PopStyleVar(2);

        return clicked;
    }

    /// <summary>加载游戏技能图标纹理（无缓存，每帧从 Dalamud 纹理提供者获取）</summary>
    private static ImTextureID LoadIconTexture(uint iconId)
    {
        var wrap = DService.Instance().Texture.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrDefault();
        return wrap?.Handle ?? 0;
    }

    protected override void SavePosition(Vector2 pos)
    {
        _config.OverlayHotkeyPanelX = pos.X;
        _config.OverlayHotkeyPanelY = pos.Y;
        _saveConfig();
    }
}