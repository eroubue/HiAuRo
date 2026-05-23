using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

/// <summary>
/// 霓虹电路板特效 — 走线、节点、数据脉冲、鼠标互动
/// </summary>
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
    private const int PulsePerTrace = 2;
    private const int MaxPulses = 40;

    private static readonly Vector3 NeonCyan = new(0, 1, 1);
    private static readonly Vector3 NeonMagenta = new(1, 0, 1);

    private readonly Trace[] _traces = new Trace[TraceCount];
    private readonly List<Node> _nodes = new();
    private readonly List<Pulse> _pulses = new();
    private bool _initialized;

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        if (!_initialized)
            InitLayout(min, max);

        // 更新脉冲
        for (var i = _pulses.Count - 1; i >= 0; i--)
        {
            var p = _pulses[i];
            p.T += p.Speed * dt;
            if (p.T > 1f)
                _pulses.RemoveAt(i);
            else
                _pulses[i] = p;
        }

        // 补充脉冲
        while (_pulses.Count < MaxPulses)
        {
            var idx = Random.Shared.Next(_traces.Length);
            var color = Random.Shared.NextSingle() < 0.6f ? NeonCyan : NeonMagenta;
            _pulses.Add(new Pulse
            {
                TraceIdx = idx,
                T = 0f,
                Speed = 0.3f + Random.Shared.NextSingle() * 0.8f,
                Color = color,
            });
        }

        // 鼠标互动 — 点击节点发射脉冲波
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            var mp = ImGui.GetMousePos();
            foreach (var node in _nodes)
            {
                if (Vector2.Distance(mp, node.Pos) < 15f)
                {
                    for (var j = 0; j < 5 && _pulses.Count < MaxPulses + 10; j++)
                    {
                        var idx = Random.Shared.Next(_traces.Length);
                        _pulses.Add(new Pulse
                        {
                            TraceIdx = idx,
                            T = 0f,
                            Speed = 0.8f + Random.Shared.NextSingle() * 1f,
                            Color = new Vector3(1, 1, 0),
                        });
                    }
                    break;
                }
            }
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

        var mouse = ImGui.GetIO().MousePos;

        // 走线 — 双层绘制
        foreach (var trace in _traces)
        {
            // 底层辉光
            dl.AddLine(trace.A, trace.B,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, 0.06f)), 4f);
            // 顶层
            dl.AddLine(trace.A, trace.B,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, 0.2f)), 1f);
        }

        // 脉冲点
        foreach (var pulse in _pulses)
        {
            if (pulse.TraceIdx >= _traces.Length) continue;
            var trace = _traces[pulse.TraceIdx];
            var pos = Vector2.Lerp(trace.A, trace.B, pulse.T);
            var alpha = 1f - MathF.Abs(pulse.T - 0.5f) * 2f;
            alpha = Math.Max(0f, alpha) * 0.9f;
            var size = 4f;

            dl.AddCircleFilled(pos, size + 4f,
                ImGui.ColorConvertFloat4ToU32(new Vector4(pulse.Color.X, pulse.Color.Y, pulse.Color.Z, alpha * 0.15f)));
            dl.AddCircleFilled(pos, size,
                ImGui.ColorConvertFloat4ToU32(new Vector4(pulse.Color.X, pulse.Color.Y, pulse.Color.Z, alpha)));
        }

        // 节点 — 呼吸闪烁 + 鼠标靠近放大
        var dt = ImGui.GetIO().DeltaTime;
        for (var i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            node.PulsePhase += node.PulseFreq * dt;
            var pulse = EffectUtils.StatePulse(node.PulseFreq);
            var mouseNear = Vector2.Distance(mouse, node.Pos) < 20f;
            var r = node.BaseRadius + (mouseNear ? 3f : 0f);
            var alpha = 0.3f + pulse * 0.3f + (mouseNear ? 0.3f : 0f);

            if (mouseNear)
                dl.AddCircleFilled(node.Pos, r + 8f,
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, 0.1f)));

            dl.AddCircleFilled(node.Pos, r,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, alpha)));
        }

        dl.PopClipRect();
    }
}
