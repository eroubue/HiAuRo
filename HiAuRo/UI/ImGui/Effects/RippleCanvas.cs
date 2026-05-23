using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

/// <summary>
/// 波纹/涟漪背景 — 多圈同心扩散波纹，周期性从随机位置产生
/// </summary>
public sealed class RippleCanvas
{
    private struct Ripple
    {
        public Vector2 Center;
        public float Radius;
        public float MaxRadius;
        public float Life;
        public float MaxLife;
        public float LineWidth;
    }

    private readonly Ripple[] _ripples;
    private readonly int _maxRipples;
    private readonly float _spawnInterval;
    private float _spawnTimer;

    /// <summary>
    /// 初始化波纹画布
    /// </summary>
    /// <param name="maxRipples">最大同时波纹数</param>
    /// <param name="spawnInterval">自动生成间隔（秒）</param>
    public RippleCanvas(int maxRipples = 6, float spawnInterval = 1.5f)
    {
        _maxRipples = maxRipples;
        _ripples = new Ripple[maxRipples];
        _spawnInterval = spawnInterval;
        _spawnTimer = Math.Max(0, _spawnInterval - 0.3f);
    }

    /// <summary>每帧更新波纹状态</summary>
    public void Update(float dt, Vector2 min, Vector2 max)
    {
        // 自动生成波纹
        _spawnTimer += dt;
        if (_spawnTimer >= _spawnInterval)
        {
            _spawnTimer = 0f;
            SpawnRipple(min, max);
        }

        // 更新波纹
        for (var i = 0; i < _ripples.Length; i++)
        {
            ref var r = ref _ripples[i];
            if (r.Life <= 0f) continue;
            r.Life -= dt;
            var progress = 1f - r.Life / r.MaxLife;
            r.Radius = r.MaxRadius * progress;
        }
    }

    /// <summary>从指定位置触发一个点击波纹</summary>
    public void TriggerClick(Vector2 pos, float maxRadius = 40f)
    {
        SpawnRippleAt(pos, maxRadius, 0.5f, 1.5f);
    }

    /// <summary>绘制所有活跃波纹</summary>
    public void Draw(ImDrawListPtr dl)
    {
        var accent = Theme.Colors.AccentBlue;

        for (var i = 0; i < _ripples.Length; i++)
        {
            ref var r = ref _ripples[i];
            if (r.Life <= 0f || r.Radius <= 0f) continue;

            var lifeRatio = Math.Max(0f, r.Life / r.MaxLife);
            var numSegments = Math.Max(32, (int)(MathF.Tau * r.Radius / 3f));
            var thickness = r.LineWidth * (0.3f + 0.7f * lifeRatio);

            var glowU32 = ImGui.ColorConvertFloat4ToU32(
                new Vector4(accent.X, accent.Y, accent.Z, lifeRatio * 0.18f));
            dl.PathArcTo(r.Center, r.Radius, 0f, MathF.PI * 2f, numSegments);
            dl.PathStroke(glowU32, 0, thickness + 4f);

            var mainU32 = ImGui.ColorConvertFloat4ToU32(
                new Vector4(accent.X, accent.Y, accent.Z, lifeRatio * 0.7f));
            dl.PathArcTo(r.Center, r.Radius, 0f, MathF.PI * 2f, numSegments);
            dl.PathStroke(mainU32, 0, thickness);
        }
    }

    private void SpawnRipple(Vector2 min, Vector2 max)
    {
        var center = new Vector2(
            min.X + (max.X - min.X) * (0.3f + Random.Shared.NextSingle() * 0.4f),
            min.Y + (max.Y - min.Y) * (0.3f + Random.Shared.NextSingle() * 0.4f));
        SpawnRippleAt(center, 60f + Random.Shared.NextSingle() * 80f,
            2f + Random.Shared.NextSingle(), 2.5f);
    }

    private void SpawnRippleAt(Vector2 center, float maxRadius, float maxLife, float lineWidth)
    {
        var oldestIdx = -1;
        var oldestLife = float.MaxValue;

        for (var i = 0; i < _ripples.Length; i++)
        {
            if (_ripples[i].Life <= 0f)
            {
                oldestIdx = i;
                break;
            }
            if (_ripples[i].Life < oldestLife)
            {
                oldestLife = _ripples[i].Life;
                oldestIdx = i;
            }
        }

        if (oldestIdx < 0) return;

        ref var r = ref _ripples[oldestIdx];
        r.Center = center;
        r.Radius = 3f;
        r.MaxRadius = maxRadius;
        r.MaxLife = maxLife;
        r.Life = maxLife;
        r.LineWidth = lineWidth;
    }
}
