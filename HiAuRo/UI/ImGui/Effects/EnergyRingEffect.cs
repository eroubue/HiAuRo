using System.Numerics;
using System.Runtime.InteropServices;

namespace HiAuRo.ImGuiLib.Effects;

/// <summary>
/// 能量环特效 — 周期性从随机位置扩散同心圆环（声纳脉冲）
/// </summary>
public sealed class EnergyRingEffect
{
    private struct RingGroup
    {
        public Vector2 Center;
        public float Radius;
        public float MaxRadius;
        public float Speed;
        public float Life;
        public int RingCount;
        public float RingSpacing;
    }

    private readonly List<RingGroup> _groups = new(8);
    private float _spawnTimer;
    private float _nextSpawnInterval = 1.5f;
    private const int MaxGroups = 6;

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        _spawnTimer += dt;

        if (_spawnTimer >= _nextSpawnInterval && _groups.Count < MaxGroups)
        {
            _spawnTimer = 0f;
            _nextSpawnInterval = 1f + Random.Shared.NextSingle() * 1f;

            var w = max.X - min.X;
            var h = max.Y - min.Y;
            var cx = min.X + w * (0.15f + Random.Shared.NextSingle() * 0.7f);
            var cy = min.Y + h * (0.15f + Random.Shared.NextSingle() * 0.7f);

            _groups.Add(new RingGroup
            {
                Center = new Vector2(cx, cy),
                Radius = 0f,
                MaxRadius = 60f + Random.Shared.NextSingle() * 100f,
                Speed = 60f + Random.Shared.NextSingle() * 40f,
                Life = 1f,
                RingCount = 2 + Random.Shared.Next(0, 2),
                RingSpacing = 15f + Random.Shared.NextSingle() * 10f,
            });
        }

        for (var i = _groups.Count - 1; i >= 0; i--)
        {
            ref var g = ref CollectionsMarshal.AsSpan(_groups)[i];
            g.Radius += g.Speed * dt;

            var fadeStart = g.MaxRadius * 0.6f;
            if (g.Radius > fadeStart)
                g.Life -= dt * (g.Speed / (g.MaxRadius - fadeStart));

            if (g.Life <= 0f)
                _groups.RemoveAt(i);
        }
    }

    public void Draw(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax)
    {
        dl.PushClipRect(winMin, winMax, true);

        var accent = Theme.Colors.AccentBlue;

        foreach (var g in _groups)
        {
            for (var r = 0; r < g.RingCount; r++)
            {
                var ringR = g.Radius - r * g.RingSpacing;
                if (ringR <= 0f) continue;

                var alpha = Math.Max(0f, g.Life) * (0.4f + 0.4f / (r + 1));
                var seg = Math.Max(32, (int)(MathF.Tau * ringR / 3f));

                // 辉光层
                var glowAlpha = alpha * 0.3f;
                var glowColor = ImGui.ColorConvertFloat4ToU32(
                    new Vector4(accent.X, accent.Y, accent.Z, glowAlpha));
                dl.PathClear();
                dl.PathArcTo(g.Center, ringR, 0f, MathF.Tau, seg);
                dl.PathStroke(glowColor, ImDrawFlags.Closed, 6f + r * 1f);

                // 主环
                var mainColor = ImGui.ColorConvertFloat4ToU32(
                    new Vector4(accent.X, accent.Y, accent.Z, alpha));
                dl.PathClear();
                dl.PathArcTo(g.Center, ringR, 0f, MathF.Tau, seg);
                dl.PathStroke(mainColor, ImDrawFlags.Closed, 2f + r * 0.5f);
            }
        }

        dl.PopClipRect();
    }
}
