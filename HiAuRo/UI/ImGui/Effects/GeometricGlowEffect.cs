using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

/// <summary>
/// 几何光效 — 赛博朋克风格动态霓虹线条：角落装饰 + 旋转多边形 + 扫描线 + 网格
/// </summary>
public sealed class GeometricGlowEffect
{
    private float _rotationAngle;
    private float _scanLineY;
    private float _scanDir = 1f;
    private const float ScanSpeed = 120f;
    private const float RotationSpeed = 15f * MathF.PI / 180f;
    private float _windowHeight;

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        var w = max.X - min.X;
        var h = max.Y - min.Y;
        _windowHeight = h;

        // 中心多边形旋转
        _rotationAngle += RotationSpeed * dt;

        // 扫描线往返
        _scanLineY += _scanDir * ScanSpeed * dt;
        if (_scanLineY > h)
        {
            _scanLineY = h;
            _scanDir = -1f;
        }
        else if (_scanLineY < 0f)
        {
            _scanLineY = 0f;
            _scanDir = 1f;
        }
    }

    public void Draw(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax)
    {
        dl.PushClipRect(winMin, winMax, true);

        var accent = Theme.Colors.AccentBlue;
        var tertiary = Theme.Colors.TextTertiary;

        DrawCornerDecos(dl, winMin, winMax, accent);
        DrawCenterPolygon(dl, winMin, winMax, accent);
        DrawScanLine(dl, winMin, winMax, accent);
        DrawGrid(dl, winMin, winMax, tertiary);

        dl.PopClipRect();
    }

    /// <summary>四角 L 形装饰线</summary>
    private static void DrawCornerDecos(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 accent)
    {
        var len = 28f;
        var thick = 1.5f;
        var glowThick = 4f;

        // 角落位置和方向
        var corners = new (Vector2 origin, Vector2 dirH, Vector2 dirV)[]
        {
            (min, new Vector2(1, 0), new Vector2(0, 1)),
            (new Vector2(max.X, min.Y), new Vector2(-1, 0), new Vector2(0, 1)),
            (new Vector2(min.X, max.Y), new Vector2(1, 0), new Vector2(0, -1)),
            (max, new Vector2(-1, 0), new Vector2(0, -1)),
        };

        foreach (var (origin, dirH, dirV) in corners)
        {
            var hEnd = origin + dirH * len;
            var vEnd = origin + dirV * len;

            // 辉光层
            var glowColor = ImGui.ColorConvertFloat4ToU32(
                new Vector4(accent.X, accent.Y, accent.Z, 0.12f));
            dl.AddLine(origin, hEnd, glowColor, glowThick);
            dl.AddLine(origin, vEnd, glowColor, glowThick);

            // 主线条
            var mainColor = ImGui.ColorConvertFloat4ToU32(
                new Vector4(accent.X, accent.Y, accent.Z, 0.5f));
            dl.AddLine(origin, hEnd, mainColor, thick);
            dl.AddLine(origin, vEnd, mainColor, thick);
        }
    }

    /// <summary>中心旋转六边形</summary>
    private void DrawCenterPolygon(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 accent)
    {
        var center = (min + max) * 0.5f;
        var radius = Math.Min(max.X - min.X, max.Y - min.Y) * 0.2f;
        if (radius < 10f) return;

        var sides = 6;

        // 辉光层
        var glowColor = ImGui.ColorConvertFloat4ToU32(
            new Vector4(accent.X, accent.Y, accent.Z, 0.08f));
        DrawPolygon(dl, center, radius + 4f, sides, _rotationAngle, glowColor, 3f);

        // 主线条
        var mainColor = ImGui.ColorConvertFloat4ToU32(
            new Vector4(accent.X, accent.Y, accent.Z, 0.25f));
        DrawPolygon(dl, center, radius, sides, _rotationAngle, mainColor, 1.5f);

        // 内层小多边形（反方向旋转）
        var innerColor = ImGui.ColorConvertFloat4ToU32(
            new Vector4(accent.X, accent.Y, accent.Z, 0.15f));
        DrawPolygon(dl, center, radius * 0.5f, sides, -_rotationAngle * 0.7f, innerColor, 1f);
    }

    private static void DrawPolygon(ImDrawListPtr dl, Vector2 center, float radius, int sides, float angle, uint color, float thickness)
    {
        for (var i = 0; i < sides; i++)
        {
            var a1 = angle + i * MathF.Tau / sides;
            var a2 = angle + ((i + 1) % sides) * MathF.Tau / sides;
            var p1 = center + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius;
            var p2 = center + new Vector2(MathF.Cos(a2), MathF.Sin(a2)) * radius;
            dl.AddLine(p1, p2, color, thickness);
        }
    }

    /// <summary>水平扫描线（CRT 效果）</summary>
    private void DrawScanLine(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 accent)
    {
        var y = min.Y + _scanLineY;
        if (y < min.Y || y > max.Y) return;

        var scanColor = ImGui.ColorConvertFloat4ToU32(
            new Vector4(accent.X, accent.Y, accent.Z, 0.3f));
        var glowColor = ImGui.ColorConvertFloat4ToU32(
            new Vector4(accent.X, accent.Y, accent.Z, 0.08f));

        dl.AddLine(new Vector2(min.X, y), new Vector2(max.X, y), glowColor, 8f);
        dl.AddLine(new Vector2(min.X, y), new Vector2(max.X, y), scanColor, 1f);
    }

    /// <summary>淡色细网格背景（HUD overlay）</summary>
    private static void DrawGrid(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 tertiary)
    {
        var gridColor = ImGui.ColorConvertFloat4ToU32(
            new Vector4(tertiary.X, tertiary.Y, tertiary.Z, 0.04f));
        var spacing = 40f;

        for (var x = min.X + spacing; x < max.X; x += spacing)
            dl.AddLine(new Vector2(x, min.Y), new Vector2(x, max.Y), gridColor, 0.5f);

        for (var y = min.Y + spacing; y < max.Y; y += spacing)
            dl.AddLine(new Vector2(min.X, y), new Vector2(max.X, y), gridColor, 0.5f);
    }
}
