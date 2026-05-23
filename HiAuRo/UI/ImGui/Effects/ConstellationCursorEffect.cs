using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

/// <summary>
/// 鼠标跟随星座特效 — 星点围绕鼠标松散分布，缓慢绕行并随机漂移
/// </summary>
public sealed class ConstellationCursorEffect
{
    private struct Star
    {
        public float OffsetDist;
        public float OffsetAngle;
        public float RotSpeed;
        public float Radius;
        public float DriftPhaseX;
        public float DriftPhaseY;
        public float DriftFreqX;
        public float DriftFreqY;
        public float DriftAmp;
    }

    private readonly Star[] _stars;
    private float _alpha;
    private float _leaveTimer;
    private bool _initialized;

    private const float FadeSpeed = 0.8f;
    private const float LeaveThreshold = 1.5f;
    private const float ConnThreshold = 80f;

    public ConstellationCursorEffect(int count = 10)
    {
        _stars = new Star[count];
    }

    private void InitAll()
    {
        for (var i = 0; i < _stars.Length; i++)
        {
            ref var s = ref _stars[i];
            s.OffsetDist = 40f + Random.Shared.NextSingle() * 80f;
            s.OffsetAngle = Random.Shared.NextSingle() * MathF.Tau;
            s.RotSpeed = MathF.Sqrt(Random.Shared.NextSingle()) * (20f * MathF.PI / 180f);
            if (Random.Shared.Next(2) == 0) s.RotSpeed = -s.RotSpeed;
            s.Radius = 2f + Random.Shared.NextSingle() * 1f;
            s.DriftPhaseX = Random.Shared.NextSingle() * MathF.Tau;
            s.DriftPhaseY = Random.Shared.NextSingle() * MathF.Tau;
            s.DriftFreqX = 0.5f + Random.Shared.NextSingle() * 1f;
            s.DriftFreqY = 0.5f + Random.Shared.NextSingle() * 1f;
            s.DriftAmp = 5f + Random.Shared.NextSingle() * 8f;
        }
        _initialized = true;
    }

    private Vector2 StarPos(int i, Vector2 mouse)
    {
        ref var s = ref _stars[i];
        var driftX = MathF.Sin(s.DriftPhaseX) * s.DriftAmp;
        var driftY = MathF.Sin(s.DriftPhaseY) * s.DriftAmp;
        var ox = MathF.Cos(s.OffsetAngle) * s.OffsetDist + driftX;
        var oy = MathF.Sin(s.OffsetAngle) * s.OffsetDist + driftY;
        return mouse + new Vector2(ox, oy);
    }

    public void Update(float dt, Vector2 winMin, Vector2 winMax)
    {
        if (!_initialized) InitAll();

        var mouse = ImGui.GetIO().MousePos;
        var inWindow = mouse.X >= winMin.X && mouse.X <= winMax.X
                    && mouse.Y >= winMin.Y && mouse.Y <= winMax.Y;

        if (inWindow)
        {
            _leaveTimer = 0f;
            _alpha = Math.Min(1f, _alpha + dt * FadeSpeed);
        }
        else
        {
            _leaveTimer += dt;
            if (_leaveTimer > LeaveThreshold)
                _alpha = Math.Max(0f, _alpha - dt * FadeSpeed);
        }

        for (var i = 0; i < _stars.Length; i++)
        {
            ref var s = ref _stars[i];
            s.OffsetAngle += s.RotSpeed * dt;
            s.DriftPhaseX += s.DriftFreqX * dt;
            s.DriftPhaseY += s.DriftFreqY * dt;
        }
    }

    public void Draw(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax)
    {
        if (_alpha <= 0f) return;

        dl.PushClipRect(winMin, winMax, true);

        var mouse = ImGui.GetIO().MousePos;
        var accent = Theme.Colors.AccentBlue;

        // 连线
        for (var i = 0; i < _stars.Length; i++)
        {
            var pi = StarPos(i, mouse);
            for (var j = i + 1; j < _stars.Length; j++)
            {
                var pj = StarPos(j, mouse);
                var dx = pi.X - pj.X;
                var dy = pi.Y - pj.Y;
                var dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist < ConnThreshold)
                {
                    var t = 1f - dist / ConnThreshold;
                    var lineAlpha = (0.15f + t * 0.15f) * _alpha;
                    dl.AddLine(pi, pj,
                        ImGui.ColorConvertFloat4ToU32(
                            new Vector4(accent.X, accent.Y, accent.Z, lineAlpha)), 1f);
                }
            }
        }

        // 星点
        for (var i = 0; i < _stars.Length; i++)
        {
            var pos = StarPos(i, mouse);
            ref var s = ref _stars[i];

            var glowAlpha = 0.15f * _alpha;
            dl.AddCircleFilled(pos, 6f + s.Radius,
                ImGui.ColorConvertFloat4ToU32(
                    new Vector4(accent.X, accent.Y, accent.Z, glowAlpha)));

            var coreAlpha = (0.6f + 0.2f) * _alpha;
            dl.AddCircleFilled(pos, s.Radius,
                ImGui.ColorConvertFloat4ToU32(
                    new Vector4(accent.X, accent.Y, accent.Z, coreAlpha)));
        }

        dl.PopClipRect();
    }
}
