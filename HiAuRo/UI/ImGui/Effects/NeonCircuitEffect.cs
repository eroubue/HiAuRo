using System.Numerics;
using System.Threading.Tasks;

namespace HiAuRo.ImGuiLib.Effects;

public sealed class NeonCircuitEffect
{
    private struct Trace
    {
        public Vector2 A;
        public Vector2 B;
        public bool Horizontal;
    }

    private struct Node
    {
        public Vector2 Pos;
        public float PulsePhase;
        public float PulseFreq;
        public float BaseRadius;
    }

    private struct Pulse
    {
        public int TraceIdx;
        public float T;
        public float Speed;
        public Vector3 Color;
    }

    private const int TraceCount = 30;
    private const int MaxPulses = 40;

    private static readonly Vector3 NeonCyan = new(0, 1, 1);
    private static readonly Vector3 NeonMagenta = new(1, 0, 1);

    private readonly Trace[] _traces = new Trace[TraceCount];
    private readonly List<Node> _nodes = new();
    private readonly List<Pulse> _pulses = new();
    private bool _initialized;

    private Vector2 _mousePos;
    private bool _mouseClicked;

    private Task? _computeTask;
    private FrameData _front = new();
    private FrameData _back = new();

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        if (!_initialized) InitLayout(min, max);

        _mousePos = ImGui.GetIO().MousePos;
        _mouseClicked = ImGui.IsMouseClicked(ImGuiMouseButton.Left);

        for (var i = _pulses.Count - 1; i >= 0; i--)
        {
            var p = _pulses[i];
            p.T += p.Speed * dt;
            if (p.T > 1f) _pulses.RemoveAt(i);
            else _pulses[i] = p;
        }

        while (_pulses.Count < MaxPulses)
        {
            var idx = Random.Shared.Next(_traces.Length);
            var color = Random.Shared.NextSingle() < 0.6f ? NeonCyan : NeonMagenta;
            _pulses.Add(new Pulse { TraceIdx = idx, T = 0f, Speed = 0.3f + Random.Shared.NextSingle() * 0.8f, Color = color });
        }

        if (_mouseClicked)
        {
            foreach (var node in _nodes)
            {
                if (Vector2.Distance(_mousePos, node.Pos) < 15f)
                {
                    for (var j = 0; j < 5 && _pulses.Count < MaxPulses + 10; j++)
                    {
                        _pulses.Add(new Pulse { TraceIdx = Random.Shared.Next(_traces.Length), T = 0f, Speed = 0.8f + Random.Shared.NextSingle() * 1f, Color = new Vector3(1, 1, 0) });
                    }
                    break;
                }
            }
        }

        if (_computeTask == null || _computeTask.IsCompleted)
        {
            if (_computeTask?.IsCompleted == true)
                SwapBuffers();
            var mpos = _mousePos;
            _computeTask = Task.Run(() => ComputeFrameData(mpos));
        }
    }

    private void InitLayout(Vector2 min, Vector2 max)
    {
        var w = max.X - min.X;
        var h = max.Y - min.Y;
        var margin = 20f;
        var nodeSet = new HashSet<Vector2>();

        for (var i = 0; i < _traces.Length; i++)
        {
            var horizontal = Random.Shared.Next(2) == 0;
            if (horizontal)
            {
                var y = min.Y + margin + Random.Shared.NextSingle() * (h - margin * 2);
                var x1 = min.X + margin + Random.Shared.NextSingle() * (w - margin * 2) * 0.4f;
                var x2 = x1 + Random.Shared.NextSingle() * (w - margin * 2) * 0.5f;
                _traces[i] = new Trace { A = new Vector2(x1, y), B = new Vector2(x2, y), Horizontal = true };
            }
            else
            {
                var x = min.X + margin + Random.Shared.NextSingle() * (w - margin * 2);
                var y1 = min.Y + margin + Random.Shared.NextSingle() * (h - margin * 2) * 0.4f;
                var y2 = y1 + Random.Shared.NextSingle() * (h - margin * 2) * 0.5f;
                _traces[i] = new Trace { A = new Vector2(x, y1), B = new Vector2(x, y2), Horizontal = false };
            }

            AddNode(_traces[i].A);
            AddNode(_traces[i].B);
        }

        void AddNode(Vector2 pos)
        {
            foreach (var n in nodeSet)
                if (Vector2.Distance(n, pos) < 8f) return;
            nodeSet.Add(pos);
            _nodes.Add(new Node
            {
                Pos = pos,
                PulsePhase = Random.Shared.NextSingle() * MathF.Tau,
                PulseFreq = 1f + Random.Shared.NextSingle() * 2f,
                BaseRadius = 3f + Random.Shared.NextSingle() * 2f,
            });
        }

        _initialized = true;
    }

    public void Draw(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax)
    {
        dl.PushClipRect(winMin, winMax, true);

        var data = Volatile.Read(ref _front);

        if (data.TraceLines != null)
            foreach (var (a, b, glowCol, mainCol) in data.TraceLines)
            {
                dl.AddLine(a, b, glowCol, 4f);
                dl.AddLine(a, b, mainCol, 1f);
            }

        if (data.PulseDots != null)
            foreach (var (pos, glowSize, glowCol, mainSize, mainCol) in data.PulseDots)
            {
                dl.AddCircleFilled(pos, glowSize, glowCol);
                dl.AddCircleFilled(pos, mainSize, mainCol);
            }

        if (data.Nodes != null)
            for (var i = 0; i < data.Nodes.Length; i++)
            {
                var (pos, radius, glowCol, mainCol) = data.Nodes[i];
                if (glowCol != 0u) dl.AddCircleFilled(pos, radius + 8f, glowCol);
                dl.AddCircleFilled(pos, radius, mainCol);
            }

        dl.PopClipRect();
    }

    private void SwapBuffers()
    {
        var tmp = _front;
        _front = _back;
        _back = tmp;
    }

    private void ComputeFrameData(Vector2 mousePos)
    {
        var traceLines = new (Vector2 A, Vector2 B, uint GlowCol, uint MainCol)[_traces.Length];
        for (var i = 0; i < _traces.Length; i++)
        {
            var trace = _traces[i];
            traceLines[i] = (trace.A, trace.B, EffectUtils.PackColor(0, 1, 1, 0.06f), EffectUtils.PackColor(0, 1, 1, 0.2f));
        }

        var pulseDots = new List<(Vector2 Pos, float GlowSize, uint GlowCol, float MainSize, uint MainCol)>();
        foreach (var pulse in _pulses)
        {
            if (pulse.TraceIdx >= _traces.Length) continue;
            var trace = _traces[pulse.TraceIdx];
            var pos = Vector2.Lerp(trace.A, trace.B, pulse.T);
            var alpha = 1f - MathF.Abs(pulse.T - 0.5f) * 2f;
            alpha = Math.Max(0f, alpha) * 0.9f;
            pulseDots.Add((pos, 8f, EffectUtils.PackColor(pulse.Color.X, pulse.Color.Y, pulse.Color.Z, alpha * 0.15f), 4f, EffectUtils.PackColor(pulse.Color.X, pulse.Color.Y, pulse.Color.Z, alpha)));
        }

        var nodes = new (Vector2 Pos, float Radius, uint GlowCol, uint MainCol)[_nodes.Count];
        for (var i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            var pulse = (MathF.Sin(node.PulsePhase) + 1f) * 0.5f;
            var mouseNear = Vector2.Distance(mousePos, node.Pos) < 20f;
            var r = node.BaseRadius + (mouseNear ? 3f : 0f);
            var alpha = 0.3f + pulse * 0.3f + (mouseNear ? 0.3f : 0f);
            nodes[i] = (node.Pos, r, mouseNear ? EffectUtils.PackColor(0, 1, 1, 0.1f) : 0u, EffectUtils.PackColor(0, 1, 1, alpha));
        }

        var back = _back;
        back.TraceLines = traceLines;
        back.PulseDots = pulseDots.ToArray();
        back.Nodes = nodes;
    }

    private sealed class FrameData
    {
        public (Vector2 A, Vector2 B, uint GlowCol, uint MainCol)[]? TraceLines;
        public (Vector2 Pos, float GlowSize, uint GlowCol, float MainSize, uint MainCol)[]? PulseDots;
        public (Vector2 Pos, float Radius, uint GlowCol, uint MainCol)[]? Nodes;
    }
}
