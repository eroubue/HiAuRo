using System.Numerics;
using Dalamud.Interface;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// ImGui 通用组件库 — 参照 Ant Design 风格
/// </summary>
public static class ComponentLibrary
{
    // ── 玻璃背景 ──────────────────────────────────

    /// <summary>绘制毛玻璃背景（半透明暗色圆角矩形）</summary>
    public static void GlassBackground(float cornerRadius, float alpha = 0.75f)
    {
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetWindowPos();
        var max = min + ImGui.GetWindowSize();
        var color = ImGui.ColorConvertFloat4ToU32(
            new Vector4(Theme.Colors.BgContainer.X,
                        Theme.Colors.BgContainer.Y,
                        Theme.Colors.BgContainer.Z, alpha));
        dl.AddRectFilled(min, max, color, cornerRadius);
    }

    // ── 按钮变体 ──────────────────────────────────

    /// <summary>强调按钮（填充色）</summary>
    public static bool AccentButton(string label, Vector4 color, Vector2? size = null)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Theme.RadiusXS);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 3));
        ImGui.PushStyleColor(ImGuiCol.Button, color);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(
            Math.Min(color.X * 1.2f, 1f),
            Math.Min(color.Y * 1.2f, 1f),
            Math.Min(color.Z * 1.2f, 1f), 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, color);
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 1, 1, 1));

        var clicked = ImGui.Button(label, size ?? Vector2.Zero);

        ImGui.PopStyleColor(4);
        ImGui.PopStyleVar(2);
        return clicked;
    }

    /// <summary>边框按钮（灰色描边无填充）</summary>
    public static bool OutlineButton(string label, Vector2? size = null)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Theme.RadiusXS);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 3));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(Theme.Colors.BgHover.X,
            Theme.Colors.BgHover.Y, Theme.Colors.BgHover.Z, 0.4f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0, 0, 0, 0));
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.Colors.Border);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Colors.TextSecondary);

        var clicked = ImGui.Button(label, size ?? Vector2.Zero);

        ImGui.PopStyleColor(5);
        ImGui.PopStyleVar(2);
        return clicked;
    }

    // ── 标准 Button（保留向后兼容）───────────────

    public static bool Button(string label, Vector2? size = null, bool disabled = false)
    {
        return AccentButton(label, Theme.Colors.AccentBlue, size);
    }

    // ── 分割竖线 ──────────────────────────────────

    public static void VSplit()
    {
        ImGui.TextColored(Theme.Colors.Border, "│");
    }

    // ── 开关 ─────────────────────────────────────

    public static bool Switch(string id, ref bool value)
    {
        ImGui.PushStyleColor(ImGuiCol.FrameBg, value ? Theme.Colors.AccentGreen : Theme.Colors.BgElevated);
        ImGui.PushStyleColor(ImGuiCol.CheckMark, Theme.Colors.AccentGreen);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 12f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2, 2));

        var changed = ImGui.Checkbox($"##{id}", ref value);

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(2);
        return changed;
    }

    public static bool Switch(string id, string label, ref bool value)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Theme.Colors.TextPrimary, label);
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - 44);
        return Switch(id, ref value);
    }

    // ── 滑块 ─────────────────────────────────────

    public static bool Slider(string id, string label, ref float value, float min, float max, string format = "%.1f")
    {
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.Colors.BgElevated);
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, Theme.Colors.AccentBlue);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, Theme.Colors.AccentBlue);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 60);
        var changed = ImGui.SliderFloat($"##{id}", ref value, min, max, format);
        ImGui.PopStyleColor(3);
        ImGui.SameLine();
        ImGui.TextColored(Theme.Colors.TextSecondary, label);
        return changed;
    }

    public static bool SliderInt(string id, string label, ref int value, int min, int max)
    {
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.Colors.BgElevated);
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, Theme.Colors.AccentBlue);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, Theme.Colors.AccentBlue);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 60);
        var changed = ImGui.SliderInt($"##{id}", ref value, min, max);
        ImGui.PopStyleColor(3);
        ImGui.SameLine();
        ImGui.TextColored(Theme.Colors.TextSecondary, label);
        return changed;
    }

    // ── 下拉选择器 ───────────────────────────────

    public static bool Select(string id, string label, ref int selectedIndex, string[] options)
    {
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.Colors.BgElevated);
        ImGui.PushStyleColor(ImGuiCol.Header, Theme.Colors.AccentBlue);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Theme.Colors.BgHover);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Theme.RadiusSM);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 80);
        var changed = ImGui.Combo($"##{id}", ref selectedIndex, options, options.Length);
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(3);
        ImGui.SameLine();
        ImGui.TextColored(Theme.Colors.TextSecondary, label);
        return changed;
    }

    // ── 标签页 ───────────────────────────────────

    public static bool Tabs(string id, ref int activeTab, string[] tabNames)
    {
        var changed = false;
        if (ImGui.BeginTabBar($"##tabs_{id}", ImGuiTabBarFlags.FittingPolicyScroll))
        {
            for (var i = 0; i < tabNames.Length; i++)
            {
                if (ImGui.BeginTabItem(tabNames[i]))
                {
                    if (i != activeTab) changed = true;
                    activeTab = i;
                    ImGui.EndTabItem();
                }
            }
            ImGui.EndTabBar();
        }
        return changed;
    }

    // ── 卡片 ─────────────────────────────────────

    private static int _cardCounter;

    public static void CardBegin(string? title = null)
    {
        var cardId = title ?? $"card_{Interlocked.Increment(ref _cardCounter)}";
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, Theme.RadiusMD);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1f);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.Colors.BgContainer);
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.Colors.Border);
        ImGui.BeginChild($"##card_{cardId}", new Vector2(-1, 0), true);
        if (title != null)
        {
            ImGui.PushFont(UiBuilder.DefaultFont);
            ImGui.TextColored(Theme.Colors.TextPrimary, title);
            ImGui.PopFont();
            ImGui.Spacing();
        }
    }

    public static void CardEnd()
    {
        ImGui.EndChild();
        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(2);
    }

    // ── 标签芯片 ─────────────────────────────────

    public static void Tag(string label, bool active, Vector4? activeColor = null, Vector4? inactiveColor = null)
    {
        var color = active ? (activeColor ?? Theme.Colors.AccentGreen) : (inactiveColor ?? Theme.Colors.BgElevated);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Theme.RadiusLG);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 2));
        ImGui.PushStyleColor(ImGuiCol.Button, color);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, color);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, color);
        ImGui.PushStyleColor(ImGuiCol.Text, active ? new Vector4(1, 1, 1, 1) : Theme.Colors.TextSecondary);
        ImGui.Button(label, Vector2.Zero);
        ImGui.PopStyleColor(4);
        ImGui.PopStyleVar(2);
    }

    // ── 分割线 ───────────────────────────────────

    public static void Divider()
    {
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Separator, Theme.Colors.Border);
        ImGui.Separator();
        ImGui.PopStyleColor();
        ImGui.Spacing();
    }

    // ── Badge ────────────────────────────────────

    /// <summary>状态圆点 (直径6px, 行高对齐)</summary>
    public static void Badge(bool active, Vector4? activeColor = null)
    {
        var color = active ? (activeColor ?? Theme.Colors.AccentGreen) : Theme.Colors.TextTertiary;
        var dl = ImGui.GetWindowDrawList();
        var cursor = ImGui.GetCursorScreenPos();
        var lineHeight = ImGui.GetTextLineHeight();
        var centerY = cursor.Y + lineHeight / 2;
        dl.AddCircleFilled(new Vector2(cursor.X + 5, centerY), 3, ImGui.ColorConvertFloat4ToU32(color));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 13);
    }

    // ── 数字输入 ─────────────────────────────────

    public static bool InputNumber(string id, string label, ref int value, int step = 1, int stepFast = 10)
    {
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.Colors.BgElevated);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Theme.RadiusSM);
        ImGui.SetNextItemWidth(80);
        var changed = ImGui.InputInt($"##{id}", ref value, step, stepFast);
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.TextColored(Theme.Colors.TextPrimary, label);
        return changed;
    }

    // ── 文本 ─────────────────────────────────────

    public static void Label(string text)
    {
        ImGui.TextColored(Theme.Colors.TextPrimary, text);
    }
}
