using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

/// <summary>
/// 萤火虫特效 — 发光点做有机布朗运动，像萤火虫飘浮
/// </summary>
public sealed class FireflyEffect
{
    private struct Firefly
    {
        public Vector2 Pos;
        public Vector2 TargetPos;
        public float Size;
        public float BrightnessPhase;
        public float BrightnessFreq;
        public float Speed;
        public float RetargetTimer;
        public float RetargetInterval;
        public Vector3 Color;
    }

    private readonly Firefly[] _fireflies;
    private float _time;

    public FireflyEffect(int count = 28)
    {
        _fireflies = new Firefly[count];
    }

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        _time += dt;
        const float pad = 30f;
        var innerMinX = min.X + pad;
        var innerMinY = min.Y + pad;
        var innerMaxX = max.X - pad;
        var innerMaxY = max.Y - pad;
        var rangeX = innerMaxX - innerMinX;
        var rangeY = innerMaxY - innerMinY;

        for (var i = 0; i < _fireflies.Length; i++)
        {
            ref var f = ref _fireflies[i];

            if (f.RetargetInterval <= 0f)
            {
                f.Pos = new Vector2(
                    innerMinX + Random.Shared.NextSingle() * rangeX,
                    innerMinY + Random.Shared.NextSingle() * rangeY);
                f.TargetPos = f.Pos;
                f.Size = 3f + Random.Shared.NextSingle() * 5f;
                f.BrightnessPhase = Random.Shared.NextSingle() * MathF.Tau;
                f.BrightnessFreq = 1f + Random.Shared.NextSingle() * 2.5f;
                f.Speed = 30f + Random.Shared.NextSingle() * 60f;
                f.RetargetInterval = 1f + Random.Shared.NextSingle() * 1.5f;
                f.RetargetTimer = f.RetargetInterval;

                var colorRand = Random.Shared.NextSingle();
                f.Color = colorRand < 0.7f
                    ? new Vector3(0.7f, 1f, 0.2f)
                    : new Vector3(1f, 0.9f, 0.3f);
                continue;
            }

            f.RetargetTimer -= dt;
            if (f.RetargetTimer <= 0f)
            {
                f.TargetPos = new Vector2(
                    innerMinX + Random.Shared.NextSingle() * rangeX,
                    innerMinY + Random.Shared.NextSingle() * rangeY);
                f.RetargetInterval = 1f + Random.Shared.NextSingle() * 1.5f;
                f.RetargetTimer = f.RetargetInterval;
            }

            // 平滑跟随目标
            var dx = f.TargetPos.X - f.Pos.X;
            var dy = f.TargetPos.Y - f.Pos.Y;
            var dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist > 0.5f)
            {
                var moveSpeed = f.Speed * dt;
                var ratio = Math.Min(1f, moveSpeed / dist);
                f.Pos.X += dx * ratio;
                f.Pos.Y += dy * ratio;
            }

            f.BrightnessPhase += f.BrightnessFreq * dt;
        }
    }

    public void Draw(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax)
    {
        dl.PushClipRect(winMin, winMax, true);

        for (var i = 0; i < _fireflies.Length; i++)
        {
            ref var f = ref _fireflies[i];
            if (f.RetargetInterval <= 0f) continue;

            var brightness = (MathF.Sin(f.BrightnessPhase) + 1f) * 0.5f;
            brightness = 0.3f + brightness * 0.7f;

            // 外发光层
            var outerAlpha = brightness * 0.08f;
            dl.AddCircleFilled(f.Pos, f.Size * 3f,
                ImGui.ColorConvertFloat4ToU32(
                    new Vector4(f.Color.X, f.Color.Y, f.Color.Z, outerAlpha)));

            // 中层辉光
            var midAlpha = brightness * 0.2f;
            dl.AddCircleFilled(f.Pos, f.Size * 1.5f,
                ImGui.ColorConvertFloat4ToU32(
                    new Vector4(f.Color.X, f.Color.Y, f.Color.Z, midAlpha)));

            // 核心发光点
            var coreAlpha = brightness * 0.8f;
            dl.AddCircleFilled(f.Pos, f.Size,
                ImGui.ColorConvertFloat4ToU32(
                    new Vector4(f.Color.X, f.Color.Y, f.Color.Z, coreAlpha)));
        }

        dl.PopClipRect();
    }
}
