using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

public sealed class LeyLinesEffect
{
    private const float Perspective = 400f;

    private const float R = 1.0f;
    private const float StarR = R * 0.85f;
    private const float L3SmallR = R * 0.12f;
    private const float L4SmallR = R * 0.07f;
    private const float L5R = R * 0.35f;
    private const float CenterSquareR = R * 0.08f;
    private const float CenterCircleR = R * 0.05f;

    private static readonly Vector2 CyanOff = new(-2f, 0);
    private static readonly Vector2 MagOff = new(2f, 0);

    private float _rotX;
    private float _rotY;
    private float _time;
    private float _flickerTimer = 2f;
    private bool _inFlicker;
    private float _flickerAlpha = 0.2f;

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        _time += dt;
        _rotY += dt * 0.8f;
        _rotX += dt * 0.3f;

        var mouse = ImGui.GetIO().MousePos;
        var center = (min + max) * 0.5f;
        var inWindow = mouse.X >= min.X && mouse.X <= max.X && mouse.Y >= min.Y && mouse.Y <= max.Y;
        if (inWindow)
        {
            var dx = (mouse.X - center.X) / Math.Max(1f, (max.X - min.X) * 0.5f);
            var dy = (mouse.Y - center.Y) / Math.Max(1f, (max.Y - min.Y) * 0.5f);
            _rotY += dx * dt * 1.5f;
            _rotX += dy * dt * 1.5f;
        }

        _flickerTimer -= dt;
        if (_flickerTimer <= 0f)
        {
            _flickerTimer = 2f + Random.Shared.NextSingle() * 1f;
            _inFlicker = true;
        }

        if (_inFlicker)
        {
            _flickerAlpha = Random.Shared.NextSingle() < 0.3f
                ? 0.05f
                : 0.15f + Random.Shared.NextSingle() * 0.15f;
            _flickerTimer -= dt;
            if (_flickerTimer <= 0f || Random.Shared.NextSingle() < 0.1f)
            {
                _inFlicker = false;
                _flickerAlpha = 0.2f;
                _flickerTimer = 2f + Random.Shared.NextSingle() * 1f;
            }
        }
        else
        {
            _flickerAlpha = 0.2f + MathF.Sin(_time * 0.5f) * 0.05f;
        }
    }

    public void Draw(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax)
    {
        dl.PushClipRect(winMin, winMax, true);

        var center = (winMin + winMax) * 0.5f;
        var scale = Math.Min(winMax.X - winMin.X, winMax.Y - winMin.Y) * 0.20f;
        var alpha = _flickerAlpha;

        DrawBaseGradient(dl, winMin, winMax);

        DrawL1_OuterRing(dl, center, scale, alpha);
        DrawL2_Octagram(dl, center, scale, alpha);
        DrawL3_OuterSmallCircles(dl, center, scale, alpha);
        DrawL4_InnerSmallCircles(dl, center, scale, alpha);
        DrawL5_EnclosingCircle(dl, center, scale, alpha);
        DrawL6_Center(dl, center, scale, alpha);

        DrawScanNoise(dl, winMin, winMax);

        dl.PopClipRect();
    }

    private Vector2 Proj(Vector3 v, Vector2 center)
    {
        return Project(v, center, _rotX, _rotY);
    }

    private static Vector2 Project(Vector3 v, Vector2 center, float rotX, float rotY)
    {
        var cosY = MathF.Cos(rotY);
        var sinY = MathF.Sin(rotY);
        var x1 = v.X * cosY - v.Z * sinY;
        var z1 = v.X * sinY + v.Z * cosY;

        var cosX = MathF.Cos(rotX);
        var sinX = MathF.Sin(rotX);
        var y1 = v.Y * cosX - z1 * sinX;
        var z2 = v.Y * sinX + z1 * cosX;

        var s = Perspective / (Perspective + z2);
        return center + new Vector2(x1, y1) * s;
    }

    private void DrawPolyline(ImDrawListPtr dl, Vector2[] pts, float alpha, float thickness)
    {
        for (var i = 0; i < pts.Length - 1; i++)
            dl.AddLine(pts[i] + CyanOff, pts[i + 1] + CyanOff,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, alpha * 0.5f)), thickness);
        for (var i = 0; i < pts.Length - 1; i++)
            dl.AddLine(pts[i] + MagOff, pts[i + 1] + MagOff,
                ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 1, alpha * 0.5f)), thickness);
        for (var i = 0; i < pts.Length - 1; i++)
            dl.AddLine(pts[i], pts[i + 1],
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.7f, 0.8f, 1f, alpha)), thickness);
    }

    private void DrawLine(ImDrawListPtr dl, Vector2 a, Vector2 b, float alpha, float thickness)
    {
        dl.AddLine(a + CyanOff, b + CyanOff,
            ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, alpha * 0.5f)), thickness);
        dl.AddLine(a + MagOff, b + MagOff,
            ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 1, alpha * 0.5f)), thickness);
        dl.AddLine(a, b,
            ImGui.ColorConvertFloat4ToU32(new Vector4(0.7f, 0.8f, 1f, alpha)), thickness);
    }

    private void DrawFilledDot(ImDrawListPtr dl, Vector2 p, float radius, float alpha)
    {
        dl.AddCircleFilled(p + CyanOff, radius,
            ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, alpha * 0.5f)));
        dl.AddCircleFilled(p + MagOff, radius,
            ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 1, alpha * 0.5f)));
        dl.AddCircleFilled(p, radius,
            ImGui.ColorConvertFloat4ToU32(new Vector4(0.7f, 0.8f, 1f, alpha)));
    }

    private void DrawL1_OuterRing(ImDrawListPtr dl, Vector2 center, float scale, float alpha)
    {
        const int segs = 64;
        var pts = new Vector2[segs + 1];
        for (var i = 0; i <= segs; i++)
        {
            var a = i * MathF.Tau / segs;
            pts[i] = Proj(new Vector3(MathF.Cos(a) * R * scale, MathF.Sin(a) * R * scale, 0), center);
        }
        DrawPolyline(dl, pts, alpha, 2.5f);
    }

    private void DrawL2_Octagram(ImDrawListPtr dl, Vector2 center, float scale, float alpha)
    {
        var sq1 = new Vector2[4];
        for (var i = 0; i < 4; i++)
        {
            var a = i * MathF.PI / 2f;
            sq1[i] = Proj(new Vector3(MathF.Cos(a) * StarR * scale, MathF.Sin(a) * StarR * scale, 0), center);
        }

        var c45 = StarR * MathF.Cos(MathF.PI / 4f);
        var sq2 = new Vector2[4];
        for (var i = 0; i < 4; i++)
        {
            var a = i * MathF.PI / 2f + MathF.PI / 4f;
            sq2[i] = Proj(new Vector3(MathF.Cos(a) * StarR * scale, MathF.Sin(a) * StarR * scale, 0), center);
        }

        for (var i = 0; i < 4; i++)
            DrawLine(dl, sq1[i], sq1[(i + 1) % 4], alpha, 2f);
        for (var i = 0; i < 4; i++)
            DrawLine(dl, sq2[i], sq2[(i + 1) % 4], alpha, 2f);
    }

    private Vector2[] GetOctagramVertices(Vector2 center, float scale)
    {
        var verts = new Vector2[8];
        for (var i = 0; i < 8; i++)
        {
            var a = i * MathF.Tau / 8f;
            verts[i] = Proj(new Vector3(MathF.Cos(a) * StarR * scale, MathF.Sin(a) * StarR * scale, 0), center);
        }
        return verts;
    }

    private void DrawL3_OuterSmallCircles(ImDrawListPtr dl, Vector2 center, float scale, float alpha)
    {
        const int segs = 24;
        for (var i = 0; i < 8; i++)
        {
            var a = i * MathF.Tau / 8f;
            var cx = MathF.Cos(a) * StarR * scale;
            var cy = MathF.Sin(a) * StarR * scale;
            var pts = new Vector2[segs + 1];
            for (var j = 0; j <= segs; j++)
            {
                var ca = j * MathF.Tau / segs;
                pts[j] = Proj(new Vector3(cx + MathF.Cos(ca) * L3SmallR * scale,
                    cy + MathF.Sin(ca) * L3SmallR * scale, 0), center);
            }
            DrawPolyline(dl, pts, alpha, 1.5f);
        }
    }

    private void DrawL4_InnerSmallCircles(ImDrawListPtr dl, Vector2 center, float scale, float alpha)
    {
        const int segs = 24;
        for (var i = 0; i < 8; i++)
        {
            var a = i * MathF.Tau / 8f;
            var dist = StarR * 0.7f;
            var cx = MathF.Cos(a) * dist * scale;
            var cy = MathF.Sin(a) * dist * scale;
            var pts = new Vector2[segs + 1];
            for (var j = 0; j <= segs; j++)
            {
                var ca = j * MathF.Tau / segs;
                pts[j] = Proj(new Vector3(cx + MathF.Cos(ca) * L4SmallR * scale,
                    cy + MathF.Sin(ca) * L4SmallR * scale, 0), center);
            }
            DrawPolyline(dl, pts, alpha, 1f);
        }
    }

    private void DrawL5_EnclosingCircle(ImDrawListPtr dl, Vector2 center, float scale, float alpha)
    {
        const int segs = 32;
        var pts = new Vector2[segs + 1];
        for (var i = 0; i <= segs; i++)
        {
            var a = i * MathF.Tau / segs;
            pts[i] = Proj(new Vector3(MathF.Cos(a) * L5R * scale, MathF.Sin(a) * L5R * scale, 0), center);
        }
        DrawPolyline(dl, pts, alpha, 1.5f);
    }

    private void DrawL6_Center(ImDrawListPtr dl, Vector2 center, float scale, float alpha)
    {
        var sq = new Vector2[5];
        for (var i = 0; i < 4; i++)
        {
            var a = i * MathF.PI / 2f + MathF.PI / 4f;
            sq[i] = Proj(new Vector3(MathF.Cos(a) * CenterSquareR * scale,
                MathF.Sin(a) * CenterSquareR * scale, 0), center);
        }
        sq[4] = sq[0];
        DrawPolyline(dl, sq, alpha, 1f);

        const int segs = 24;
        var circlePts = new Vector2[segs + 1];
        for (var i = 0; i <= segs; i++)
        {
            var a = i * MathF.Tau / segs;
            circlePts[i] = Proj(new Vector3(MathF.Cos(a) * CenterCircleR * scale,
                MathF.Sin(a) * CenterCircleR * scale, 0), center);
        }
        DrawPolyline(dl, circlePts, alpha, 1f);

        var dot = Proj(Vector3.Zero, center);
        dl.AddCircleFilled(dot + CyanOff, 2f,
            ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, alpha * 0.5f)));
        dl.AddCircleFilled(dot + MagOff, 2f,
            ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 1, alpha * 0.5f)));
        dl.AddCircleFilled(dot, 2f,
            ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, alpha)));
    }

    private static void DrawBaseGradient(ImDrawListPtr dl, Vector2 min, Vector2 max)
    {
        var h = max.Y - min.Y;
        var steps = 6;
        var stepH = h / steps;
        for (var i = 0; i < steps; i++)
        {
            var t = i / (float)steps;
            var alpha = 0.02f + t * 0.03f;
            var y1 = min.Y + i * stepH;
            var y2 = y1 + stepH;
            dl.AddRectFilled(new Vector2(min.X, y1), new Vector2(max.X, y2),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.1f, 0.2f, 0.4f, alpha)));
        }
    }

    private static void DrawScanNoise(ImDrawListPtr dl, Vector2 min, Vector2 max)
    {
        var w = max.X - min.X;
        var count = 8 + (Random.Shared.NextSingle() < 0.3f ? 12 : 0);
        for (var i = 0; i < count; i++)
        {
            var x = min.X + Random.Shared.NextSingle() * w;
            var y = min.Y + Random.Shared.NextSingle() * (max.Y - min.Y);
            var len = 5f + Random.Shared.NextSingle() * 20f;
            var a = 0.05f + Random.Shared.NextSingle() * 0.15f;
            dl.AddLine(new Vector2(x, y), new Vector2(x + len, y),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.7f, 0.8f, 1f, a)), 1f);
        }
    }
}
