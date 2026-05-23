using System.Numerics;
using System.Threading.Tasks;

namespace HiAuRo.ImGuiLib.Effects;

public sealed class DataPipelineEffect
{
    private struct Pipe
    {
        public Vector2 A;
        public Vector2 B;
        public bool Horizontal;
    }

    private struct PipeNode
    {
        public Vector2 Pos;
        public float PulsePhase;
        public float PulseFreq;
        public float EmitTimer;
    }

    private struct DataPacket
    {
        public int PipeIdx;
        public float T;
        public float Speed;
        public Vector3 Color;
        public float Size;
    }

    private const int PipeCount = 20;
    private const int InitialPackets = 20;
    private static readonly Vector3[] PacketColors = [new(0, 1, 1), new(1, 0, 1), new(1, 1, 0)];

    private readonly Pipe[] _pipes = new Pipe[PipeCount];
    private readonly List<PipeNode> _nodes = new();
    private readonly List<DataPacket> _packets = new();
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

        for (var i = _packets.Count - 1; i >= 0; i--)
        {
            var p = _packets[i];
            p.T += p.Speed * dt;
            if (p.T > 1f) _packets.RemoveAt(i);
            else _packets[i] = p;
        }

        for (var i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            node.PulsePhase += node.PulseFreq * dt;
            node.EmitTimer -= dt;
            if (node.EmitTimer <= 0f && _packets.Count < 50)
            {
                node.EmitTimer = 1f + Random.Shared.NextSingle() * 3f;
                var pipeIdx = Random.Shared.Next(_pipes.Length);
                var dir = Random.Shared.Next(2) == 0;
                _packets.Add(new DataPacket
                {
                    PipeIdx = pipeIdx,
                    T = dir ? 0f : 1f,
                    Speed = (dir ? 1f : -1f) * (0.2f + Random.Shared.NextSingle() * 0.6f),
                    Color = PacketColors[Random.Shared.Next(PacketColors.Length)],
                    Size = 3f + Random.Shared.NextSingle() * 3f,
                });
                _nodes[i] = node;
            }
        }

        while (_packets.Count < InitialPackets)
        {
            var pipeIdx = Random.Shared.Next(_pipes.Length);
            var dir = Random.Shared.Next(2) == 0;
            _packets.Add(new DataPacket
            {
                PipeIdx = pipeIdx,
                T = dir ? 0f : 1f,
                Speed = (dir ? 1f : -1f) * (0.2f + Random.Shared.NextSingle() * 0.6f),
                Color = PacketColors[Random.Shared.Next(PacketColors.Length)],
                Size = 3f + Random.Shared.NextSingle() * 3f,
            });
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            for (var i = 0; i < _nodes.Count; i++)
            {
                if (Vector2.Distance(_mousePos, _nodes[i].Pos) < 15f)
                {
                    for (var j = 0; j < 6 && _packets.Count < 60; j++)
                    {
                        _packets.Add(new DataPacket
                        {
                            PipeIdx = Random.Shared.Next(_pipes.Length),
                            T = 0.5f,
                            Speed = (Random.Shared.Next(2) == 0 ? 1f : -1f) * (0.8f + Random.Shared.NextSingle()),
                            Color = PacketColors[Random.Shared.Next(PacketColors.Length)],
                            Size = 4f,
                        });
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
        var margin = 15f;
        var nodePositions = new List<Vector2>();

        for (var i = 0; i < _pipes.Length; i++)
        {
            var horizontal = Random.Shared.Next(2) == 0;
            if (horizontal)
            {
                var y = min.Y + margin + Random.Shared.NextSingle() * (h - margin * 2);
                var x1 = min.X + margin + Random.Shared.NextSingle() * (w * 0.3f);
                var x2 = x1 + w * 0.3f + Random.Shared.NextSingle() * (w * 0.4f);
                _pipes[i] = new Pipe { A = new Vector2(x1, y), B = new Vector2(x2, y), Horizontal = true };
            }
            else
            {
                var x = min.X + margin + Random.Shared.NextSingle() * (w - margin * 2);
                var y1 = min.Y + margin + Random.Shared.NextSingle() * (h * 0.3f);
                var y2 = y1 + h * 0.3f + Random.Shared.NextSingle() * (h * 0.4f);
                _pipes[i] = new Pipe { A = new Vector2(x, y1), B = new Vector2(x, y2), Horizontal = false };
            }

            TryAddNode(_pipes[i].A);
            TryAddNode(_pipes[i].B);
        }

        void TryAddNode(Vector2 pos)
        {
            foreach (var existing in nodePositions)
                if (Vector2.Distance(existing, pos) < 12f) return;
            nodePositions.Add(pos);
            _nodes.Add(new PipeNode
            {
                Pos = pos,
                PulsePhase = Random.Shared.NextSingle() * MathF.Tau,
                PulseFreq = 1f + Random.Shared.NextSingle() * 2f,
                EmitTimer = Random.Shared.NextSingle() * 2f,
            });
        }

        _initialized = true;
    }

    public void Draw(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax)
    {
        dl.PushClipRect(winMin, winMax, true);

        var data = Volatile.Read(ref _front);

        if (data.PipeWalls != null)
            foreach (var (min, max, col) in data.PipeWalls)
                dl.AddRectFilled(min, max, col);

        if (data.PipeLines != null)
            foreach (var (a, b, col) in data.PipeLines)
                dl.AddLine(a, b, col, 1f);

        if (data.PacketRects != null)
            foreach (var (min, max, col) in data.PacketRects)
                dl.AddRectFilled(min, max, col);

        if (data.Nodes != null)
            foreach (var (pos, glowRadius, glowCol, radius, mainCol) in data.Nodes)
            {
                dl.AddCircleFilled(pos, glowRadius, glowCol);
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
        var pipeWalls = new List<(Vector2 Min, Vector2 Max, uint Col)>();
        var pipeLines = new List<(Vector2 A, Vector2 B, uint Col)>();

        for (var i = 0; i < _pipes.Length; i++)
        {
            var pipe = _pipes[i];
            var near = IsMouseNearPipe(mousePos, pipe);
            var wallAlpha = near ? 0.15f : 0.07f;
            var thick = near ? 8f : 6f;
            var lineAlpha = near ? 0.4f : 0.15f;

            var wallColor = EffectUtils.PackColor(0.1f, 0.2f, 0.3f, wallAlpha);
            if (pipe.Horizontal)
                pipeWalls.Add((pipe.A - new Vector2(0, thick * 0.5f), pipe.B + new Vector2(0, thick * 0.5f), wallColor));
            else
                pipeWalls.Add((pipe.A - new Vector2(thick * 0.5f, 0), pipe.B + new Vector2(thick * 0.5f, 0), wallColor));

            pipeLines.Add((pipe.A, pipe.B, EffectUtils.PackColor(0, 1, 1, lineAlpha)));
        }

        var packetRects = new List<(Vector2 Min, Vector2 Max, uint Col)>();
        foreach (var packet in _packets)
        {
            if (packet.PipeIdx >= _pipes.Length) continue;
            var pipe = _pipes[packet.PipeIdx];
            var t = Math.Clamp(packet.T, 0f, 1f);
            var pos = Vector2.Lerp(pipe.A, pipe.B, t);
            var fade = 1f - MathF.Abs(t - 0.5f) * 2f;
            var alpha = Math.Max(0f, fade) * 0.9f;
            packetRects.Add((pos - new Vector2(packet.Size, packet.Size * 0.6f), pos + new Vector2(packet.Size, packet.Size * 0.6f), EffectUtils.PackColor(packet.Color.X, packet.Color.Y, packet.Color.Z, alpha)));
        }

        var nodes = new (Vector2 Pos, float GlowRadius, uint GlowCol, float Radius, uint MainCol)[_nodes.Count];
        for (var i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            var pulse = (MathF.Sin(node.PulsePhase) + 1f) * 0.5f;
            var alpha = 0.2f + pulse * 0.3f;
            var r = 4f + pulse * 2f;
            nodes[i] = (node.Pos, r + 5f, EffectUtils.PackColor(0, 1, 1, alpha * 0.1f), r, EffectUtils.PackColor(0, 1, 1, alpha));
        }

        var back = _back;
        back.PipeWalls = pipeWalls.ToArray();
        back.PipeLines = pipeLines.ToArray();
        back.PacketRects = packetRects.ToArray();
        back.Nodes = nodes;
    }

    private static bool IsMouseNearPipe(Vector2 mouse, Pipe pipe)
    {
        var ab = pipe.B - pipe.A;
        var am = mouse - pipe.A;
        var t = Vector2.Dot(am, ab) / Math.Max(1f, Vector2.Dot(ab, ab));
        t = Math.Clamp(t, 0f, 1f);
        var closest = pipe.A + ab * t;
        return Vector2.Distance(mouse, closest) < 15f;
    }

    private sealed class FrameData
    {
        public (Vector2 Min, Vector2 Max, uint Col)[]? PipeWalls;
        public (Vector2 A, Vector2 B, uint Col)[]? PipeLines;
        public (Vector2 Min, Vector2 Max, uint Col)[]? PacketRects;
        public (Vector2 Pos, float GlowRadius, uint GlowCol, float Radius, uint MainCol)[]? Nodes;
    }
}
