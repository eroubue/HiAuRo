using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

public sealed class LeyLinesEffect
{
    private const float Perspective = 400f;

    private static readonly Vector2 CyanOff = new(-2f, 0);
    private static readonly Vector2 MagOff = new(2f, 0);

    private ImTextureID _textureId;
    private bool _textureLoaded;
    private bool _textureLoadFailed;

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
        LoadTexture();
        if (_textureId == 0) return;

        dl.PushClipRect(winMin, winMax, true);

        var center = (winMin + winMax) * 0.5f;
        var alpha = _flickerAlpha;

        DrawBaseGradient(dl, winMin, winMax);

        var shortSide = Math.Min(winMax.X - winMin.X, winMax.Y - winMin.Y);
        var displaySize = shortSide * 0.6f;
        var half = displaySize * 0.5f;

        var corners3D = new[]
        {
            new Vector3(-half, -half, 0),
            new Vector3(half, -half, 0),
            new Vector3(half, half, 0),
            new Vector3(-half, half, 0),
        };

        var p = new Vector2[4];
        for (var i = 0; i < 4; i++)
            p[i] = Project(corners3D[i], center, _rotX, _rotY);

        dl.AddImageQuad(_textureId,
            p[0] + CyanOff, p[1] + CyanOff, p[2] + CyanOff, p[3] + CyanOff,
            Vector2.Zero, Vector2.UnitX, Vector2.One, Vector2.UnitY,
            ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, alpha * 0.5f)));

        dl.AddImageQuad(_textureId,
            p[0] + MagOff, p[1] + MagOff, p[2] + MagOff, p[3] + MagOff,
            Vector2.Zero, Vector2.UnitX, Vector2.One, Vector2.UnitY,
            ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 1, alpha * 0.5f)));

        dl.AddImageQuad(_textureId,
            p[0], p[1], p[2], p[3],
            Vector2.Zero, Vector2.UnitX, Vector2.One, Vector2.UnitY,
            ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, alpha)));

        DrawScanNoise(dl, winMin, winMax);

        dl.PopClipRect();
    }

    private void LoadTexture()
    {
        if (_textureLoaded || _textureLoadFailed) return;
        try
        {
            var assembly = typeof(LeyLinesEffect).Assembly;
            using var stream = assembly.GetManifestResourceStream("HiAuRo.Resources.ley_lines.png");
            if (stream == null) { _textureLoadFailed = true; return; }

            var tempPath = Path.Combine(Path.GetTempPath(), "hiauro_ley_lines.png");
            using (var fs = File.Create(tempPath))
                stream.CopyTo(fs);

            var wrap = DService.Instance().Texture.GetFromFile(tempPath)?.GetWrapOrEmpty();
            if (wrap == null || wrap.Handle == 0)
            {
                _textureLoadFailed = true;
                return;
            }
            _textureId = wrap.Handle;            _textureLoaded = true;
        }
        catch
        {
            _textureLoadFailed = true;
        }
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
