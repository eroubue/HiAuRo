using System.Numerics;
using Dalamud.Interface;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// ImGui 通用组件库 — 参照 Ant Design 风格
/// 每个组件为 static 方法，返回是否发生交互
/// </summary>
public static class ComponentLibrary
{
    /// <summary>按钮 — 主题色圆角按钮</summary>
    public static bool Button(string label, Vector2? size = null, bool disabled = false)
    {
        if (disabled) ImGui.BeginDisabled();
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Theme.RadiusSM);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Theme.PaddingSM);
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.Colors.AccentBlue);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(
            Theme.Colors.AccentBlue.X * 1.15f,
            Theme.Colors.AccentBlue.Y * 1.15f,
            Theme.Colors.AccentBlue.Z * 1.15f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.Colors.AccentBlue);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Colors.TextPrimary);

        var clicked = ImGui.Button(label, size ?? Vector2.Zero);

        ImGui.PopStyleColor(4);
        ImGui.PopStyleVar(2);
        if (disabled) ImGui.EndDisabled();
        return clicked;
    }

    /// <summary>开关 — 带颜色的 checkbox</summary>
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

    /// <summary>开关 + 标签（一行）</summary>
    public static bool Switch(string id, string label, ref bool value)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Theme.Colors.TextPrimary, label);
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - 44);
        return Switch(id, ref value);
    }

    /// <summary>滑块 — 主题色滑块</summary>
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

    /// <summary>整数滑块</summary>
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

    /// <summary>下拉选择器</summary>
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

    /// <summary>标签页</summary>
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

    private static int _cardCounter;

    /// <summary>卡片容器 — Begin/End 配对</summary>
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

    /// <summary>标签芯片 — 彩色小标签</summary>
    public static void Tag(string label, bool active, Vector4? activeColor = null, Vector4? inactiveColor = null)
    {
        var color = active
            ? (activeColor ?? Theme.Colors.AccentGreen)
            : (inactiveColor ?? Theme.Colors.BgElevated);

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

    /// <summary>分割线</summary>
    public static void Divider()
    {
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Separator, Theme.Colors.Border);
        ImGui.Separator();
        ImGui.PopStyleColor();
        ImGui.Spacing();
    }

    /// <summary>Badge — 状态圆点</summary>
    public static void Badge(bool active, Vector4? activeColor = null)
    {
        var color = active ? (activeColor ?? Theme.Colors.AccentGreen) : Theme.Colors.TextTertiary;
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos() + new Vector2(0, 5);
        dl.AddCircleFilled(pos + new Vector2(4, 4), 4, ImGui.ColorConvertFloat4ToU32(color));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 14);
    }

    /// <summary>数字输入</summary>
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

    /// <summary>纯文本标签</summary>
    public static void Label(string text)
    {
        ImGui.TextColored(Theme.Colors.TextPrimary, text);
    }
}
