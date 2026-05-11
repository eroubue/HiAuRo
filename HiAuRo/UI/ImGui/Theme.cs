using System.Numerics;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// Ant Design 5.0 设计令牌 — 色彩/间距/圆角/字体
/// </summary>
public static class Theme
{
    private static Vector4 Hex(uint hex) => new(
        ((hex >> 16) & 0xFF) / 255f,
        ((hex >> 8)  & 0xFF) / 255f,
        (hex         & 0xFF) / 255f,
        ((hex >> 24) & 0xFF) / 255f
    );

    public static class Colors
    {
        public static readonly Vector4 BgLayout    = Hex(0xFF141414);
        public static readonly Vector4 BgContainer = Hex(0xFF1C1C1E);
        public static readonly Vector4 BgElevated  = Hex(0xFF2A2A2E);
        public static readonly Vector4 BgHover     = Hex(0xFF333336);

        public static readonly Vector4 TextPrimary   = Hex(0xFFE8E8E8);
        public static readonly Vector4 TextSecondary = Hex(0xFFA0A0A0);
        public static readonly Vector4 TextTertiary  = Hex(0xFF808080);

        public static readonly Vector4 AccentBlue   = Hex(0xFF1677FF);
        public static readonly Vector4 AccentGreen  = Hex(0xFF30D158);
        public static readonly Vector4 AccentRed    = Hex(0xFFFF453A);
        public static readonly Vector4 AccentOrange = Hex(0xFFFF9F0A);

        public static readonly Vector4 Border       = Hex(0xFF333333);
        public static readonly Vector4 BorderActive = Hex(0xFF1677FF);
    }

    public const float RadiusXS = 4f;
    public const float RadiusSM = 6f;
    public const float RadiusMD = 8f;
    public const float RadiusLG = 12f;

    public static readonly Vector2 PaddingXS  = new(4, 2);
    public static readonly Vector2 PaddingSM  = new(8, 4);
    public static readonly Vector2 PaddingMD  = new(12, 8);
    public static readonly Vector2 ItemSpacing = new(8, 6);

    public const float FontSizeSM = 11f;
    public const float FontSizeMD = 13f;
    public const float FontSizeLG = 16f;

    public const float AnimSpeed = 12f; // lerp 速度
}
