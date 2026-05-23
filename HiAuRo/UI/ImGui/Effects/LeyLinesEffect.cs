using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

public sealed class LeyLinesEffect
{
    private float _rotationAngle;
    private float _tiltAngle;
    private float _time;

    private const float TiltSpeed = 4f * MathF.PI / 180f;
    private const int EllipseSegments = 64;
    private static readonly int[] PentagramOrder = [0, 2, 4, 1, 3];

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        _time += dt;
        _rotationAngle += 1f * dt;
        _tiltAngle += TiltSpeed * dt;
    }

    public void Draw(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax)
    {
        dl.PushClipRect(winMin, winMax, true);

        var center = (winMin + winMax) * 0.5f;
        var w = winMax.X - winMin.X;
        var h = winMax.Y - winMin.Y;
        var R = Math.Min(w, h) * 0.35f;

        var cosTilt = MathF.Cos(_tiltAngle);

        Vector2 P(Vector2 p) => new(center.X + (p.X - center.X) * cosTilt, p.Y);

        var isDark = Theme.Mode == Theme.ThemeMode.Dark;
        var purple = isDark ? new Vector3(0.4f, 0.1f, 0.6f) : new Vector3(0.3f, 0.15f, 0.5f);
        var blue = isDark ? new Vector3(0.2f, 0.3f, 0.8f) : new Vector3(0.15f, 0.25f, 0.7f);
        var magenta = isDark ? new Vector3(0.7f, 0.2f, 0.6f) : new Vector3(0.6f, 0.2f, 0.5f);
        var orange = isDark ? new Vector3(1f, 0.6f, 0.1f) : new Vector3(0.9f, 0.5f, 0.1f);

        var mouse = ImGui.GetIO().MousePos;
        var mouseDist = Vector2.Distance(mouse, center);
        var mouseProximity = mouseDist < R * 1.2f ? 1.3f : 1f;

        var breathe = 0.85f + 0.15f * MathF.Sin(_time * 2f);
        var alphaMod = breathe * mouseProximity;

        float A(float baseA) => Math.Min(1f, baseA * alphaMod);

        // 第1层 — 底层光晕（透视不重要，保持 AddCircleFilled）
        dl.AddCircleFilled(center, R * 1.3f, U32(purple, A(0.06f)), 64);
        dl.AddCircleFilled(center, R * 1.1f, U32(purple, A(0.04f)), 64);

        // 第2层 — 三层同心椭圆环（带辉光）
        DrawEllipseRing(dl, center, R, cosTilt, purple, A(0.7f), 3f);
        DrawEllipseRing(dl, center, R * 0.7f, cosTilt, purple, A(0.5f), 2f);
        DrawEllipseRing(dl, center, R * 0.4f, cosTilt, purple, A(0.5f), 2f);

        // 预计算五芒星5个外顶点（经透视变换）
        var outerPts = new Vector2[5];
        for (var i = 0; i < 5; i++)
        {
            var angle = _rotationAngle + i * 72f * MathF.PI / 180f - MathF.PI / 2f;
            outerPts[i] = P(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (R * 0.4f));
        }

        // 第3层 — 五芒星
        var pentColor = new Vector3(
            (purple.X + blue.X) * 0.5f,
            (purple.Y + blue.Y) * 0.5f,
            (purple.Z + blue.Z) * 0.5f);
        dl.PathLineTo(outerPts[PentagramOrder[0]]);
        for (var i = 1; i < 5; i++)
            dl.PathLineTo(outerPts[PentagramOrder[i]]);
        dl.PathLineTo(outerPts[PentagramOrder[0]]);
        dl.PathStroke(U32(pentColor, A(0.6f)), ImDrawFlags.None, 2f);

        // 第5层 — 五片中心花瓣
        for (var i = 0; i < 5; i++)
        {
            var a = outerPts[i];
            var b = outerPts[(i + 2) % 5];
            var mid = (a + b) * 0.5f;
            var dirA = Vector2.Normalize(a - mid);
            var dirB = Vector2.Normalize(b - mid);
            var petalR = Vector2.Distance(a, mid) * 0.55f;

            dl.PathLineTo(mid);
            dl.PathArcTo(mid + dirA * petalR * 0.5f, petalR * 0.3f, MathF.Atan2(dirA.Y, dirA.X), MathF.Atan2(dirA.Y, dirA.X) + MathF.PI * 0.5f, 8);
            dl.PathArcTo(mid + dirB * petalR * 0.5f, petalR * 0.3f, MathF.Atan2(dirB.Y, dirB.X), MathF.Atan2(dirB.Y, dirB.X) + MathF.PI * 0.5f, 8);
            dl.PathFillConvex(U32(magenta, A(0.12f)));
        }

        // 第4层 — 5条辐射连接线（外顶点→外圈）
        for (var i = 0; i < 5; i++)
        {
            var angle = _rotationAngle + i * 72f * MathF.PI / 180f - MathF.PI / 2f;
            var inner = outerPts[i];
            var outer = P(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * R);

            dl.AddLine(inner, outer, U32(purple, A(0.35f)), 1.5f);
            dl.AddLine(inner, outer, U32(purple, A(0.12f)), 4f);
        }

        // 第6层 — 5个三角形扇区（用透视变换后的点分段绘制弧线）
        for (var i = 0; i < 5; i++)
        {
            var a1 = _rotationAngle + i * 72f * MathF.PI / 180f - MathF.PI / 2f;
            var a2 = a1 + 72f * MathF.PI / 180f;
            var midR = R * 0.7f;

            dl.PathLineTo(P(center + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * midR));
            DrawEllipseArc(dl, center, R, cosTilt, a1, a2, 8);
            dl.PathLineTo(P(center + new Vector2(MathF.Cos(a2), MathF.Sin(a2)) * midR));
            DrawEllipseArc(dl, center, midR, cosTilt, a2, a1, 8);
            dl.PathFillConvex(U32(purple, A(0.04f)));
        }

        // 第7层 — 10个顶点光点
        for (var i = 0; i < 5; i++)
        {
            var pt = outerPts[i];
            dl.AddCircleFilled(pt, 4f, U32(orange, A(0.8f)));
            dl.AddCircleFilled(pt, 7f, U32(orange, A(0.2f)));
        }

        for (var i = 0; i < 5; i++)
        {
            var angle = _rotationAngle + i * 72f * MathF.PI / 180f - MathF.PI / 2f;
            var pt = P(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * R);
            dl.AddCircleFilled(pt, 4f, U32(orange, A(0.8f)));
            dl.AddCircleFilled(pt, 7f, U32(orange, A(0.2f)));
        }

        // 第8层 — 外圈光刺
        for (var i = 0; i < 5; i++)
        {
            var angle = _rotationAngle + i * 72f * MathF.PI / 180f - MathF.PI / 2f;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var basePt = P(center + dir * R);
            var tipPt = P(center + dir * (R + 5f));
            dl.AddLine(basePt, tipPt, U32(orange, A(0.7f)), 2f);
        }

        // 第9层 — 中心光点
        dl.AddCircleFilled(center, 3f, U32(new Vector3(1, 1, 1), A(0.9f)));
        dl.AddCircleFilled(center, 8f, U32(new Vector3(1, 1, 1), A(0.15f)));

        dl.PopClipRect();
    }

    private static void DrawEllipseRing(ImDrawListPtr dl, Vector2 center, float radius, float cosTilt, Vector3 color, float alpha, float thickness)
    {
        DrawEllipsePath(dl, center, radius + thickness, cosTilt);
        dl.PathStroke(U32(color, alpha * 0.15f), ImDrawFlags.None, thickness * 2.5f);
        DrawEllipsePath(dl, center, radius, cosTilt);
        dl.PathStroke(U32(color, alpha), ImDrawFlags.None, thickness);
    }

    private static void DrawEllipsePath(ImDrawListPtr dl, Vector2 center, float radius, float cosTilt)
    {
        var rx = radius * cosTilt;
        for (var i = 0; i < EllipseSegments; i++)
        {
            var a = i * MathF.Tau / EllipseSegments;
            dl.PathLineTo(new Vector2(center.X + MathF.Cos(a) * rx, center.Y + MathF.Sin(a) * radius));
        }
    }

    private static void DrawEllipseArc(ImDrawListPtr dl, Vector2 center, float radius, float cosTilt, float startAngle, float endAngle, int segments)
    {
        var rx = radius * cosTilt;
        var step = (endAngle - startAngle) / segments;
        for (var i = 0; i <= segments; i++)
        {
            var a = startAngle + step * i;
            dl.PathLineTo(new Vector2(center.X + MathF.Cos(a) * rx, center.Y + MathF.Sin(a) * radius));
        }
    }

    private static uint U32(Vector3 rgb, float a)
        => ImGui.ColorConvertFloat4ToU32(new Vector4(rgb.X, rgb.Y, rgb.Z, Math.Clamp(a, 0f, 1f)));
}
