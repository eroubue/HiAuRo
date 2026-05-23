using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

public sealed class LeyLinesEffect
{
    private const int RingSegments = 64;
    private const float Perspective = 400f;

    private const float OuterR = 1.0f;
    private const float MiddleR = 0.7f;
    private const float InnerR = 0.4f;
    private const float PentOuterR = 0.75f;
    private const float PentInnerR = PentOuterR * 0.382f;

    private static readonly int[] PentagramOrder = [0, 2, 4, 1, 3];

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

        DrawBaseGradient(dl, winMin, winMax);

        var alpha = _flickerAlpha;
        var cyanOff = new Vector2(-2f, 0);
        var magOff = new Vector2(2f, 0);

        DrawRing(dl, center, scale, OuterR, cyanOff, magOff, alpha);
        DrawRing(dl, center, scale, MiddleR, cyanOff, magOff, alpha);
        DrawRing(dl, center, scale, InnerR, cyanOff, magOff, alpha);

        var pentOuter = new Vector2[5];
        var pentInner = new Vector2[5];
        for (var i = 0; i < 5; i++)
        {
            var outerAngle = i * MathF.Tau / 5f - MathF.PI / 2f;
            pentOuter[i] = Project(
                new Vector3(MathF.Cos(outerAngle) * PentOuterR * scale,
                    MathF.Sin(outerAngle) * PentOuterR * scale, 0),
                center, _rotX, _rotY);

            var innerAngle = (i + 0.5f) * MathF.Tau / 5f - MathF.PI / 2f;
            pentInner[i] = Project(
                new Vector3(MathF.Cos(innerAngle) * PentInnerR * scale,
                    MathF.Sin(innerAngle) * PentInnerR * scale, 0),
                center, _rotX, _rotY);
        }

        for (var i = 0; i < 5; i++)
        {
            var a = pentOuter[PentagramOrder[i]];
            var b = pentOuter[PentagramOrder[(i + 1) % 5]];
            DrawEdge(dl, a, b, cyanOff, magOff, alpha);
        }

        for (var i = 0; i < 5; i++)
        {
            var angle = i * MathF.Tau / 5f - MathF.PI / 2f;
            var innerPt = Project(
                new Vector3(MathF.Cos(angle) * InnerR * scale,
                    MathF.Sin(angle) * InnerR * scale, 0),
                center, _rotX, _rotY);
            var outerPt = Project(
                new Vector3(MathF.Cos(angle) * OuterR * scale,
                    MathF.Sin(angle) * OuterR * scale, 0),
                center, _rotX, _rotY);
            DrawEdge(dl, innerPt, outerPt, cyanOff, magOff, alpha);
        }

        for (var i = 0; i < 5; i++)
            DrawVertex(dl, pentOuter[i], cyanOff, magOff, alpha);
        for (var i = 0; i < 5; i++)
            DrawVertex(dl, pentInner[i], cyanOff, magOff, alpha);
        DrawVertex(dl, Project(Vector3.Zero, center, _rotX, _rotY), cyanOff, magOff, alpha);
        DrawVertex(dl,
            Project(new Vector3(OuterR * scale, 0, 0), center, _rotX, _rotY),
            cyanOff, magOff, alpha);

        DrawScanNoise(dl, winMin, winMax);

        dl.PopClipRect();
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

    private void DrawRing(ImDrawListPtr dl, Vector2 center, float scale, float radius,
        Vector2 cyanOff, Vector2 magOff, float alpha)
    {
        var pts = new Vector2[RingSegments + 1];
        for (var i = 0; i <= RingSegments; i++)
        {
            var a = i * MathF.Tau / RingSegments;
            var v = new Vector3(MathF.Cos(a) * radius * scale, MathF.Sin(a) * radius * scale, 0);
            pts[i] = Project(v, center, _rotX, _rotY);
        }

        for (var i = 0; i < pts.Length - 1; i++)
            dl.AddLine(pts[i] + cyanOff, pts[i + 1] + cyanOff,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, alpha * 0.5f)), 1f);
        for (var i = 0; i < pts.Length - 1; i++)
            dl.AddLine(pts[i] + magOff, pts[i + 1] + magOff,
                ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 1, alpha * 0.5f)), 1f);
        for (var i = 0; i < pts.Length - 1; i++)
            dl.AddLine(pts[i], pts[i + 1],
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.7f, 0.8f, 1f, alpha)), 1f);
    }

    private static void DrawEdge(ImDrawListPtr dl, Vector2 a, Vector2 b,
        Vector2 cyanOff, Vector2 magOff, float alpha)
    {
        dl.AddLine(a + cyanOff, b + cyanOff,
            ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, alpha * 0.5f)), 1f);
        dl.AddLine(a + magOff, b + magOff,
            ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 1, alpha * 0.5f)), 1f);
        dl.AddLine(a, b,
            ImGui.ColorConvertFloat4ToU32(new Vector4(0.7f, 0.8f, 1f, alpha)), 1f);
    }

    private static void DrawVertex(ImDrawListPtr dl, Vector2 p,
        Vector2 cyanOff, Vector2 magOff, float alpha)
    {
        dl.AddCircleFilled(p + cyanOff, 2f,
            ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, alpha * 0.5f)));
        dl.AddCircleFilled(p + magOff, 2f,
            ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 1, alpha * 0.5f)));
        dl.AddCircleFilled(p, 3f,
            ImGui.ColorConvertFloat4ToU32(new Vector4(0.7f, 0.8f, 1f, alpha)));
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
