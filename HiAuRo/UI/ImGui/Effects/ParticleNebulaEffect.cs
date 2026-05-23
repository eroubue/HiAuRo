using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

/// <summary>
/// 粒子星云特效 — 大量粒子围绕窗口中心旋转，模拟星系/星云
/// </summary>
public sealed class ParticleNebulaEffect
{
    private struct Particle
    {
        public float Angle;
        public float Radius;
        public float YOffset;
        public float Size;
        public Vector3 Color;
        public float AngularSpeed;
        public float PulsePhase;
        public float PulseFreq;
    }

    private struct EscapeParticle
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Life;
        public float MaxLife;
        public float Size;
        public Vector3 Color;
    }

    private readonly Particle[] _particles;
    private readonly EscapeParticle[] _escapes;
    private float _time;

    public ParticleNebulaEffect(int particleCount = 100, int escapeCount = 12)
    {
        _particles = new Particle[particleCount];
        for (var i = 0; i < _particles.Length; i++)
            InitParticle(ref _particles[i], i, particleCount);

        _escapes = new EscapeParticle[escapeCount];
    }

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        _time += dt;

        for (var i = 0; i < _particles.Length; i++)
        {
            ref var p = ref _particles[i];
            p.Angle += p.AngularSpeed * dt;
            p.PulsePhase += p.PulseFreq * dt;
        }

        for (var i = 0; i < _escapes.Length; i++)
        {
            ref var e = ref _escapes[i];
            if (e.Life <= 0f)
            {
                ResetEscape(ref e, min, max);
                continue;
            }
            e.Life -= dt;
            e.Pos += e.Vel * dt;
        }
    }

    public void Draw(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax)
    {
        dl.PushClipRect(winMin, winMax, true);

        var center = (winMin + winMax) * 0.5f;
        var maxR = Math.Min(winMax.X - winMin.X, winMax.Y - winMin.Y) * 0.45f;
        var accent = Theme.Colors.AccentBlue;

        // 绘制星系粒子
        for (var i = 0; i < _particles.Length; i++)
        {
            ref var p = ref _particles[i];

            var r = p.Radius * maxR;
            var px = center.X + MathF.Cos(p.Angle) * r;
            var py = center.Y + MathF.Sin(p.Angle) * r * 0.6f + p.YOffset * maxR * 0.3f;

            var pulse = (MathF.Sin(p.PulsePhase) + 1f) * 0.5f;
            var alpha = 0.3f + pulse * 0.5f;

            var color = ImGui.ColorConvertFloat4ToU32(
                new Vector4(p.Color.X, p.Color.Y, p.Color.Z, alpha));
            dl.AddCircleFilled(new Vector2(px, py), p.Size, color);
        }

        // 绘制逃逸粒子
        for (var i = 0; i < _escapes.Length; i++)
        {
            ref var e = ref _escapes[i];
            if (e.Life <= 0f) continue;

            var lifeRatio = Math.Max(0f, e.Life / e.MaxLife);
            var color = ImGui.ColorConvertFloat4ToU32(
                new Vector4(e.Color.X, e.Color.Y, e.Color.Z, lifeRatio * 0.6f));
            dl.AddCircleFilled(e.Pos, e.Size * lifeRatio, color);
        }

        dl.PopClipRect();
    }

    private void InitParticle(ref Particle p, int index, int total)
    {
        var ratio = (float)index / total;
        p.Angle = Random.Shared.NextSingle() * MathF.Tau;
        p.Radius = 0.1f + ratio * 0.9f;
        p.YOffset = (Random.Shared.NextSingle() - 0.5f) * 0.6f;
        p.Size = ratio < 0.3f ? 1.5f + Random.Shared.NextSingle() * 1f : 2f + Random.Shared.NextSingle() * 2f;
        p.AngularSpeed = ratio < 0.3f
            ? 0.5f + Random.Shared.NextSingle() * 0.3f
            : 0.1f + Random.Shared.NextSingle() * 0.2f;
        if (Random.Shared.NextSingle() > 0.5f) p.AngularSpeed *= -1f;
        p.PulsePhase = Random.Shared.NextSingle() * MathF.Tau;
        p.PulseFreq = 1f + Random.Shared.NextSingle() * 2f;

        // 品牌蓝为主，混入紫色/青色
        var colorRand = Random.Shared.NextSingle();
        if (colorRand < 0.5f)
            p.Color = new Vector3(0.09f, 0.47f, 1f); // AccentBlue
        else if (colorRand < 0.75f)
            p.Color = new Vector3(0.5f, 0.2f, 0.9f); // 紫色
        else
            p.Color = new Vector3(0f, 0.8f, 0.9f); // 青色
    }

    private void ResetEscape(ref EscapeParticle e, Vector2 min, Vector2 max)
    {
        var center = (min + max) * 0.5f;
        var angle = Random.Shared.NextSingle() * MathF.Tau;
        var speed = 30f + Random.Shared.NextSingle() * 60f;
        e.Pos = center;
        e.Vel = new Vector2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed);
        e.MaxLife = 2f + Random.Shared.NextSingle() * 3f;
        e.Life = e.MaxLife;
        e.Size = 2f + Random.Shared.NextSingle() * 2f;

        var colorRand = Random.Shared.NextSingle();
        e.Color = colorRand < 0.5f
            ? new Vector3(0.09f, 0.47f, 1f)
            : new Vector3(0.5f, 0.2f, 0.9f);
    }
}
