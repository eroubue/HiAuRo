using System.Numerics;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// Ant Design 5 设计令牌 — 亮色/暗色双主题
/// 参考: https://ant.design/docs/spec/colors-cn
/// </summary>
public static class Theme
{
    private static Vector4 Hex(uint hex) => new(
        ((hex >> 16) & 0xFF) / 255f,
        ((hex >> 8) & 0xFF) / 255f,
        (hex & 0xFF) / 255f,
        ((hex >> 24) & 0xFF) / 255f
    );

    private static Vector4 Rgba(float r, float g, float b, float a) => new(r, g, b, a);

    // ── 主题切换 ──

    public enum ThemeMode { Light = 0, Dark = 1 }

    private static ThemeMode _mode = ThemeMode.Light;
    public static ThemeMode Mode
    {
        get => _mode;
        set { _mode = value; Tokens = value == ThemeMode.Light ? Light : Dark; }
    }

    public static ThemeTokens Tokens { get; private set; } = Light!;

    static Theme() { }

    // ── 亮色令牌 (Ant Design 5 默认) ──

    public static readonly ThemeTokens Light = new()
    {
        BgBase        = Hex(0xFFFFFFFF),
        BgLayout      = Hex(0xFFF5F5F5),
        BgContainer   = Hex(0xFFFFFFFF),
        BgElevated    = Hex(0xFFFFFFFF),
        BgHover       = Rgba(0, 0, 0, 0.06f),
        BgSpotlight   = Hex(0xFFFFFFFF),

        // 毛玻璃三层: 底色 + 高光 + 暗部
        GlassBg       = Rgba(1f, 1f, 1f, 0.72f),      // 半透明白底
        GlassHighlight = Rgba(1f, 1f, 1f, 0.25f),     // 顶部高光
        GlassShade     = Rgba(0, 0, 0, 0.03f),        // 底部暗部
        GlassBorder    = Rgba(0, 0, 0, 0.08f),        // 细边框
        GlassShadow    = Rgba(0, 0, 0, 0.10f),        // 投影

        TextPrimary   = Rgba(0, 0, 0, 0.88f),
        TextSecondary = Rgba(0, 0, 0, 0.65f),
        TextTertiary  = Rgba(0, 0, 0, 0.45f),

        AccentBlue    = Hex(0xFF1677FF),
        AccentGreen   = Hex(0xFF52C41A),
        AccentRed     = Hex(0xFFFF4D4F),
        AccentOrange  = Hex(0xFFFAAD14),

        Border        = Hex(0xFFD9D9D9),
        BorderSecondary = Hex(0xFFF0F0F0),

        FillPrimary   = Rgba(0, 0, 0, 0.18f),
        FillSecondary = Rgba(0, 0, 0, 0.06f),
        FillTertiary  = Rgba(0, 0, 0, 0.04f),

        SwitchTrackOn = Hex(0xFF1677FF),
        SwitchTrackOff = Hex(0xFFBFBFBF),
        SwitchKnob    = Hex(0xFFFFFFFF),

        SliderTrack   = Hex(0xFF1677FF),
        SliderRail    = Hex(0xFFF0F0F0),

        SidebarActive = Hex(0xFFE6F4FF),
        SidebarActiveBorder = Hex(0xFF1677FF),

        TagActiveText = Hex(0xFFFFFFFF),
    };

    // ── 暗色令牌 ──

    public static readonly ThemeTokens Dark = new()
    {
        BgBase        = Hex(0xFF000000),
        BgLayout      = Hex(0xFF000000),
        BgContainer   = Hex(0xFF141414),
        BgElevated    = Hex(0xFF1F1F1F),
        BgHover       = Rgba(1f, 1f, 1f, 0.06f),
        BgSpotlight   = Hex(0xFF424242),

        // 毛玻璃三层: 底色 + 高光 + 暗部
        GlassBg       = Rgba(0.10f, 0.10f, 0.12f, 0.72f),  // 半透明暗底(偏蓝)
        GlassHighlight = Rgba(1f, 1f, 1f, 0.06f),           // 顶部微高光
        GlassShade     = Rgba(0, 0, 0, 0.20f),              // 底部暗部
        GlassBorder    = Rgba(1f, 1f, 1f, 0.08f),           // 细边框
        GlassShadow    = Rgba(0, 0, 0, 0.30f),              // 投影

        TextPrimary   = Rgba(1f, 1f, 1f, 0.85f),
        TextSecondary = Rgba(1f, 1f, 1f, 0.65f),
        TextTertiary  = Rgba(1f, 1f, 1f, 0.45f),

        AccentBlue    = Hex(0xFF1668DC),
        AccentGreen   = Hex(0xFF30D158),
        AccentRed     = Hex(0xFFFF453A),
        AccentOrange  = Hex(0xFFFF9F0A),

        Border        = Hex(0xFF424242),
        BorderSecondary = Hex(0xFF303030),

        FillPrimary   = Rgba(1f, 1f, 1f, 0.15f),
        FillSecondary = Rgba(1f, 1f, 1f, 0.12f),
        FillTertiary  = Rgba(1f, 1f, 1f, 0.08f),

        SwitchTrackOn = Hex(0xFF1668DC),
        SwitchTrackOff = Hex(0xFF424242),
        SwitchKnob    = Hex(0xFFFFFFFF),

        SliderTrack   = Hex(0xFF1668DC),
        SliderRail    = Hex(0xFF303030),

        SidebarActive = Rgba(0.09f, 0.41f, 0.87f, 0.2f),
        SidebarActiveBorder = Hex(0xFF1668DC),

        TagActiveText = Hex(0xFFFFFFFF),
    };

    // ── 动态颜色代理 (Theme.Colors.XXX 自动跟随当前主题) ──

    public static class Colors
    {
        public static Vector4 BgLayout        => Tokens.BgLayout;
        public static Vector4 BgContainer     => Tokens.BgContainer;
        public static Vector4 BgElevated      => Tokens.BgElevated;
        public static Vector4 BgHover         => Tokens.BgHover;
        public static Vector4 BgSpotlight     => Tokens.BgSpotlight;

        public static Vector4 GlassBg         => Tokens.GlassBg;
        public static Vector4 GlassHighlight  => Tokens.GlassHighlight;
        public static Vector4 GlassShade      => Tokens.GlassShade;
        public static Vector4 GlassBorder     => Tokens.GlassBorder;
        public static Vector4 GlassShadow     => Tokens.GlassShadow;

        public static Vector4 TextPrimary     => Tokens.TextPrimary;
        public static Vector4 TextSecondary   => Tokens.TextSecondary;
        public static Vector4 TextTertiary    => Tokens.TextTertiary;

        public static Vector4 AccentBlue      => Tokens.AccentBlue;
        public static Vector4 AccentGreen     => Tokens.AccentGreen;
        public static Vector4 AccentRed       => Tokens.AccentRed;
        public static Vector4 AccentOrange    => Tokens.AccentOrange;

        public static Vector4 Border          => Tokens.Border;
        public static Vector4 BorderSecondary => Tokens.BorderSecondary;
        public static Vector4 BorderActive    => Tokens.AccentBlue;

        public static Vector4 FillPrimary     => Tokens.FillPrimary;
        public static Vector4 FillSecondary   => Tokens.FillSecondary;
        public static Vector4 FillTertiary    => Tokens.FillTertiary;

        public static Vector4 SwitchTrackOn   => Tokens.SwitchTrackOn;
        public static Vector4 SwitchTrackOff  => Tokens.SwitchTrackOff;
        public static Vector4 SwitchKnob      => Tokens.SwitchKnob;

        public static Vector4 SliderTrack     => Tokens.SliderTrack;
        public static Vector4 SliderRail      => Tokens.SliderRail;

        public static Vector4 SidebarActive      => Tokens.SidebarActive;
        public static Vector4 SidebarActiveBorder => Tokens.SidebarActiveBorder;

        public static Vector4 TagActiveText => Tokens.TagActiveText;
    }

    // ── 圆角 (Ant Design 5) ──

    public const float RadiusXS = 2f;
    public const float RadiusSM = 4f;
    public const float RadiusMD = 6f;
    public const float RadiusLG = 8f;

    // ── 间距 ──

    public static readonly Vector2 PaddingXS  = new(4, 2);
    public static readonly Vector2 PaddingSM  = new(8, 4);
    public static readonly Vector2 PaddingMD  = new(12, 8);
    public static readonly Vector2 ItemSpacing = new(8, 6);

    // ── 字号 ──

    public const float FontSizeSM = 11f;
    public const float FontSizeMD = 13f;
    public const float FontSizeLG = 16f;

    // ── 动画 ──

    public const float AnimSpeed = 12f;

    // ── 阴影 ──

    public const float ShadowOffset = 2f;
    public const float ShadowBlur = 8f;
}

/// <summary>
/// 主题令牌集合 — Ant Design 5 设计体系
/// </summary>
public class ThemeTokens
{
    // ── 背景 ──
    public Vector4 BgBase, BgLayout, BgContainer, BgElevated, BgHover, BgSpotlight;

    // ── 毛玻璃叠加 ──
    public Vector4 GlassBg, GlassHighlight, GlassShade, GlassBorder, GlassShadow;

    // ── 文字 ──
    public Vector4 TextPrimary, TextSecondary, TextTertiary;

    // ── 品牌色 ──
    public Vector4 AccentBlue, AccentGreen, AccentRed, AccentOrange;

    // ── 边框 ──
    public Vector4 Border, BorderSecondary;

    // ── 填充层级 ──
    public Vector4 FillPrimary, FillSecondary, FillTertiary;

    // ── 开关 ──
    public Vector4 SwitchTrackOn, SwitchTrackOff, SwitchKnob;

    // ── 滑块 ──
    public Vector4 SliderTrack, SliderRail;

    // ── 侧边栏 ──
    public Vector4 SidebarActive, SidebarActiveBorder;

    // ── 标签 ──
    public Vector4 TagActiveText;
}