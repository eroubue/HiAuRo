using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

/// <summary>
/// 星座连线特效 — 随机光点缓慢漂移 + 鼠标跟随星群，近邻连线时隐时现
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

        // 鼠标跟随星专用
        public float OffsetDist;
        public float OffsetAngle;
        public float RotSpeed;
        public float DriftPhaseX;
        public float DriftPhaseY;
        public float DriftFreqX;
        public float DriftFreqY;
        public float DriftAmp;
    }

    private const int NormalCount = 25;
    private const int CursorCount = 10;
    private const float FadeSpeed = 0.8f;
    private const float LeaveThreshold = 1.5f;
    private const float CursorConnThreshold = 100f;

    private readonly Star[] _stars;
    private bool _initialized;
    private float _connThreshold;
    private float _cursorAlpha;
    private float _leaveTimer;
    private Vector2 _smoothedCursor;

    public ConstellationEffect()
    {
        _stars = new Star[NormalCount + CursorCount];
    }

    private void InitAll(Vector2 min, Vector2 max)
    {
        var w = max.X - min.X;
        var h = max.Y - min.Y;
        _connThreshold = MathF.Max(80f, h * 0.25f);

        var center = (min + max) * 0.5f;
        _smoothedCursor = center;

        for (var i = 0; i < _stars.Length; i++)
        {
            ref var s = ref _stars[i];
            if (i < NormalCount)
            {
                s.Pos = new Vector2(
                    min.X + Random.Shared.NextSingle() * w,
                    min.Y + Random.Shared.NextSingle() * h);
                var angle = Random.Shared.NextSingle() * MathF.Tau;
                var speed = 5f + Random.Shared.NextSingle() * 10f;
                s.Vel = new Vector2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed);
                s.Radius = 2f + Random.Shared.NextSingle() * 1f;
            }
            else
            {
                s.OffsetDist = 40f + Random.Shared.NextSingle() * 80f;
                s.OffsetAngle = Random.Shared.NextSingle() * MathF.Tau;
                s.RotSpeed = MathF.Sqrt(Random.Shared.NextSingle()) * (25f * MathF.PI / 180f);
                if (Random.Shared.Next(2) == 0) s.RotSpeed = -s.RotSpeed;
                s.Radius = 2f + Random.Shared.NextSingle() * 1f;
                s.DriftPhaseX = Random.Shared.NextSingle() * MathF.Tau;
                s.DriftPhaseY = Random.Shared.NextSingle() * MathF.Tau;
                s.DriftFreqX = 0.5f + Random.Shared.NextSingle() * 1f;
                s.DriftFreqY = 0.5f + Random.Shared.NextSingle() * 1f;
                s.DriftAmp = 5f + Random.Shared.NextSingle() * 8f;
                s.Pos = center;
            }
            s.FlickerPhase = Random.Shared.NextSingle() * MathF.Tau;
            s.FlickerFreq = 0.8f + Random.Shared.NextSingle() * 2f;
        }
        _initialized = true;
    }

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        if (!_initialized)
            InitAll(min, max);

        var center = (min + max) * 0.5f;

        // 鼠标跟随星状态
        var mouse = ImGui.GetIO().MousePos;
        var inWindow = mouse.X >= min.X && mouse.X <= max.X
                    && mouse.Y >= min.Y && mouse.Y <= max.Y;

        if (inWindow)
        {
            _leaveTimer = 0f;
            _cursorAlpha = Math.Min(1f, _cursorAlpha + dt * FadeSpeed);
            _smoothedCursor = Vector2.Lerp(_smoothedCursor, mouse, MathF.Min(1f, dt * 8f));
        }
        else
        {
            _leaveTimer += dt;
            if (_leaveTimer > LeaveThreshold)
            {
                _cursorAlpha = Math.Max(0f, _cursorAlpha - dt * FadeSpeed);
                _smoothedCursor = Vector2.Lerp(_smoothedCursor, center, MathF.Min(1f, dt * 2f));
            }
        }

        for (var i = 0; i < _stars.Length; i++)
        {
            ref var s = ref _stars[i];
            s.FlickerPhase += s.FlickerFreq * dt;

            if (i < NormalCount)
            {
                s.Pos += s.Vel * dt;
                if (s.Pos.X < min.X) { s.Pos.X = min.X; s.Vel.X = MathF.Abs(s.Vel.X); }
                if (s.Pos.X > max.X) { s.Pos.X = max.X; s.Vel.X = -MathF.Abs(s.Vel.X); }
                if (s.Pos.Y < min.Y) { s.Pos.Y = min.Y; s.Vel.Y = MathF.Abs(s.Vel.Y); }
                if (s.Pos.Y > max.Y) { s.Pos.Y = max.Y; s.Vel.Y = -MathF.Abs(s.Vel.Y); }
            }
            else
            {
                s.OffsetAngle += s.RotSpeed * dt;
                s.DriftPhaseX += s.DriftFreqX * dt;
                s.DriftPhaseY += s.DriftFreqY * dt;

                var driftX = MathF.Sin(s.DriftPhaseX) * s.DriftAmp;
                var driftY = MathF.Sin(s.DriftPhaseY) * s.DriftAmp;
                var ox = MathF.Cos(s.OffsetAngle) * s.OffsetDist + driftX;
                var oy = MathF.Sin(s.OffsetAngle) * s.OffsetDist + driftY;
                var target = _smoothedCursor + new Vector2(ox, oy);
                s.Pos = Vector2.Lerp(s.Pos, target, MathF.Min(1f, dt * 4f));
            }
        }
    }

    public void Draw(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax)
    {
        dl.PushClipRect(winMin, winMax, true);

        var accent = Theme.Colors.AccentBlue;

        // 连线 — 所有星点互相检测
        for (var i = 0; i < _stars.Length; i++)
        {
            for (var j = i + 1; j < _stars.Length; j++)
            {
                var pi = _stars[i].Pos;
                var pj = _stars[j].Pos;
                var dx = pi.X - pj.X;
                var dy = pi.Y - pj.Y;
                var dist = MathF.Sqrt(dx * dx + dy * dy);

                var threshold = _connThreshold;
                var lineAlphaBase = 0.25f;

                // 涉及鼠标跟随星时用更大阈值和淡入
                var iCursor = i >= NormalCount;
                var jCursor = j >= NormalCount;
                if (iCursor || jCursor)
                {
                    threshold = CursorConnThreshold;
                    lineAlphaBase = 0.3f;
                }

                if (dist < threshold)
                {
                    var t = 1f - dist / threshold;
                    var lineAlpha = t * lineAlphaBase;

                    // 鼠标跟随星的连线随 _cursorAlpha 淡入淡出
                    if (iCursor || jCursor)
                        lineAlpha *= _cursorAlpha;

                    var lineColor = ImGui.ColorConvertFloat4ToU32(
                        new Vector4(accent.X, accent.Y, accent.Z, lineAlpha));
                    dl.AddLine(pi, pj, lineColor, 1f);
                }
            }
        }

        // 光点
        for (var i = 0; i < _stars.Length; i++)
        {
            ref var s = ref _stars[i];
            var isCursor = i >= NormalCount;
            var flicker = (MathF.Sin(s.FlickerPhase) + 1f) * 0.5f;

            float alpha, glowSize, glowAlpha;
            if (isCursor)
            {
                alpha = (0.6f + flicker * 0.2f) * _cursorAlpha;
                glowSize = 7f;
                glowAlpha = alpha * 0.2f;
            }
            else
            {
                alpha = 0.5f + flicker * 0.3f;
                glowSize = 4f;
                glowAlpha = alpha * 0.15f;
            }

            dl.AddCircleFilled(s.Pos, s.Radius + glowSize,
                ImGui.ColorConvertFloat4ToU32(new Vector4(accent.X, accent.Y, accent.Z, glowAlpha)));
            dl.AddCircleFilled(s.Pos, s.Radius,
                ImGui.ColorConvertFloat4ToU32(new Vector4(accent.X, accent.Y, accent.Z, alpha)));
        }

        dl.PopClipRect();
    }
}
