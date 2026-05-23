using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

public sealed class LeyLinesEffect
{
    private static readonly Vector2[] FCenters =
    [
        new(4f, 0f),
        new(2f, 3.464102f),
        new(-2f, 3.464102f),
        new(-4f, 0f),
        new(-2f, -3.464102f),
        new(2f, -3.464102f),
    ];

    private static readonly Vector2[] TriBase1 =
    [
        new(4.005988f, 2.970220f),
        new(-0.569292f, 4.954398f),
        new(-4.575280f, 1.984178f),
        new(-4.005988f, -2.970220f),
        new(0.569292f, -4.954398f),
        new(4.575280f, -1.984178f),
    ];

    private static readonly Vector2[] TriBase2 =
    [
        new(4.575280f, 1.984178f),
        new(0.569292f, 4.954398f),
        new(-4.005988f, 2.970220f),
        new(-4.575280f, -1.984178f),
        new(-0.569292f, -4.954398f),
        new(4.005988f, -2.970220f),
    ];

    private static readonly Vector2[] TriTips =
    [
        new(5.152851f, 2.975000f),
        new(0.000000f, 5.950000f),
        new(-5.152851f, 2.975000f),
        new(-5.152851f, -2.975000f),
        new(0.000000f, -5.950000f),
        new(5.152851f, -2.975000f),
    ];

    private static readonly Vector2[] TriCentroids =
    [
        new(4.577851f, 2.643024f),
        new(0.000000f, 5.286047f),
        new(-4.577851f, 2.643024f),
        new(-4.577851f, -2.643024f),
        new(0.000000f, -5.286047f),
        new(4.577851f, -2.643024f),
    ];

    private static readonly Vector2[] DiamondC =
    [
        new(4f, 0f), new(0f, 4f), new(-4f, 0f), new(0f, -4f),
    ];

    private static readonly Vector2[] SquareD =
    [
        new(2f, 2f), new(-2f, 2f), new(-2f, -2f), new(2f, -2f),
    ];

    private float _rotAngle;
    private float _time;
    private float _flickerAlpha = 0.2f;
    private float _flickerTimer = 2f;
    private bool _inFlicker;

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        _time += dt;
        _rotAngle += 1f * dt;

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
        var shortSide = MathF.Min(winMax.X - winMin.X, winMax.Y - winMin.Y);
        var scale = shortSide * 0.06f;
        var a = _flickerAlpha;
        var accent = Theme.Colors.AccentBlue;

        DrawCircle(dl, center, scale, Vector2.Zero, 6f, accent, a, 7.5f);
        DrawCircle(dl, center, scale, Vector2.Zero, 4f, accent, a, 6f);
        DrawPoly(dl, center, scale, DiamondC, accent, a * 0.8f, 4.5f);
        DrawPoly(dl, center, scale, SquareD, accent, a * 0.7f, 4.5f);

        for (var i = 0; i < 6; i++)
            DrawCircle(dl, center, scale, FCenters[i], 2f, accent, a * 0.8f, 4.5f);

        Span<Vector2> tri = stackalloc Vector2[3];
        for (var i = 0; i < 6; i++)
        {
            tri[0] = TriBase1[i];
            tri[1] = TriBase2[i];
            tri[2] = TriTips[i];
            DrawPoly(dl, center, scale, tri, accent, a * 0.6f, 3f);
        }

        for (var i = 0; i < 6; i++)
            DrawCircle(dl, center, scale, TriCentroids[i], 0.23f, accent, a * 0.5f, 3f);

        dl.PopClipRect();
    }

    private Vector2 ToScreen(Vector2 screenCenter, float scale, Vector2 local)
    {
        var cos = MathF.Cos(_rotAngle);
        var sin = MathF.Sin(_rotAngle);
        return screenCenter + new Vector2(
            local.X * cos - local.Y * sin,
            local.X * sin + local.Y * cos) * scale;
    }

    private void DrawCircle(ImDrawListPtr dl, Vector2 screenCenter, float scale,
        Vector2 localCenter, float radius, Vector4 color, float alpha, float thickness)
    {
        var c = ToScreen(screenCenter, scale, localCenter);
        var r = MathF.Max(radius * scale, 0.1f);
        var glowCol = ColorU32(color, alpha * 0.25f);
        var mainCol = ColorU32(color, alpha);

        dl.PathArcTo(c, r, 0f, MathF.Tau, 48);
        dl.PathStroke(glowCol, ImDrawFlags.None, thickness * 3f);
        dl.PathArcTo(c, r, 0f, MathF.Tau, 48);
        dl.PathStroke(mainCol, ImDrawFlags.None, thickness);
    }

    private void DrawPoly(ImDrawListPtr dl, Vector2 screenCenter, float scale,
        ReadOnlySpan<Vector2> verts, Vector4 color, float alpha, float thickness)
    {
        var glowCol = ColorU32(color, alpha * 0.25f);
        var mainCol = ColorU32(color, alpha);

        for (var pass = 0; pass < 2; pass++)
        {
            dl.PathLineTo(ToScreen(screenCenter, scale, verts[0]));
            for (var i = 1; i < verts.Length; i++)
                dl.PathLineTo(ToScreen(screenCenter, scale, verts[i]));
            dl.PathLineTo(ToScreen(screenCenter, scale, verts[0]));
            dl.PathStroke(pass == 0 ? glowCol : mainCol, ImDrawFlags.None,
                pass == 0 ? thickness * 3f : thickness);
        }
    }

    private static uint ColorU32(Vector4 c, float a)
        => ImGui.ColorConvertFloat4ToU32(new Vector4(c.X, c.Y, c.Z, a));
}
