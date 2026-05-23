using System.Numerics;
using System.Threading.Tasks;

namespace HiAuRo.ImGuiLib.Effects;

public sealed class GlitchEffect
{
    private struct TearLine
    {
        public float Y;
        public float Height;
        public float OffsetX;
        public float Alpha;
        public float Life;
        public float MaxLife;
    }

    private struct EmpRing
    {
        public Vector2 Center;
        public float Radius;
        public float MaxRadius;
        float _life;

        public float Life
        {
            get => _life;
            set => _life = Math.Clamp(value, 0f, 1f);
        }
    }

    private const int NormalTearCount = 5;
    private const int BurstTearCount = 15;
    private const float BurstIntervalMin = 3f;
    private const float BurstIntervalMax = 5f;
    private const float BurstDurationMin = 0.1f;
    private const float BurstDurationMax = 0.3f;

    private static readonly Vector3 Cyan = new(0, 1, 1);
    private static readonly Vector3 Magenta = new(1, 0, 1);
    private static readonly Vector3 Yellow = new(1, 1, 0);

    private readonly List<TearLine> _tears = new();
    private readonly List<EmpRing> _empRings = new();
    private float _burstTimer;
    private float _burstDuration;
    private bool _inBurst;
    private float _time;
    private Vector2 _lastMouse;
    private float _mouseSpeed;
    private bool _initialized;

    private Task? _computeTask;
    private FrameData _front = new();
    private FrameData _back = new();

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        _time += dt;
        var h = max.Y - min.Y;

        if (!_initialized)
        {
            _burstTimer = BurstIntervalMin + Random.Shared.NextSingle() * (BurstIntervalMax - BurstIntervalMin);
            _lastMouse = ImGui.GetIO().MousePos;
            _initialized = true;
        }

        var mouse = ImGui.GetIO().MousePos;
        var dv = mouse - _lastMouse;
        _mouseSpeed = Vector2.Dot(dv, dv);
        _lastMouse = mouse;

        _burstTimer -= dt;
        if (_burstTimer <= 0f)
        {
            _inBurst = true;
            _burstDuration = BurstDurationMin + Random.Shared.NextSingle() * (BurstDurationMax - BurstDurationMin);
            _burstTimer = BurstIntervalMin + Random.Shared.NextSingle() * (BurstIntervalMax - BurstIntervalMin);
        }

        if (_inBurst)
        {
            _burstDuration -= dt;
            if (_burstDuration <= 0f) _inBurst = false;
        }

        var isBurst = _inBurst || _mouseSpeed > 2500f;
        var count = isBurst ? BurstTearCount : NormalTearCount;
        while (_tears.Count < count) SpawnTear(min, max);

        for (var i = _tears.Count - 1; i >= 0; i--)
        {
            var t = _tears[i];
            t.Life -= dt;
            if (t.Life <= 0f) { _tears.RemoveAt(i); continue; }
            t.Alpha = t.Life / t.MaxLife;
            _tears[i] = t;
        }

        for (var i = _empRings.Count - 1; i >= 0; i--)
        {
            var r = _empRings[i];
            r.Radius += dt * 300f;
            r.Life -= dt * 2.5f;
            if (r.Life <= 0f || r.Radius > r.MaxRadius) _empRings.RemoveAt(i);
            else _empRings[i] = r;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            if (mouse.X >= min.X && mouse.X <= max.X && mouse.Y >= min.Y && mouse.Y <= max.Y)
            {
                _empRings.Add(new EmpRing { Center = mouse, Radius = 0f, MaxRadius = Math.Max(max.X - min.X, h) * 0.5f, Life = 1f });
            }
        }

        if (_computeTask == null || _computeTask.IsCompleted)
        {
            if (_computeTask?.IsCompleted == true)
                SwapBuffers();
            var time = _time;
            var inBurst2 = _inBurst;
            _computeTask = Task.Run(() => ComputeFrameData(time, min, max, inBurst2));
        }
    }

    private void SpawnTear(Vector2 min, Vector2 max)
    {
        var h = max.Y - min.Y;
        var life = 0.05f + Random.Shared.NextSingle() * 0.15f;
        _tears.Add(new TearLine
        {
            Y = min.Y + Random.Shared.NextSingle() * h,
            Height = 1f + Random.Shared.NextSingle() * 3f,
            OffsetX = (Random.Shared.NextSingle() * 2f - 1f) * 8f,
            Alpha = 1f,
            Life = life,
            MaxLife = life,
        });
    }

    public void Draw(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax)
    {
        dl.PushClipRect(winMin, winMax, true);

        var data = Volatile.Read(ref _front);

        if (data.EdgeWaveTop != null)
        {
            dl.PathLineTo(data.EdgeWaveTop[0].A);
            for (var i = 0; i < data.EdgeWaveTop.Length; i++)
                dl.PathLineTo(data.EdgeWaveTop[i].B);
            dl.PathStroke(data.EdgeWaveCol, ImDrawFlags.None, 1f);
        }

        if (data.EdgeWaveBottom != null)
        {
            dl.PathLineTo(data.EdgeWaveBottom[0].A);
            for (var i = 0; i < data.EdgeWaveBottom.Length; i++)
                dl.PathLineTo(data.EdgeWaveBottom[i].B);
            dl.PathStroke(data.EdgeWaveCol, ImDrawFlags.None, 1f);
        }

        if (data.ChannelRects != null)
            foreach (var (min, max, col) in data.ChannelRects)
                dl.AddRectFilled(min, max, col);

        if (data.NoiseLines != null)
            foreach (var (a, b, col) in data.NoiseLines)
                dl.AddLine(a, b, col, 1f);

        if (data.EmpRings != null)
            foreach (var (center, radius, col, thick) in data.EmpRings)
                dl.AddCircle(center, radius, col, 0, thick);

        dl.PopClipRect();
    }

    private void SwapBuffers()
    {
        var tmp = _front;
        _front = _back;
        _back = tmp;
    }

    private void ComputeFrameData(float time, Vector2 min, Vector2 max, bool inBurst)
    {
        var w = max.X - min.X;
        var segLen = 20f;
        var amp = 3f;
        var waveCol = EffectUtils.PackColor(0, 1, 1, 0.15f);

        var topPts = new List<(Vector2 A, Vector2 B)>();
        for (var x = min.X; x < max.X - segLen; x += segLen)
        {
            var off = MathF.Sin(time * 5f + x * 0.05f) * amp;
            topPts.Add((new Vector2(x, min.Y + off), new Vector2(x + segLen, min.Y + MathF.Sin(time * 5f + (x + segLen) * 0.05f) * amp)));
        }

        var botPts = new List<(Vector2 A, Vector2 B)>();
        for (var x = min.X; x < max.X - segLen; x += segLen)
        {
            var off = MathF.Sin(time * 4.5f + x * 0.06f) * amp;
            botPts.Add((new Vector2(x, max.Y + off), new Vector2(x + segLen, max.Y + MathF.Sin(time * 4.5f + (x + segLen) * 0.06f) * amp)));
        }

        var channelRects = new List<(Vector2 Min, Vector2 Max, uint Col)>();
        foreach (var tear in _tears)
        {
            var alpha = tear.Alpha;
            channelRects.Add((new Vector2(min.X + tear.OffsetX - 2f, tear.Y), new Vector2(min.X + tear.OffsetX - 2f + w, tear.Y + tear.Height), EffectUtils.PackColor(Cyan.X, Cyan.Y, Cyan.Z, alpha * 0.4f)));
            channelRects.Add((new Vector2(min.X + tear.OffsetX, tear.Y), new Vector2(min.X + tear.OffsetX + w, tear.Y + tear.Height), EffectUtils.PackColor(Magenta.X, Magenta.Y, Magenta.Z, alpha * 0.4f)));
            channelRects.Add((new Vector2(min.X + tear.OffsetX + 2f, tear.Y), new Vector2(min.X + tear.OffsetX + 2f + w, tear.Y + tear.Height), EffectUtils.PackColor(Yellow.X, Yellow.Y, Yellow.Z, alpha * 0.4f)));
        }

        var noiseLines = new List<(Vector2 A, Vector2 B, uint Col)>();
        var noiseCount = 20 + (inBurst ? 40 : 0);
        for (var i = 0; i < noiseCount; i++)
        {
            var x = min.X + Random.Shared.NextSingle() * (max.X - min.X);
            var y = min.Y + Random.Shared.NextSingle() * (max.Y - min.Y);
            var len = 3f + Random.Shared.NextSingle() * 15f;
            var colIdx = Random.Shared.Next(3);
            var color = colIdx == 0 ? Cyan : colIdx == 1 ? Magenta : Yellow;
            var a = 0.1f + Random.Shared.NextSingle() * 0.3f;
            noiseLines.Add((new Vector2(x, y), new Vector2(x + len, y), EffectUtils.PackColor(color.X, color.Y, color.Z, a)));
        }

        var empRings = new (Vector2 Center, float Radius, uint Col, float Thick)[_empRings.Count];
        for (var i = 0; i < _empRings.Count; i++)
        {
            var ring = _empRings[i];
            var a = ring.Life * 0.6f;
            empRings[i] = (ring.Center, ring.Radius, EffectUtils.PackColor(0, 1, 1, a), 2f);
        }

        var back = _back;
        back.EdgeWaveTop = topPts.ToArray();
        back.EdgeWaveBottom = botPts.ToArray();
        back.EdgeWaveCol = waveCol;
        back.ChannelRects = channelRects.ToArray();
        back.NoiseLines = noiseLines.ToArray();
        back.EmpRings = empRings;
    }

    private sealed class FrameData
    {
        public (Vector2 A, Vector2 B)[]? EdgeWaveTop;
        public (Vector2 A, Vector2 B)[]? EdgeWaveBottom;
        public uint EdgeWaveCol;
        public (Vector2 Min, Vector2 Max, uint Col)[]? ChannelRects;
        public (Vector2 A, Vector2 B, uint Col)[]? NoiseLines;
        public (Vector2 Center, float Radius, uint Col, float Thick)[]? EmpRings;
    }
}
