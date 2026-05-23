using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

/// <summary>
/// 粒子系统 — 浮动光点，在指定矩形区域内随机生成和漂移
/// </summary>
public sealed class ParticleSystem
{
    private struct Particle
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Size;
        public float Life;
        public float MaxLife;
    }

    private readonly Particle[] _pool;
    private readonly int _maxParticles;
    private readonly float _spawnRate;
    private float _spawnAccum;

    /// <summary>
    /// 初始化粒子系统
    /// </summary>
    /// <param name="maxParticles">粒子池上限</param>
    /// <param name="spawnRate">每秒生成粒子数</param>
    public ParticleSystem(int maxParticles = 60, float spawnRate = 8f)
    {
        _maxParticles = maxParticles;
        _pool = new Particle[maxParticles];
        _spawnRate = spawnRate;
    }

    /// <summary>每帧更新粒子状态</summary>
    public void Update(float dt, Vector2 min, Vector2 max)
    {
        // 生成新粒子
        _spawnAccum += _spawnRate * dt;
        while (_spawnAccum >= 1f)
        {
            _spawnAccum -= 1f;
            SpawnParticle(min, max);
        }

        // 更新现有粒子
        for (var i = 0; i < _pool.Length; i++)
        {
            ref var p = ref _pool[i];
            if (p.Life <= 0f) continue;

            p.Life -= dt;
            p.Pos += p.Vel * dt;

            // 超出区域则反弹
            if (p.Pos.X < min.X || p.Pos.X > max.X) p.Vel.X *= -1f;
            if (p.Pos.Y < min.Y || p.Pos.Y > max.Y) p.Vel.Y *= -1f;
            p.Pos = Vector2.Clamp(p.Pos, min, max);
        }
    }

    /// <summary>绘制所有活跃粒子</summary>
    public void Draw(ImDrawListPtr dl)
    {
        var accent = Theme.Colors.AccentBlue;

        for (var i = 0; i < _pool.Length; i++)
        {
            ref var p = ref _pool[i];
            if (p.Life <= 0f) continue;

            var alpha = Math.Max(0f, p.Life / p.MaxLife);
            var color = new Vector4(accent.X, accent.Y, accent.Z, alpha * 0.35f);
            dl.AddCircleFilled(p.Pos, p.Size * (0.5f + alpha * 0.5f),
                ImGui.ColorConvertFloat4ToU32(color));
        }
    }

    private void SpawnParticle(Vector2 min, Vector2 max)
    {
        // 复用最旧的已死粒子槽位
        var oldestIdx = -1;
        var oldestLife = float.MaxValue;

        for (var i = 0; i < _pool.Length; i++)
        {
            if (_pool[i].Life <= 0f)
            {
                oldestIdx = i;
                break;
            }
            if (_pool[i].Life < oldestLife)
            {
                oldestLife = _pool[i].Life;
                oldestIdx = i;
            }
        }

        if (oldestIdx < 0) return;

        ref var p = ref _pool[oldestIdx];
        p.Pos = new Vector2(
            min.X + Random.Shared.NextSingle() * (max.X - min.X),
            min.Y + Random.Shared.NextSingle() * (max.Y - min.Y));
        p.Vel = new Vector2(
            (Random.Shared.NextSingle() - 0.5f) * 12f,
            (Random.Shared.NextSingle() - 0.5f) * 12f);
        p.Size = 2f + Random.Shared.NextSingle() * 4f;
        p.MaxLife = 2f + Random.Shared.NextSingle() * 2f;
        p.Life = p.MaxLife;
    }
}
