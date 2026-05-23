using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

/// <summary>
/// 星座连线特效 — 随机光点缓慢漂移，近邻连线时隐时现
/// </summary>
public sealed class ConstellationEffect
{
    private struct Star
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Radius;
        public float FlickerPhase;
        public float FlickerFreq;
    }

    private readonly Star[] _stars;
    private bool _initialized;
    private float _connThreshold;

    public ConstellationEffect(int count = 25)
    {
        _stars = new Star[count];
    }

    private void InitAll(Vector2 min, Vector2 max)
    {
        var w = max.X - min.X;
        var h = max.Y - min.Y;
        _connThreshold = MathF.Max(80f, h * 0.25f);

        for (var i = 0; i < _stars.Length; i++)
        {
            ref var s = ref _stars[i];
            s.Pos = new Vector2(
                min.X + Random.Shared.NextSingle() * w,
                min.Y + Random.Shared.NextSingle() * h);
            var angle = Random.Shared.NextSingle() * MathF.Tau;
            var speed = 5f + Random.Shared.NextSingle() * 10f;
            s.Vel = new Vector2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed);
            s.Radius = 2f + Random.Shared.NextSingle() * 1f;
            s.FlickerPhase = Random.Shared.NextSingle() * MathF.Tau;
            s.FlickerFreq = 0.8f + Random.Shared.NextSingle() * 2f;
        }
        _initialized = true;
    }

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        if (!_initialized)
            InitAll(min, max);

        for (var i = 0; i < _stars.Length; i++)
        {
            ref var s = ref _stars[i];
            s.Pos += s.Vel * dt;
            s.FlickerPhase += s.FlickerFreq * dt;

            // 边缘反弹
            if (s.Pos.X < min.X) { s.Pos.X = min.X; s.Vel.X = MathF.Abs(s.Vel.X); }
            if (s.Pos.X > max.X) { s.Pos.X = max.X; s.Vel.X = -MathF.Abs(s.Vel.X); }
            if (s.Pos.Y < min.Y) { s.Pos.Y = min.Y; s.Vel.Y = MathF.Abs(s.Vel.Y); }
            if (s.Pos.Y > max.Y) { s.Pos.Y = max.Y; s.Vel.Y = -MathF.Abs(s.Vel.Y); }
        }
    }

    public void Draw(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax)
    {
        dl.PushClipRect(winMin, winMax, true);

        var accent = Theme.Colors.AccentBlue;

        // 连线
        for (var i = 0; i < _stars.Length; i++)
        {
            for (var j = i + 1; j < _stars.Length; j++)
            {
                var dx = _stars[i].Pos.X - _stars[j].Pos.X;
                var dy = _stars[i].Pos.Y - _stars[j].Pos.Y;
                var dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist < _connThreshold)
                {
                    var t = 1f - dist / _connThreshold;
                    var lineAlpha = t * 0.25f;
                    var lineColor = ImGui.ColorConvertFloat4ToU32(
                        new Vector4(accent.X, accent.Y, accent.Z, lineAlpha));
                    dl.AddLine(_stars[i].Pos, _stars[j].Pos, lineColor, 1f);
                }
            }
        }

        // 光点
        for (var i = 0; i < _stars.Length; i++)
        {
            ref var s = ref _stars[i];
            var flicker = (MathF.Sin(s.FlickerPhase) + 1f) * 0.5f;
            var alpha = 0.5f + flicker * 0.3f;

            // 辉光
            var glowAlpha = alpha * 0.15f;
            dl.AddCircleFilled(s.Pos, s.Radius + 4f,
                ImGui.ColorConvertFloat4ToU32(new Vector4(accent.X, accent.Y, accent.Z, glowAlpha)));

            // 核心
            dl.AddCircleFilled(s.Pos, s.Radius,
                ImGui.ColorConvertFloat4ToU32(new Vector4(accent.X, accent.Y, accent.Z, alpha)));
        }

        dl.PopClipRect();
    }
}
