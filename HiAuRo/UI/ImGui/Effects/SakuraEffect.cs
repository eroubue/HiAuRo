using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

/// <summary>
/// 樱花飘落特效 — 粉色花瓣从窗口顶部飘落，带旋转和水平摇摆
/// </summary>
public sealed class SakuraEffect
{
    private struct Petal
    {
        public float X;
        public float Y;
        public float Size;
        public float Speed;
        public float SwingAmp;
        public float SwingFreq;
        public float SwingPhase;
        public float Rotation;
        public float RotSpeed;
        public float Life;
        public float MaxLife;
    }

    private readonly Petal[] _petals;
    private float _time;

    public SakuraEffect(int count = 50)
    {
        _petals = new Petal[count];
    }

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        _time += dt;
        var h = max.Y - min.Y;

        for (var i = 0; i < _petals.Length; i++)
        {
            ref var p = ref _petals[i];

            if (p.Life <= 0f)
            {
                ResetPetal(ref p, min, max, true);
                continue;
            }

            p.Life -= dt;
            p.Y += p.Speed * dt;
            p.SwingPhase += p.SwingFreq * dt;
            p.X += MathF.Sin(p.SwingPhase) * p.SwingAmp * dt;
            p.Rotation += p.RotSpeed * dt;

            if (p.Y > max.Y + p.Size || p.Life <= 0f)
                ResetPetal(ref p, min, max, false);
        }
    }

    public void Draw(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax)
    {
        dl.PushClipRect(winMin, winMax, true);

        var isLight = Theme.Mode == Theme.ThemeMode.Light;

        for (var i = 0; i < _petals.Length; i++)
        {
            ref var p = ref _petals[i];
            if (p.Life <= 0f) continue;

            var lifeRatio = Math.Max(0f, p.Life / p.MaxLife);

            // 接近底部时淡出
            var h = winMax.Y - winMin.Y;
            var yRatio = Math.Clamp((p.Y - winMin.Y) / h, 0f, 1f);
            var fadeBottom = yRatio > 0.8f ? 1f - (yRatio - 0.8f) / 0.2f : 1f;
            var alpha = Math.Min(lifeRatio, fadeBottom) * 0.8f;
            if (alpha <= 0.01f) continue;

            var baseColor = isLight
                ? new Vector4(0.8f, 0.2f, 0.4f, alpha)
                : new Vector4(1f, 0.55f, 0.65f, alpha);

            var color = ImGui.ColorConvertFloat4ToU32(baseColor);
            var pos = new Vector2(p.X, p.Y);

            // 用椭圆模拟花瓣（水平方向略大）
            var rx = p.Size;
            var ry = p.Size * 0.6f;

            // 旋转后的偏移
            var cos = MathF.Cos(p.Rotation);
            var sin = MathF.Sin(p.Rotation);

            // 用多个小圆叠加模拟椭圆花瓣
            var steps = 8;
            for (var s = 0; s < steps; s++)
            {
                var t = s / (float)steps * MathF.Tau;
                var lx = MathF.Cos(t) * rx;
                var ly = MathF.Sin(t) * ry;
                var rx2 = lx * cos - ly * sin;
                var ry2 = lx * sin + ly * cos;
                dl.PathLineTo(pos + new Vector2(rx2, ry2));
            }
            dl.PathFillConvex(color);
        }

        dl.PopClipRect();
    }

    private void ResetPetal(ref Petal p, Vector2 min, Vector2 max, bool initial)
    {
        var w = max.X - min.X;
        var h = max.Y - min.Y;
        p.X = min.X + Random.Shared.NextSingle() * w;
        p.Y = initial ? min.Y + Random.Shared.NextSingle() * h : min.Y - Random.Shared.NextSingle() * 20f;
        p.Size = 6f + Random.Shared.NextSingle() * 8f;
        p.Speed = 30f + Random.Shared.NextSingle() * 50f;
        p.SwingAmp = 15f + Random.Shared.NextSingle() * 25f;
        p.SwingFreq = 1f + Random.Shared.NextSingle() * 2f;
        p.SwingPhase = Random.Shared.NextSingle() * MathF.Tau;
        p.Rotation = Random.Shared.NextSingle() * MathF.Tau;
        p.RotSpeed = (Random.Shared.NextSingle() - 0.5f) * 2f;
        p.MaxLife = 4f + Random.Shared.NextSingle() * 4f;
        p.Life = p.MaxLife;
    }
}
