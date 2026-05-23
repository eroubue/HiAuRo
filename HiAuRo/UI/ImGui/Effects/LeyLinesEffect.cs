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

    private static readonly Vector2[] TriTips =
    [
        new(6f, 3.464102f),
        new(0f, 6.928203f),
        new(-6f, 3.464102f),
        new(-6f, -3.464102f),
        new(0f, -6.928203f),
        new(6f, -3.464102f),
    ];

    private static readonly Vector2[] TriCentroids =
    [
        new(4f, 2.309401f),
        new(0f, 4.618802f),
        new(-4f, 2.309401f),
        new(-4f, -2.309401f),
        new(0f, -4.618802f),
        new(4f, -2.309401f),
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
        _rotAngle += 3f * dt;

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
        var scale = shortSide * 0.12f;
        var a = _flickerAlpha;
        var accent = Theme.Colors.AccentBlue;

        DrawCircle(dl, center, scale, Vector2.Zero, 6f, accent, a, 2.5f);
        DrawCircle(dl, center, scale, Vector2.Zero, 4f, accent, a, 2f);
        DrawPoly(dl, center, scale, DiamondC, accent, a * 0.8f, 1.5f);
        DrawPoly(dl, center, scale, SquareD, accent, a * 0.7f, 1.5f);

        for (var i = 0; i < 6; i++)
            DrawCircle(dl, center, scale, FCenters[i], 2f, accent, a * 0.8f, 1.5f);

        Span<Vector2> tri = stackalloc Vector2[3];
        for (var i = 0; i < 6; i++)
        {
            var next = (i + 1) % 6;
            tri[0] = FCenters[i];
            tri[1] = FCenters[next];
            tri[2] = TriTips[i];
            DrawPoly(dl, center, scale, tri, accent, a * 0.6f, 1f);
        }

        for (var i = 0; i < 6; i++)
            DrawCircle(dl, center, scale, TriCentroids[i], 1f, accent, a * 0.5f, 1f);

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
