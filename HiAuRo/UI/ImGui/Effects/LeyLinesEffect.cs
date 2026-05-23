using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

public sealed class LeyLinesEffect
{
    private float _rotationAngle;
    private float _time;
    private float _glitchTimer;
    private bool _inGlitch;
    private float _glitchDuration;
    private readonly Random _rng = new();

    private const int Segments = 64;
    private static readonly int[] PentagramOrder = [0, 2, 4, 1, 3];

    private static readonly Vector3 Purple = new(0.4f, 0.1f, 0.6f);
    private static readonly Vector3 Cyan = new(0f, 1f, 1f);
    private static readonly Vector3 Magenta = new(1f, 0f, 1f);
    private static readonly Vector3 Orange = new(1f, 0.6f, 0.1f);

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        _time += dt;
        _rotationAngle += 1f * dt;

        if (_inGlitch)
        {
            _glitchDuration -= dt;
            if (_glitchDuration <= 0f) _inGlitch = false;
        }
        else
        {
            _glitchTimer += dt;
            if (_glitchTimer > 2f + (float)_rng.NextDouble())
            {
                _inGlitch = true;
                _glitchDuration = 0.05f + (float)_rng.NextDouble() * 0.1f;
                _glitchTimer = 0f;
            }
        }
    }

    public void Draw(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax)
    {
        dl.PushClipRect(winMin, winMax, true);

        var center = (winMin + winMax) * 0.5f;
        var w = winMax.X - winMin.X;
        var h = winMax.Y - winMin.Y;
        var R = Math.Min(w, h) * 0.35f;

        var mouse = ImGui.GetIO().MousePos;
        var relX = Math.Clamp((mouse.X - center.X) / (w * 0.5f), -1f, 1f);
        var relY = Math.Clamp((mouse.Y - center.Y) / (h * 0.5f), -1f, 1f);
        var angleY = relX * 0.5f;
        var angleX = relY * 0.3f;
        var cosY = MathF.Cos(angleY);
        var sinY = MathF.Sin(angleY);
        var cosX = MathF.Cos(angleX);
        var sinX = MathF.Sin(angleX);

        Vector2 Project(float x, float y) => new(
            center.X + x * cosY,
            center.Y + y * cosX - x * sinY * sinX);

        var mouseDist = Vector2.Distance(mouse, center);
        var mouseProximity = mouseDist < R * 1.2f ? 1.3f : 1f;
        var breathe = 0.85f + 0.15f * MathF.Sin(_time * 2f);
        var alphaMod = breathe * mouseProximity;
        if (_inGlitch) alphaMod *= 0.1f;

        float A(float a) => Math.Min(1f, a * alphaMod);

        // 底层蓝色渐变（上半部）
        for (var i = 0; i < 6; i++)
        {
            var t0 = (float)i / 6 * 0.5f;
            var t1 = (float)(i + 1) / 6 * 0.5f;
            var alpha = 0.06f * (1f - (float)i / 6);
            dl.AddRectFilled(
                new Vector2(winMin.X, winMin.Y + h * t0),
                new Vector2(winMax.X, winMin.Y + h * t1),
                U32(new Vector3(0.1f, 0.2f, 0.6f), A(alpha)));
        }

        // 扫描线干扰
        for (var i = 0; i < 8; i++)
        {
            var y = winMin.Y + (float)_rng.NextDouble() * h;
            var x = winMin.X + (float)_rng.NextDouble() * w;
            var len = 10f + (float)_rng.NextDouble() * 30f;
            dl.AddLine(new Vector2(x, y), new Vector2(x + len, y), U32(Cyan, A((float)_rng.NextDouble() * 0.25f)));
        }

        // 故障撕裂条纹
        if (_inGlitch)
        {
            for (var i = 0; i < 3; i++)
            {
                var y = winMin.Y + (float)_rng.NextDouble() * h;
                dl.AddRectFilled(
                    new Vector2(winMin.X, y),
                    new Vector2(winMax.X, y + 2f),
                    U32(Magenta, A(0.08f)));
            }
        }

        // 底层光晕
        dl.AddCircleFilled(center, R * 1.3f, U32(Purple, A(0.04f)), 64);
        dl.AddCircleFilled(center, R * 1.1f, U32(Purple, A(0.03f)), 64);

        // 三层同心环（采样 → 3D投影 → 连线 + 色差）
        DrawRing(dl, Project, R, A(0.2f), 3f);
        DrawRing(dl, Project, R * 0.7f, A(0.15f), 2f);
        DrawRing(dl, Project, R * 0.4f, A(0.15f), 2f);

        // 五芒星顶点
        var outerPts = new Vector2[5];
        for (var i = 0; i < 5; i++)
        {
            var angle = _rotationAngle + i * 72f * MathF.PI / 180f - MathF.PI / 2f;
            outerPts[i] = Project(MathF.Cos(angle) * R * 0.4f, MathF.Sin(angle) * R * 0.4f);
        }

        // 五芒星
        var pentPts = new Vector2[6];
        for (var i = 0; i < 5; i++) pentPts[i] = outerPts[PentagramOrder[i]];
        pentPts[5] = pentPts[0];
        DrawPolylineChroma(dl, pentPts, Purple, A(0.2f), 2f);

        // 五片花瓣
        for (var i = 0; i < 5; i++)
        {
            var a = outerPts[i];
            var b = outerPts[(i + 2) % 5];
            var mid = (a + b) * 0.5f;
            var dirA = Vector2.Normalize(a - mid);
            var dirB = Vector2.Normalize(b - mid);
            var petalR = Vector2.Distance(a, mid) * 0.55f;

            dl.PathLineTo(mid);
            dl.PathArcTo(mid + dirA * petalR * 0.5f, petalR * 0.3f,
                MathF.Atan2(dirA.Y, dirA.X), MathF.Atan2(dirA.Y, dirA.X) + MathF.PI * 0.5f, 8);
            dl.PathArcTo(mid + dirB * petalR * 0.5f, petalR * 0.3f,
                MathF.Atan2(dirB.Y, dirB.X), MathF.Atan2(dirB.Y, dirB.X) + MathF.PI * 0.5f, 8);
            dl.PathFillConvex(U32(Magenta, A(0.08f)));
        }

        // 5条辐射连接线
        for (var i = 0; i < 5; i++)
        {
            var angle = _rotationAngle + i * 72f * MathF.PI / 180f - MathF.PI / 2f;
            DrawLineChroma(dl, outerPts[i],
                Project(MathF.Cos(angle) * R, MathF.Sin(angle) * R), Purple, A(0.15f), 1.5f);
        }

        // 三角形扇区
        for (var i = 0; i < 5; i++)
        {
            var a1 = _rotationAngle + i * 72f * MathF.PI / 180f - MathF.PI / 2f;
            var a2 = a1 + 72f * MathF.PI / 180f;
            var midR = R * 0.7f;

            dl.PathLineTo(Project(MathF.Cos(a1) * midR, MathF.Sin(a1) * midR));
            for (var j = 0; j <= 8; j++)
            {
                var a = a1 + (a2 - a1) * j / 8f;
                dl.PathLineTo(Project(MathF.Cos(a) * R, MathF.Sin(a) * R));
            }
            dl.PathLineTo(Project(MathF.Cos(a2) * midR, MathF.Sin(a2) * midR));
            for (var j = 0; j <= 8; j++)
            {
                var a = a2 + (a1 - a2) * j / 8f;
                dl.PathLineTo(Project(MathF.Cos(a) * midR, MathF.Sin(a) * midR));
            }
            dl.PathFillConvex(U32(Purple, A(0.03f)));
        }

        // 10个顶点光点
        for (var i = 0; i < 5; i++)
            DrawDot(dl, outerPts[i], A);
        for (var i = 0; i < 5; i++)
        {
            var angle = _rotationAngle + i * 72f * MathF.PI / 180f - MathF.PI / 2f;
            DrawDot(dl, Project(MathF.Cos(angle) * R, MathF.Sin(angle) * R), A);
        }

        // 外圈光刺
        for (var i = 0; i < 5; i++)
        {
            var angle = _rotationAngle + i * 72f * MathF.PI / 180f - MathF.PI / 2f;
            var dx = MathF.Cos(angle);
            var dy = MathF.Sin(angle);
            DrawLineChroma(dl,
                Project(dx * R, dy * R),
                Project(dx * (R + 5f), dy * (R + 5f)),
                Orange, A(0.5f), 2f);
        }

        // 中心光点
        dl.AddCircleFilled(center, 3f, U32(new Vector3(1, 1, 1), A(0.5f)));
        dl.AddCircleFilled(center, 8f, U32(new Vector3(1, 1, 1), A(0.1f)));

        dl.PopClipRect();
    }

    private void DrawDot(ImDrawListPtr dl, Vector2 pt, Func<float, float> A)
    {
        dl.AddCircleFilled(pt, 4f, U32(Orange, A(0.5f)));
        dl.AddCircleFilled(pt, 7f, U32(Orange, A(0.15f)));
        dl.AddCircleFilled(pt + new Vector2(-2, 0), 3f, U32(Cyan, A(0.2f)));
        dl.AddCircleFilled(pt + new Vector2(2, 0), 3f, U32(Magenta, A(0.2f)));
    }

    private void DrawRing(ImDrawListPtr dl, Func<float, float, Vector2> project, float radius, float alpha, float thickness)
    {
        var pts = new Vector2[Segments + 1];
        for (var i = 0; i <= Segments; i++)
        {
            var a = i * MathF.Tau / Segments;
            pts[i] = project(MathF.Cos(a) * radius, MathF.Sin(a) * radius);
        }

        for (var i = 0; i < pts.Length - 1; i++)
            dl.AddLine(pts[i], pts[i + 1], U32(Purple, alpha * 0.15f), thickness * 2.5f);

        DrawPolylineChroma(dl, pts, Purple, alpha, thickness);
    }

    private static void DrawPolylineChroma(ImDrawListPtr dl, Vector2[] pts, Vector3 color, float alpha, float thickness)
    {
        for (var i = 0; i < pts.Length - 1; i++)
            dl.AddLine(pts[i] + new Vector2(-2, 0), pts[i + 1] + new Vector2(-2, 0), U32(Cyan, alpha * 0.5f), thickness);
        for (var i = 0; i < pts.Length - 1; i++)
            dl.AddLine(pts[i] + new Vector2(2, 0), pts[i + 1] + new Vector2(2, 0), U32(Magenta, alpha * 0.5f), thickness);
        for (var i = 0; i < pts.Length - 1; i++)
            dl.AddLine(pts[i], pts[i + 1], U32(color, alpha), thickness);
    }

    private static void DrawLineChroma(ImDrawListPtr dl, Vector2 a, Vector2 b, Vector3 color, float alpha, float thickness)
    {
        dl.AddLine(a + new Vector2(-2, 0), b + new Vector2(-2, 0), U32(Cyan, alpha * 0.5f), thickness);
        dl.AddLine(a + new Vector2(2, 0), b + new Vector2(2, 0), U32(Magenta, alpha * 0.5f), thickness);
        dl.AddLine(a, b, U32(color, alpha), thickness);
    }

    private static uint U32(Vector3 rgb, float a)
        => ImGui.ColorConvertFloat4ToU32(new Vector4(rgb.X, rgb.Y, rgb.Z, Math.Clamp(a, 0f, 1f)));
}
