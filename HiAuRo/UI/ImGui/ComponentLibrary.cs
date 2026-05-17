using System.Numerics;
using Dalamud.Interface;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// ImGui 组件库 — 复刻 Ant Design 5 按钮/开关/卡片等组件
/// 参考: https://ant-design.antgroup.com/components/button-cn
/// </summary>
public static class ComponentLibrary
{
    // ═══════════════════════════════════════════════════
    // 颜色工具
    // ═══════════════════════════════════════════════════

    private static Vector4 Lighten(Vector4 c, float amount)
    {
        return new Vector4(
            Math.Min(c.X + amount, 1f),
            Math.Min(c.Y + amount, 1f),
            Math.Min(c.Z + amount, 1f),
            c.W);
    }

    private static Vector4 Darken(Vector4 c, float amount)
    {
        return new Vector4(
            Math.Max(c.X - amount, 0f),
            Math.Max(c.Y - amount, 0f),
            Math.Max(c.Z - amount, 0f),
            c.W);
    }

    private static uint ColorU32(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c);

    // ═══════════════════════════════════════════════════
    // 毛玻璃背景 + 阴影
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 半透明圆角背景 + 阴影 — 铺满整个窗口
    /// </summary>
    public static void GlassBackground(float cornerRadius)
    {
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetWindowPos();
        var max = min + ImGui.GetWindowSize();

        // 底层阴影
        dl.AddRectFilled(min + new Vector2(0, 2), max + new Vector2(0, 2),
            ColorU32(Theme.Colors.GlassShadow), cornerRadius);

        // 半透明底色 — 铺满整个窗口
        dl.AddRectFilled(min, max,
            ColorU32(Theme.Colors.GlassBg), cornerRadius);

        // 1px 细边框
        dl.AddRect(min, max,
            ColorU32(Theme.Colors.GlassBorder), cornerRadius, 0, 1.0f);
    }

    // ═══════════════════════════════════════════════════
    // 图标集 (DrawList 矢量绘制)
    // ═══════════════════════════════════════════════════

    /// <summary>图标类型</summary>
    public enum IconType
    {
        /// <summary>播放</summary>
        Play,
        /// <summary>停止</summary>
        Stop,
        /// <summary>暂停</summary>
        Pause,
        /// <summary>保存</summary>
        Save,
        /// <summary>向下箭头</summary>
        ChevronDown,
        /// <summary>向上箭头</summary>
        ChevronUp,
        /// <summary>关闭</summary>
        Close
    }

    /// <summary>在指定中心绘制图标 (图标尺寸约 12~14px)</summary>
    private static void DrawIcon(ImDrawListPtr dl, Vector2 center, IconType icon, uint color)
    {
        switch (icon)
        {
            case IconType.Stop:
                // 10×10 实心方块，圆角 2px
                dl.AddRectFilled(center - new Vector2(5, 5), center + new Vector2(5, 5), color, Theme.RadiusXS);
                break;

            case IconType.Play:
                // 12×14 三角形
                dl.AddTriangleFilled(
                    center + new Vector2(-5, -7),
                    center + new Vector2(-5, 7),
                    center + new Vector2(7, 0),
                    color);
                break;

            case IconType.Pause:
                // 两根 3×12 竖条，间距 2px
                dl.AddRectFilled(center + new Vector2(-5, -6), center + new Vector2(-2, 6), color, 1);
                dl.AddRectFilled(center + new Vector2(2, -6), center + new Vector2(5, 6), color, 1);
                break;

            case IconType.Save:
                // 软盘图标: 10×10 矩形 + 顶部切口
                dl.AddRectFilled(center + new Vector2(-5, -3), center + new Vector2(5, 7), color, Theme.RadiusXS);
                dl.AddRectFilled(center + new Vector2(-5, -3), center + new Vector2(-1, 0), ColorU32(Vector4.Zero));
                dl.AddRectFilled(center + new Vector2(-3, 2), center + new Vector2(3, 6), ColorU32(Theme.Colors.GlassBg), 1);
                break;

            case IconType.ChevronDown:
                // 向下 V 形
                dl.AddTriangleFilled(
                    center + new Vector2(-6, -2), center + new Vector2(6, -2), center + new Vector2(0, 5), color);
                break;

            case IconType.ChevronUp:
                // 向上 V 形
                dl.AddTriangleFilled(
                    center + new Vector2(-6, 2), center + new Vector2(6, 2), center + new Vector2(0, -5), color);
                break;

            case IconType.Close:
                // X 形
                dl.AddLine(center + new Vector2(-4, -4), center + new Vector2(4, 4), color, 2.5f);
                dl.AddLine(center + new Vector2(4, -4), center + new Vector2(-4, 4), color, 2.5f);
                break;
        }
    }

    // ═══════════════════════════════════════════════════
    // 图标按钮 (AntdUI Icon Button 风格)
    // ═══════════════════════════════════════════════════

    /// <summary>图标按钮样式</summary>
    public enum IconButtonStyle
    {
        /// <summary>填充样式</summary>
        Fill,
        /// <summary>边框样式</summary>
        Outline,
        /// <summary>文字样式</summary>
        Text
    }

    /// <summary>
    /// AntdUI 风格图标按钮 — 大尺寸、大圆角、清晰图标
    /// size 最小建议 52×36 (战斗易点击)
    /// </summary>
    public static bool IconButton(IconType icon, Vector4 color, Vector2 size,
        IconButtonStyle style = IconButtonStyle.Fill)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Theme.RadiusMD);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 6));

        Vector4 bg, bgHover, bgActive, borderCol, iconCol;

        if (style == IconButtonStyle.Fill)
        {
            bg = color;
            bgHover = Lighten(color, 0.12f);
            bgActive = Darken(color, 0.12f);
            borderCol = color;
            iconCol = new Vector4(1, 1, 1, 1);
        }
        else if (style == IconButtonStyle.Outline)
        {
            bg = Vector4.Zero;
            bgHover = Theme.Colors.BgHover;
            bgActive = Theme.Colors.FillPrimary;
            borderCol = color;
            iconCol = color;
        }
        else // Text
        {
            bg = Vector4.Zero;
            bgHover = Theme.Colors.BgHover;
            bgActive = Theme.Colors.FillPrimary;
            borderCol = Vector4.Zero;
            iconCol = color;
        }

        ImGui.PushStyleColor(ImGuiCol.Button, bg);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, bgHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, bgActive);
        if (style == IconButtonStyle.Outline)
            ImGui.PushStyleColor(ImGuiCol.Border, borderCol);

        var clicked = ImGui.Button($"##icon_{icon}", size);

        var rectMin = ImGui.GetItemRectMin();
        var rectMax = ImGui.GetItemRectMax();
        var center = (rectMin + rectMax) / 2;
        DrawIcon(ImGui.GetWindowDrawList(), center, icon, ColorU32(iconCol));

        if (style == IconButtonStyle.Outline)
            ImGui.PopStyleColor(4);
        else
            ImGui.PopStyleColor(3);
        ImGui.PopStyleVar(2);

        return clicked;
    }

    // ═══════════════════════════════════════════════════
    // Ant Design 5 按钮变体
    // ═══════════════════════════════════════════════════

    /// <summary>主按钮（蓝色填充）</summary>
    public static bool PrimaryButton(string label, Vector2? size = null)
        => FillButton(label, Theme.Colors.AccentBlue, size);

    /// <summary>危险按钮（红色填充）</summary>
    public static bool DangerButton(string label, Vector2? size = null)
        => FillButton(label, Theme.Colors.AccentRed, size);

    /// <summary>成功按钮（绿色填充）</summary>
    public static bool SuccessButton(string label, Vector2? size = null)
        => FillButton(label, Theme.Colors.AccentGreen, size);

    /// <summary>警告按钮（橙色填充）</summary>
    public static bool WarningButton(string label, Vector2? size = null)
        => FillButton(label, Theme.Colors.AccentOrange, size);

    /// <summary>默认按钮（白底灰边）</summary>
    public static bool DefaultButton(string label, Vector2? size = null)
        => OutlineButton(label, Theme.Colors.Border, Theme.Colors.TextPrimary, size);

    /// <summary>文字按钮（透明背景）</summary>
    public static bool TextButton(string label, Vector2? size = null)
        => GhostButton(label, Theme.Colors.AccentBlue, size);

    /// <summary>链接按钮</summary>
    public static bool LinkButton(string label, Vector2? size = null)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Theme.RadiusSM);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4, 2));
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Colors.AccentBlue);

        var clicked = ImGui.Button(label, size ?? Vector2.Zero);

        if (ImGui.IsItemHovered())
        {
            var dl = ImGui.GetWindowDrawList();
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            dl.AddLine(new Vector2(min.X, max.Y - 1), new Vector2(max.X, max.Y - 1),
                ColorU32(Theme.Colors.AccentBlue), 1);
        }

        ImGui.PopStyleColor(4);
        ImGui.PopStyleVar(2);
        return clicked;
    }

    // ── 内部: 填充按钮 ──
    private static bool FillButton(string label, Vector4 color, Vector2? size)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Theme.RadiusMD);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(15, 6));
        ImGui.PushStyleColor(ImGuiCol.Button, color);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Lighten(color, 0.12f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Darken(color, 0.12f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 1, 1, 1));

        var clicked = ImGui.Button(label, size ?? Vector2.Zero);

        ImGui.PopStyleColor(4);
        ImGui.PopStyleVar(2);
        return clicked;
    }

    // ── 内部: 边框按钮 ──
    private static bool OutlineButton(string label, Vector4 borderColor, Vector4 textColor, Vector2? size)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Theme.RadiusMD);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(15, 6));
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.Colors.BgHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.Colors.FillSecondary);
        ImGui.PushStyleColor(ImGuiCol.Border, borderColor);
        ImGui.PushStyleColor(ImGuiCol.Text, textColor);

        var clicked = ImGui.Button(label, size ?? Vector2.Zero);

        ImGui.PopStyleColor(5);
        ImGui.PopStyleVar(2);
        return clicked;
    }

    // ── 内部: Ghost 幽灵按钮 ──
    private static bool GhostButton(string label, Vector4 textColor, Vector2? size)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Theme.RadiusMD);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(15, 6));
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.Colors.BgHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.Colors.FillPrimary);
        ImGui.PushStyleColor(ImGuiCol.Text, textColor);

        var clicked = ImGui.Button(label, size ?? Vector2.Zero);

        ImGui.PopStyleColor(4);
        ImGui.PopStyleVar(2);
        return clicked;
    }

    /// <summary>旧版 Button（等同于 PrimaryButton）</summary>
    public static bool Button(string label, Vector2? size = null, bool disabled = false)
        => PrimaryButton(label, size);

    /// <summary>自定义颜色填充按钮</summary>
    public static bool AccentButton(string label, Vector4 color, Vector2? size = null)
        => FillButton(label, color, size);

    /// <summary>旧版 OutlineButton（等同于 DefaultButton）</summary>
    public static bool OutlineButton(string label, Vector2? size = null)
        => DefaultButton(label, size);

    // ═══════════════════════════════════════════════════
    // 图标 + 文字 组合按钮 (AntdUI 风格)
    // ═══════════════════════════════════════════════════

    /// <summary>图标 + 文字按钮 (例如 "▶ 启动")</summary>
    public static bool IconTextButton(IconType icon, string label, Vector4 color,
        Vector2? size = null, IconButtonStyle style = IconButtonStyle.Fill)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Theme.RadiusMD);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(12, 6));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, new Vector2(6, 0));

        Vector4 bg, bgHover, bgActive, borderCol, iconCol, textCol;

        if (style == IconButtonStyle.Fill)
        {
            bg = color;
            bgHover = Lighten(color, 0.12f);
            bgActive = Darken(color, 0.12f);
            borderCol = color;
            iconCol = new Vector4(1, 1, 1, 1);
            textCol = new Vector4(1, 1, 1, 1);
        }
        else if (style == IconButtonStyle.Outline)
        {
            bg = Vector4.Zero;
            bgHover = Theme.Colors.BgHover;
            bgActive = Theme.Colors.FillPrimary;
            borderCol = color;
            iconCol = color;
            textCol = color;
        }
        else
        {
            bg = Vector4.Zero;
            bgHover = Theme.Colors.BgHover;
            bgActive = Theme.Colors.FillPrimary;
            borderCol = Vector4.Zero;
            iconCol = color;
            textCol = color;
        }

        ImGui.PushStyleColor(ImGuiCol.Button, bg);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, bgHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, bgActive);
        if (style == IconButtonStyle.Outline)
            ImGui.PushStyleColor(ImGuiCol.Border, borderCol);
        ImGui.PushStyleColor(ImGuiCol.Text, textCol);

        var clicked = ImGui.Button($"##it_{icon}_{label}", size ?? Vector2.Zero);

        // 在按钮区域内绘制图标 + 文字
        var rectMin = ImGui.GetItemRectMin();
        var rectMax = ImGui.GetItemRectMax();
        var center = (rectMin + rectMax) / 2;
        var textSize = ImGui.CalcTextSize(label);
        var totalW = 14 + 4 + textSize.X; // icon + gap + text
        var startX = center.X - totalW / 2 + 7;

        // 绘制图标
        DrawIcon(ImGui.GetWindowDrawList(), new Vector2(startX, center.Y), icon, ColorU32(iconCol));

        // 绘制文字
        ImGui.GetWindowDrawList().AddText(
            new Vector2(startX + 10, center.Y - textSize.Y / 2),
            ColorU32(textCol),
            label);

        if (style == IconButtonStyle.Outline)
            ImGui.PopStyleColor(5);
        else
            ImGui.PopStyleColor(4);
        ImGui.PopStyleVar(3);

        return clicked;
    }

    // ═══════════════════════════════════════════════════
    // 分割竖线
    // ═══════════════════════════════════════════════════

    /// <summary>分割竖线</summary>
    public static void VSplit()
    {
        ImGui.TextColored(Theme.Colors.BorderSecondary, "│");
    }

    // ═══════════════════════════════════════════════════
    // 开关 (AntdUI Switch 风格)
    // ═══════════════════════════════════════════════════

    /// <summary>开关控件</summary>
    public static bool Switch(string id, ref bool value)
    {
        ImGui.PushStyleColor(ImGuiCol.FrameBg, value ? Theme.Colors.SwitchTrackOn : Theme.Colors.SwitchTrackOff);
        ImGui.PushStyleColor(ImGuiCol.CheckMark, Theme.Colors.SwitchKnob);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 12f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2, 2));

        var changed = ImGui.Checkbox($"##{id}", ref value);

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(2);
        return changed;
    }

    /// <summary>带标签的开关控件</summary>
    public static bool Switch(string id, string label, ref bool value)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Theme.Colors.TextPrimary, label);
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - 44);
        return Switch(id, ref value);
    }

    // ═══════════════════════════════════════════════════
    // 滑块
    // ═══════════════════════════════════════════════════

    /// <summary>滑块控件</summary>
    public static bool Slider(string id, string label, ref float value, float min, float max, string format = "%.1f")
    {
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.Colors.SliderRail);
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, Theme.Colors.SliderTrack);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, Theme.Colors.SliderTrack);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Colors.TextPrimary);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 60);
        var changed = ImGui.SliderFloat($"##{id}", ref value, min, max, format);
        ImGui.PopStyleColor(4);
        ImGui.SameLine();
        ImGui.TextColored(Theme.Colors.TextSecondary, label);
        return changed;
    }

    /// <summary>整数滑块控件</summary>
    public static bool SliderInt(string id, string label, ref int value, int min, int max)
    {
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.Colors.SliderRail);
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, Theme.Colors.SliderTrack);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, Theme.Colors.SliderTrack);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Colors.TextPrimary);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 60);
        var changed = ImGui.SliderInt($"##{id}", ref value, min, max);
        ImGui.PopStyleColor(4);
        ImGui.SameLine();
        ImGui.TextColored(Theme.Colors.TextSecondary, label);
        return changed;
    }

    // ═══════════════════════════════════════════════════
    // 下拉选择器
    // ═══════════════════════════════════════════════════

    /// <summary>下拉选择器</summary>
    public static bool Select(string id, string label, ref int selectedIndex, string[] options)
    {
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.Colors.FillSecondary);
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

    // ═══════════════════════════════════════════════════
    // 标签页
    // ═══════════════════════════════════════════════════

    /// <summary>标签页控件</summary>
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

    // ═══════════════════════════════════════════════════
    // 卡片
    // ═══════════════════════════════════════════════════

    private static int _cardCounter;

    /// <summary>开始卡片容器</summary>
    public static void CardBegin(string? title = null, Vector4? bgColor = null)
    {
        var cardId = title ?? $"card_{Interlocked.Increment(ref _cardCounter)}";
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, Theme.RadiusMD);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1f);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, bgColor ?? Theme.Colors.BgContainer);
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.Colors.BorderSecondary);
        ImGui.BeginChild($"##card_{cardId}", new Vector2(-1, 0), true);
        if (title != null)
        {
            ImGui.PushFont(UiBuilder.DefaultFont);
            ImGui.TextColored(Theme.Colors.TextPrimary, title);
            ImGui.PopFont();
            ImGui.Spacing();
        }
    }

    /// <summary>结束卡片容器</summary>
    public static void CardEnd()
    {
        ImGui.EndChild();
        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(2);
    }

    // ═══════════════════════════════════════════════════
    // 标签芯片 (Tag)
    // ═══════════════════════════════════════════════════

    /// <summary>标签芯片</summary>
    public static void Tag(string label, bool active, Vector4? activeColor = null, Vector4? inactiveColor = null)
    {
        var color = active ? (activeColor ?? Theme.Colors.AccentGreen) : (inactiveColor ?? Theme.Colors.FillSecondary);
        var textColor = active ? Theme.Colors.TagActiveText : Theme.Colors.TextSecondary;
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Theme.RadiusSM);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 4));
        ImGui.PushStyleColor(ImGuiCol.Button, color);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, color);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, color);
        ImGui.PushStyleColor(ImGuiCol.Text, textColor);
        ImGui.Button(label, Vector2.Zero);
        ImGui.PopStyleColor(4);
        ImGui.PopStyleVar(2);
    }

    // ═══════════════════════════════════════════════════
    // 分割线
    // ═══════════════════════════════════════════════════

    /// <summary>分割线</summary>
    public static void Divider()
    {
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Separator, Theme.Colors.BorderSecondary);
        ImGui.Separator();
        ImGui.PopStyleColor();
        ImGui.Spacing();
    }

    // ═══════════════════════════════════════════════════
    // Badge 徽标
    // ═══════════════════════════════════════════════════

    /// <summary>徽标</summary>
    public static void Badge(bool active, Vector4? activeColor = null)
    {
        var color = active ? (activeColor ?? Theme.Colors.AccentGreen) : Theme.Colors.TextTertiary;
        var dl = ImGui.GetWindowDrawList();
        var cursor = ImGui.GetCursorScreenPos();
        var lineHeight = ImGui.GetTextLineHeight();
        var centerY = cursor.Y + lineHeight / 2;
        dl.AddCircleFilled(new Vector2(cursor.X + 6, centerY), 4, ColorU32(color));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 16);
    }

    // ═══════════════════════════════════════════════════
    // 数字输入
    // ═══════════════════════════════════════════════════

    /// <summary>数字输入框</summary>
    public static bool InputNumber(string id, string label, ref int value, int step = 1, int stepFast = 10)
    {
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Theme.Colors.FillSecondary);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Theme.RadiusSM);
        ImGui.SetNextItemWidth(80);
        var changed = ImGui.InputInt($"##{id}", ref value, step, stepFast);
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.TextColored(Theme.Colors.TextPrimary, label);
        return changed;
    }

    // ═══════════════════════════════════════════════════
    // 文本标签
    // ═══════════════════════════════════════════════════

    /// <summary>文本标签</summary>
    public static void Label(string text)
    {
        ImGui.TextColored(Theme.Colors.TextPrimary, text);
    }
}