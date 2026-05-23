using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

/// <summary>
/// 雷达扫描仪特效 — 同心圆、十字准线、旋转扫描扇形、目标点、鼠标互动
/// </summary>
public sealed class RadarScannerEffect
{
    private struct Target
    {
        public Vector2 Pos;
        public float Ring;
        public float Angle;
        public bool Highlighted;
        public float HighlightFade;
    }

    private const int RingCount = 4;
    private const int TargetCount = 12;
    private const float ScanSpeed = MathF.PI / 2f;

    private readonly Target[] _targets = new Target[TargetCount];
    private float _scanAngle;
    private bool _initialized;
    private float _time;
    private Vector2 _lockedTarget;

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        _time += dt;
        _scanAngle += ScanSpeed * dt;

        if (!_initialized)
        {
            InitTargets(min, max);
            _initialized = true;
        }

        var center = (min + max) * 0.5f;
        var radius = GetRadarRadius(min, max);

        for (var i = 0; i < _targets.Length; i++)
        {
            ref var t = ref _targets[i];
            var targetAngle = MathF.Atan2(t.Pos.Y - center.Y, t.Pos.X - center.X);
            if (targetAngle < 0) targetAngle += MathF.Tau;

            var scan = _scanAngle % MathF.Tau;

            var diff = MathF.Abs(targetAngle - scan);
            if (diff > MathF.PI) diff = MathF.Tau - diff;

            t.Highlighted = diff < 0.15f;
            if (t.Highlighted)
                t.HighlightFade = 1f;
            else
                t.HighlightFade = Math.Max(0f, t.HighlightFade - dt * 2f);
        }
    }

    private void InitTargets(Vector2 min, Vector2 max)
    {
        var center = (min + max) * 0.5f;
        var radius = GetRadarRadius(min, max);

        for (var i = 0; i < _targets.Length; i++)
        {
            var ring = (i % RingCount + 1f) / (RingCount + 1f);
            var angle = Random.Shared.NextSingle() * MathF.Tau;
            var r = ring * radius;
            _targets[i] = new Target
            {
                Pos = center + new Vector2(MathF.Cos(angle) * r, MathF.Sin(angle) * r),
                Ring = ring,
                Angle = angle,
            };
        }
    }

    private static float GetRadarRadius(Vector2 min, Vector2 max)
    {
        return Math.Min(max.X - min.X, max.Y - min.Y) * 0.35f;
    }

    public void Draw(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax)
    {
        dl.PushClipRect(winMin, winMax, true);

        var center = (winMin + winMax) * 0.5f;
        var radius = GetRadarRadius(winMin, winMax);
        if (radius < 20f) { dl.PopClipRect(); return; }

        var accent = new Vector4(0, 1, 1, 1f);

        // 网格底
        DrawGrid(dl, winMin, winMax);

        // 同心圆
        for (var i = 1; i <= RingCount; i++)
        {
            var r = (i / (float)(RingCount + 1)) * radius;
            var ringColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, 0.12f));
            dl.AddCircle(center, r, ringColor, 64, 1f);
        }

        // 外圈
        var outerColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, 0.3f));
        dl.AddCircle(center, radius, outerColor, 64, 1.5f);

        // 十字准线
        var crossColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, 0.15f));
        dl.AddLine(new Vector2(center.X - radius, center.Y), new Vector2(center.X + radius, center.Y), crossColor, 1f);
        dl.AddLine(new Vector2(center.X, center.Y - radius), new Vector2(center.X, center.Y + radius), crossColor, 1f);

        // 扫描扇形
        DrawSweep(dl, center, radius);

        // 扫描线（双层辉光）
        var scanEnd = center + new Vector2(MathF.Cos(_scanAngle), MathF.Sin(_scanAngle)) * radius;
        var scanGlow = ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, 0.1f));
        var scanMain = ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, 0.6f));
        dl.AddLine(center, scanEnd, scanGlow, 4f);
        dl.AddLine(center, scanEnd, scanMain, 1f);

        // 目标点
        DrawTargets(dl, center);

        // 鼠标目标标注
        DrawMouseTarget(dl, winMin, winMax, center, radius);

        // 点击锁定跟踪
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            var mp = ImGui.GetMousePos();
            if (mp.X >= winMin.X && mp.X <= winMax.X && mp.Y >= winMin.Y && mp.Y <= winMax.Y)
                _lockedTarget = mp;
        }

        if (_lockedTarget != Vector2.Zero)
        {
            dl.AddCircle(_lockedTarget, 6f,
                ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 0.8f)), 4, 2f);
            var dist = Vector2.Distance(_lockedTarget, center);
            var ang = MathF.Atan2(_lockedTarget.Y - center.Y, _lockedTarget.X - center.X) * 180f / MathF.PI;
            dl.AddText(_lockedTarget + new Vector2(10, -8),
                ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0.3f, 0.3f, 0.8f)),
                $"LOCK {dist:F0}px {ang:F0}°");
        }

        dl.PopClipRect();
    }

    private void DrawSweep(ImDrawListPtr dl, Vector2 center, float radius)
    {
        var steps = 20;
        var sweepAngle = 0.5f;
        dl.PathLineTo(center);
        for (var i = 0; i <= steps; i++)
        {
            var t = i / (float)steps;
            var a = _scanAngle - sweepAngle * t;
            dl.PathLineTo(center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius);
        }
        var sweepColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, 0.08f));
        dl.PathFillConvex(sweepColor);
    }

    private void DrawTargets(ImDrawListPtr dl, Vector2 center)
    {
        for (var i = 0; i < _targets.Length; i++)
        {
            ref var t = ref _targets[i];
            var fade = t.HighlightFade;
            var baseAlpha = 0.3f;
            var alpha = baseAlpha + fade * 0.7f;
            var size = 3f + fade * 3f;

            var color = ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 0.3f, alpha));
            dl.AddCircleFilled(t.Pos, size, color);

            if (fade > 0.1f)
            {
                var glowColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 0.3f, fade * 0.2f));
                dl.AddCircleFilled(t.Pos, size + 6f, glowColor);
            }
        }
    }

    private void DrawMouseTarget(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector2 center, float radius)
    {
        var mouse = ImGui.GetIO().MousePos;
        if (mouse.X < min.X || mouse.X > max.X || mouse.Y < min.Y || mouse.Y > max.Y) return;

        var dist = Vector2.Distance(mouse, center);
        var angle = MathF.Atan2(mouse.Y - center.Y, mouse.X - center.X) * 180f / MathF.PI;
        var bearing = ((angle % 360) + 360) % 360;

        var mainColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0.8f, 0, 0.7f));
        var dimColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0.8f, 0, 0.4f));

        dl.AddCircle(mouse, 5f, mainColor, 4, 1.5f);
        dl.AddLine(mouse + new Vector2(-8, 0), mouse + new Vector2(8, 0), dimColor, 1f);
        dl.AddLine(mouse + new Vector2(0, -8), mouse + new Vector2(0, 8), dimColor, 1f);

        var text = $"{dist:F0}m {bearing:F0}°";
        dl.AddText(mouse + new Vector2(10, -6), mainColor, text);
    }

    private static void DrawGrid(ImDrawListPtr dl, Vector2 min, Vector2 max)
    {
        var gridColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, 0.03f));
        var spacing = 30f;
        for (var x = min.X + spacing; x < max.X; x += spacing)
            dl.AddLine(new Vector2(x, min.Y), new Vector2(x, max.Y), gridColor, 0.5f);
        for (var y = min.Y + spacing; y < max.Y; y += spacing)
            dl.AddLine(new Vector2(min.X, y), new Vector2(max.X, y), gridColor, 0.5f);
    }
}
