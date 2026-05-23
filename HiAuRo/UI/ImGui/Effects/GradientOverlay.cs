using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

/// <summary>
/// 渐变纹理背景 — 分段绘制法实现 ImDrawList 上的渐变效果
/// </summary>
public static class GradientOverlay
{
    /// <summary>
    /// 绘制垂直渐变（从顶部到底部）
    /// 使用分段绘制法，将区域横向切 N 条逐条变色
    /// </summary>
    public static void DrawVertical(ImDrawListPtr dl, Vector2 min, Vector2 max,
        Vector4 topColor, Vector4 bottomColor, int segments = 32)
    {
        var height = max.Y - min.Y;
        if (height <= 0f) return;

        var segHeight = height / segments;

        for (var i = 0; i < segments; i++)
        {
            var t = (float)i / (segments - 1);
            var color = new Vector4(
                AnimationHelper.Lerp(topColor.X, bottomColor.X, t),
                AnimationHelper.Lerp(topColor.Y, bottomColor.Y, t),
                AnimationHelper.Lerp(topColor.Z, bottomColor.Z, t),
                AnimationHelper.Lerp(topColor.W, bottomColor.W, t));

            var yMin = min.Y + i * segHeight;
            var yMax = min.Y + (i + 1) * segHeight;
            dl.AddRectFilled(
                new Vector2(min.X, yMin),
                new Vector2(max.X, yMax),
                ImGui.ColorConvertFloat4ToU32(color));
        }
    }

    /// <summary>
    /// 绘制主题渐变 — 使用当前主题的 GlassHighlight → GlassShade
    /// </summary>
    public static void DrawThemeGradient(ImDrawListPtr dl, Vector2 min, Vector2 max, int segments = 32)
    {
        DrawVertical(dl, min, max, Theme.Colors.GlassHighlight, Theme.Colors.GlassShade, segments);
    }
}
