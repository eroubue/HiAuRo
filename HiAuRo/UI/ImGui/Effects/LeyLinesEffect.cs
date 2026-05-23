using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

public sealed class LeyLinesEffect
{
    private static readonly Vector2 CyanOff = new(-2f, 0);
    private static readonly Vector2 MagOff = new(2f, 0);

    private ImTextureID _textureId;
    private bool _textureLoaded;
    private bool _textureLoadFailed;

    private float _time;
    private float _flickerTimer = 2f;
    private bool _inFlicker;
    private float _flickerAlpha = 0.2f;

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        _time += dt;

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
        var topLeft = center - new Vector2(half);
        var bottomRight = center + new Vector2(half);

        dl.AddImage(_textureId, topLeft + CyanOff, bottomRight + CyanOff,
            Vector2.Zero, Vector2.One,
            ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, alpha * 0.5f)));

        dl.AddImage(_textureId, topLeft + MagOff, bottomRight + MagOff,
            Vector2.Zero, Vector2.One,
            ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 1, alpha * 0.5f)));

        dl.AddImage(_textureId, topLeft, bottomRight,
            Vector2.Zero, Vector2.One,
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
            using var stream = assembly.GetManifestResourceStream("HiAuRo.Resources.blm_white.png");
            if (stream == null) { _textureLoadFailed = true; return; }

            var tempPath = Path.Combine(Path.GetTempPath(), "hiauro_blm_white.png");
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
