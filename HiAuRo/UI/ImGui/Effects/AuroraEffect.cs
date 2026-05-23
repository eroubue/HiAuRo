using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

/// <summary>
/// 极光特效 — 窗口顶部波浪形彩色渐变光带，像北极光
/// </summary>
public sealed class AuroraEffect
{
    private struct Band
    {
        public Vector3 Color;
        public float Frequency;
        public float Amplitude;
        public float Phase;
        public float PhaseSpeed;
        public float YCenter;
        public float YSpread;
        public float Thickness;
        public float GlowThickness;
    }

    private readonly Band[] _bands;
    private float _time;
    private const int SampleCount = 80;

    public AuroraEffect()
    {
        var accentBlue = new Vector3(0.09f, 0.47f, 1f);
        var accentGreen = new Vector3(0.32f, 0.77f, 0.1f);
        var purple = new Vector3(0.5f, 0.2f, 0.8f);

        _bands = new Band[]
        {
            new()
            {
                Color = accentGreen, Frequency = 1.2f, Amplitude = 0.06f,
                Phase = 0f, PhaseSpeed = 0.4f, YCenter = 0.12f, YSpread = 0.08f,
                Thickness = 4f, GlowThickness = 12f,
            },
            new()
            {
                Color = accentBlue, Frequency = 0.8f, Amplitude = 0.08f,
                Phase = 1f, PhaseSpeed = 0.3f, YCenter = 0.18f, YSpread = 0.1f,
                Thickness = 3f, GlowThickness = 10f,
            },
            new()
            {
                Color = purple, Frequency = 1.5f, Amplitude = 0.04f,
                Phase = 2f, PhaseSpeed = 0.5f, YCenter = 0.08f, YSpread = 0.06f,
                Thickness = 5f, GlowThickness = 14f,
            },
            new()
            {
                Color = new Vector3(0.1f, 0.6f, 0.5f), Frequency = 1f, Amplitude = 0.07f,
                Phase = 3f, PhaseSpeed = 0.35f, YCenter = 0.15f, YSpread = 0.09f,
                Thickness = 3.5f, GlowThickness = 11f,
            },
        };
    }

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        _time += dt;
        for (var i = 0; i < _bands.Length; i++)
            _bands[i].Phase += _bands[i].PhaseSpeed * dt;
    }

    public void Draw(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax)
    {
        dl.PushClipRect(winMin, winMax, true);

        var w = winMax.X - winMin.X;
        var h = winMax.Y - winMin.Y;
        var step = w / (SampleCount - 1);

        for (var b = 0; b < _bands.Length; b++)
        {
            ref var band = ref _bands[b];

            // 辉光层（更粗更透明）
            DrawBandLine(dl, winMin, w, h, step, band.Color, band.Phase,
                band.Frequency, band.Amplitude, band.YCenter, band.YSpread,
                0.06f, band.GlowThickness);

            // 主线条
            DrawBandLine(dl, winMin, w, h, step, band.Color, band.Phase,
                band.Frequency, band.Amplitude, band.YCenter, band.YSpread,
                0.2f, band.Thickness);
        }

        dl.PopClipRect();
    }

    private static void DrawBandLine(ImDrawListPtr dl, Vector2 winMin, float w, float h, float step,
        Vector3 color, float phase, float freq, float amp, float yCenter, float ySpread, float alpha, float thickness)
    {
        Span<Vector2> pathPoints = stackalloc Vector2[SampleCount];

        for (var i = 0; i < SampleCount; i++)
        {
            var xRatio = (float)i / (SampleCount - 1);
            var wave = MathF.Sin(xRatio * freq * MathF.Tau + phase) * amp;
            var yRatio = yCenter + wave * ySpread;
            pathPoints[i] = new Vector2(winMin.X + i * step, winMin.Y + yRatio * h);
        }

        for (var i = 0; i < SampleCount - 1; i++)
        {
            var midY = (pathPoints[i].Y + pathPoints[i + 1].Y) * 0.5f;
            var fadeOut = Math.Clamp(1f - (midY - winMin.Y) / (h * 0.5f), 0f, 1f);
            var a = alpha * fadeOut;
            if (a < 0.005f) continue;

            var lineColor = ImGui.ColorConvertFloat4ToU32(
                new Vector4(color.X, color.Y, color.Z, a));
            dl.AddLine(pathPoints[i], pathPoints[i + 1], lineColor, thickness);
        }
    }
}
