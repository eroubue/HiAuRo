using System.Numerics;
using System.Threading.Tasks;

namespace HiAuRo.ImGuiLib.Effects;

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

    private Vector2 _mousePos;
    private bool _mouseInWindow;
    private bool _mouseClicked;

    private Task? _computeTask;
    private FrameData _front = new();
    private FrameData _back = new();

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
            if (t.Highlighted) t.HighlightFade = 1f;
            else t.HighlightFade = Math.Max(0f, t.HighlightFade - dt * 2f);
        }

        var mouse = ImGui.GetIO().MousePos;
        _mouseInWindow = mouse.X >= min.X && mouse.X <= max.X && mouse.Y >= min.Y && mouse.Y <= max.Y;
        _mousePos = mouse;
        _mouseClicked = ImGui.IsMouseClicked(ImGuiMouseButton.Left);

        if (_computeTask == null || _computeTask.IsCompleted)
        {
            if (_computeTask?.IsCompleted == true)
                SwapBuffers();
            var scan = _scanAngle;
            var mpos = _mousePos;
            var mIn = _mouseInWindow;
            var mClick = _mouseClicked;
            _computeTask = Task.Run(() => ComputeFrameData(scan, min, max, mpos, mIn, mClick));
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

        var data = Volatile.Read(ref _front);
        if (data.GridLines == null) { dl.PopClipRect(); return; }

        foreach (var (a, b, col, thick) in data.GridLines)
            dl.AddLine(a, b, col, thick);

        if (data.Circles != null)
            foreach (var (center, radius, col, segs, thick) in data.Circles)
                dl.AddCircle(center, radius, col, segs, thick);

        if (data.CrossLines != null)
            foreach (var (a, b, col, thick) in data.CrossLines)
                dl.AddLine(a, b, col, thick);

        if (data.SweepPts != null)
        {
            dl.PathLineTo(data.SweepPts[0]);
            for (var i = 1; i < data.SweepPts.Length; i++)
                dl.PathLineTo(data.SweepPts[i]);
            dl.PathFillConvex(data.SweepCol);
        }

        if (data.ScanLines != null)
            foreach (var (a, b, col, thick) in data.ScanLines)
                dl.AddLine(a, b, col, thick);

        if (data.Targets != null)
            foreach (var (pos, size, col, hasGlow, glowSize, glowCol) in data.Targets)
        {
            if (hasGlow) dl.AddCircleFilled(pos, glowSize, glowCol);
            dl.AddCircleFilled(pos, size, col);
        }

        if (data.HasMouseTarget && data.MouseTarget.HasValue)
        {
            var mt = data.MouseTarget.Value;
            dl.AddCircle(mt.Pos, mt.Radius, mt.Col, mt.Segs, mt.Thick);
            dl.AddLine(mt.Pos + new Vector2(-8, 0), mt.Pos + new Vector2(8, 0), mt.DimCol, 1f);
            dl.AddLine(mt.Pos + new Vector2(0, -8), mt.Pos + new Vector2(0, 8), mt.DimCol, 1f);
            dl.AddText(mt.TextPos, mt.TextCol, mt.Text);
        }

        if (data.HasLockedTarget && data.LockedTarget.HasValue)
        {
            var lt = data.LockedTarget.Value;
            dl.AddCircle(lt.Pos, lt.Radius, lt.Col, lt.Segs, lt.Thick);
            dl.AddText(lt.TextPos, lt.TextCol, lt.Text);
        }

        dl.PopClipRect();
    }

    private void SwapBuffers()
    {
        var tmp = _front;
        _front = _back;
        _back = tmp;
    }

    private void ComputeFrameData(float scanAngle, Vector2 min, Vector2 max, Vector2 mousePos, bool mouseInWindow, bool mouseClicked)
    {
        var center = (min + max) * 0.5f;
        var radius = GetRadarRadius(min, max);

        var back = _back;

        if (radius < 20f)
        {
            back.GridLines = null;
            return;
        }

        var gridLines = new List<(Vector2 A, Vector2 B, uint Col, float Thick)>();
        var gridCol = EffectUtils.PackColor(0, 1, 1, 0.03f);
        var spacing = 30f;
        for (var x = min.X + spacing; x < max.X; x += spacing)
            gridLines.Add((new Vector2(x, min.Y), new Vector2(x, max.Y), gridCol, 0.5f));
        for (var y = min.Y + spacing; y < max.Y; y += spacing)
            gridLines.Add((new Vector2(min.X, y), new Vector2(max.X, y), gridCol, 0.5f));
        back.GridLines = gridLines.ToArray();

        var ringCol = EffectUtils.PackColor(0, 1, 1, 0.12f);
        var circles = new (Vector2 Center, float Radius, uint Col, int Segs, float Thick)[RingCount + 1];
        for (var i = 1; i <= RingCount; i++)
            circles[i - 1] = (center, (i / (float)(RingCount + 1)) * radius, ringCol, 64, 1f);
        circles[RingCount] = (center, radius, EffectUtils.PackColor(0, 1, 1, 0.3f), 64, 1.5f);
        back.Circles = circles;

        var crossCol = EffectUtils.PackColor(0, 1, 1, 0.15f);
        back.CrossLines =
        [
            (new Vector2(center.X - radius, center.Y), new Vector2(center.X + radius, center.Y), crossCol, 1f),
            (new Vector2(center.X, center.Y - radius), new Vector2(center.X, center.Y + radius), crossCol, 1f),
        ];

        var steps = 20;
        var sweepAngle = 0.5f;
        var sweepPts = new Vector2[steps + 2];
        sweepPts[0] = center;
        for (var i = 0; i <= steps; i++)
        {
            var t = i / (float)steps;
            var a = scanAngle - sweepAngle * t;
            sweepPts[i + 1] = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
        }
        back.SweepPts = sweepPts;
        back.SweepCol = EffectUtils.PackColor(0, 1, 1, 0.08f);

        var scanEnd = center + new Vector2(MathF.Cos(scanAngle), MathF.Sin(scanAngle)) * radius;
        back.ScanLines =
        [
            (center, scanEnd, EffectUtils.PackColor(0, 1, 1, 0.1f), 4f),
            (center, scanEnd, EffectUtils.PackColor(0, 1, 1, 0.6f), 1f),
        ];

        var targets = new (Vector2 Pos, float Size, uint Col, bool HasGlow, float GlowSize, uint GlowCol)[_targets.Length];
        for (var i = 0; i < _targets.Length; i++)
        {
            ref var tgt = ref _targets[i];
            var fade = tgt.HighlightFade;
            var alpha = 0.3f + fade * 0.7f;
            var size = 3f + fade * 3f;
            targets[i] = (tgt.Pos, size, EffectUtils.PackColor(0, 1, 0.3f, alpha), fade > 0.1f, size + 6f, EffectUtils.PackColor(0, 1, 0.3f, fade * 0.2f));
        }
        back.Targets = targets;

        if (mouseInWindow)
        {
            var dist = Vector2.Distance(mousePos, center);
            var angle = MathF.Atan2(mousePos.Y - center.Y, mousePos.X - center.X) * 180f / MathF.PI;
            var bearing = ((angle % 360) + 360) % 360;
            back.HasMouseTarget = true;
            back.MouseTarget = (mousePos, 5f, EffectUtils.PackColor(1, 0.8f, 0, 0.7f), 4, 1.5f,
                EffectUtils.PackColor(1, 0.8f, 0, 0.4f),
                mousePos + new Vector2(10, -6), EffectUtils.PackColor(1, 0.8f, 0, 0.7f),
                $"{dist:F0}m {bearing:F0}°");
        }
        else
        {
            back.HasMouseTarget = false;
            back.MouseTarget = null;
        }

        if (mouseClicked && mouseInWindow)
            _lockedTarget = mousePos;

        if (_lockedTarget != Vector2.Zero)
        {
            back.HasLockedTarget = true;
            var dist = Vector2.Distance(_lockedTarget, center);
            var ang = MathF.Atan2(_lockedTarget.Y - center.Y, _lockedTarget.X - center.X) * 180f / MathF.PI;
            back.LockedTarget = (_lockedTarget, 6f, EffectUtils.PackColor(1, 0, 0, 0.8f), 4, 2f,
                _lockedTarget + new Vector2(10, -8), EffectUtils.PackColor(1, 0.3f, 0.3f, 0.8f),
                $"LOCK {dist:F0}px {ang:F0}°");
        }
        else
        {
            back.HasLockedTarget = false;
            back.LockedTarget = null;
        }
    }

    private sealed class FrameData
    {
        public (Vector2 A, Vector2 B, uint Col, float Thick)[]? GridLines;
        public (Vector2 Center, float Radius, uint Col, int Segs, float Thick)[]? Circles;
        public (Vector2 A, Vector2 B, uint Col, float Thick)[]? CrossLines;
        public Vector2[]? SweepPts;
        public uint SweepCol;
        public (Vector2 A, Vector2 B, uint Col, float Thick)[]? ScanLines;
        public (Vector2 Pos, float Size, uint Col, bool HasGlow, float GlowSize, uint GlowCol)[]? Targets;
        public bool HasMouseTarget;
        public (Vector2 Pos, float Radius, uint Col, int Segs, float Thick, uint DimCol, Vector2 TextPos, uint TextCol, string Text)? MouseTarget;
        public bool HasLockedTarget;
        public (Vector2 Pos, float Radius, uint Col, int Segs, float Thick, Vector2 TextPos, uint TextCol, string Text)? LockedTarget;
    }
}
