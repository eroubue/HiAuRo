using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

/// <summary>
/// 霓虹网格特效 — Synthwave/Outrun 风格透视网格地面 + 扫描线
/// </summary>
public sealed class NeonGridEffect
{
    private float _scrollOffset;
    private float _time;

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        _time += dt;
        _scrollOffset += dt * 40f;
    }

    public void Draw(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax)
    {
        dl.PushClipRect(winMin, winMax, true);

        var isLight = Theme.Mode == Theme.ThemeMode.Light;
        var purple = isLight ? new Vector4(0.5f, 0f, 0.5f, 1f) : new Vector4(0.6f, 0.1f, 0.8f, 1f);
        var cyan = isLight ? new Vector4(0f, 0.8f, 0.8f, 1f) : new Vector4(0f, 1f, 1f, 1f);
        var orange = isLight ? new Vector4(1f, 0.4f, 0f, 1f) : new Vector4(1f, 0.3f, 0f, 1f);

        DrawSun(dl, winMin, winMax, orange, purple);
        DrawPerspectiveGrid(dl, winMin, winMax, cyan, purple);
        DrawScanLines(dl, winMin, winMax, purple);

        dl.PopClipRect();
    }

    private void DrawSun(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 orange, Vector4 purple)
    {
        var center = new Vector2((min.X + max.X) * 0.5f, min.Y + (max.Y - min.Y) * 0.35f);
        var sunRadius = Math.Min(max.X - min.X, max.Y - min.Y) * 0.15f;
        if (sunRadius < 5f) return;

        var steps = 12;
        for (var i = steps; i >= 0; i--)
        {
            var ratio = (float)i / steps;
            var r = sunRadius * ratio;
            var color = Vector4.Lerp(purple, orange, ratio);
            color.W = 0.04f + ratio * 0.06f;
            dl.AddCircleFilled(center, r, ImGui.ColorConvertFloat4ToU32(color));
        }
    }

    private void DrawPerspectiveGrid(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 cyan, Vector4 purple)
    {
        var w = max.X - min.X;
        var h = max.Y - min.Y;
        var vanishY = min.Y - h * 0.1f;
        var bottomY = max.Y;
        var centerX = (min.X + max.X) * 0.5f;

        // 水平线（透视：底部密、顶部稀）
        var hLineCount = 30;
        var gridRange = bottomY - vanishY;
        for (var i = 1; i <= hLineCount; i++)
        {
            // 用指数分布让底部更密
            var t = (float)i / hLineCount;
            var y = bottomY - gridRange * t * t;
            if (y < min.Y || y > max.Y) continue;

            // 位移滚动
            y -= _scrollOffset % (gridRange / hLineCount);
            if (y < min.Y || y > max.Y) continue;

            var alpha = t * 0.3f;
            var lineColor = Vector4.Lerp(purple, cyan, t);
            lineColor.W = alpha;
            var color = ImGui.ColorConvertFloat4ToU32(lineColor);
            dl.AddLine(new Vector2(min.X, y), new Vector2(max.X, y), color, 1f);
        }

        // 竖线（从消失点发散）
        var vLineCount = 15;
        var spreadAtBottom = w * 0.8f;
        for (var i = -vLineCount / 2; i <= vLineCount / 2; i++)
        {
            var t = (float)i / (vLineCount / 2);
            var bottomX = centerX + t * spreadAtBottom * 0.5f;

            var alpha = 0.15f * (1f - MathF.Abs(t) * 0.5f);
            var lineColor = purple;
            lineColor.W = alpha;
            var color = ImGui.ColorConvertFloat4ToU32(lineColor);
            dl.AddLine(new Vector2(centerX, vanishY), new Vector2(bottomX, bottomY), color, 1f);
        }
    }

    private void DrawScanLines(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 purple)
    {
        var spacing = 6f;
        var scanColor = new Vector4(purple.X, purple.Y, purple.Z, 0.04f);
        var color = ImGui.ColorConvertFloat4ToU32(scanColor);

        for (var y = min.Y; y < max.Y; y += spacing)
            dl.AddLine(new Vector2(min.X, y), new Vector2(max.X, y), color, 0.5f);
    }
}
