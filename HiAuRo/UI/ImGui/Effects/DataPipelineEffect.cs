using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

/// <summary>
/// 数据流管道特效 — 管道、数据包、节点、鼠标互动
/// </summary>
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
    private static readonly Vector3[] PacketColors =
    [
        new(0, 1, 1),
        new(1, 0, 1),
        new(1, 1, 0),
    ];

    private readonly Pipe[] _pipes = new Pipe[PipeCount];
    private readonly List<PipeNode> _nodes = new();
    private readonly List<DataPacket> _packets = new();
    private bool _initialized;
    private float _scrollOffset;

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        if (!_initialized)
            InitLayout(min, max);

        _scrollOffset += dt * 8f;

        // 更新数据包
        for (var i = _packets.Count - 1; i >= 0; i--)
        {
            var p = _packets[i];
            p.T += p.Speed * dt;
            if (p.T > 1f)
                _packets.RemoveAt(i);
            else
                _packets[i] = p;
        }

        // 节点定期发射新数据包
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

        // 保持最小数据包数
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

        // 鼠标悬停管道加速
        var mouse = ImGui.GetIO().MousePos;
        for (var i = 0; i < _packets.Count; i++)
        {
            if (_packets[i].PipeIdx >= _pipes.Length) continue;
            var pipe = _pipes[_packets[i].PipeIdx];
            var pos = Vector2.Lerp(pipe.A, pipe.B, _packets[i].T);
            if (Vector2.Distance(mouse, pos) < 15f)
            {
                var p = _packets[i];
                p.Speed *= 1.8f;
                _packets[i] = p;
            }
        }

        // 点击节点爆发数据包
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            for (var i = 0; i < _nodes.Count; i++)
            {
                if (Vector2.Distance(mouse, _nodes[i].Pos) < 15f)
                {
                    for (var j = 0; j < 6 && _packets.Count < 60; j++)
                    {
                        var pipeIdx = Random.Shared.Next(_pipes.Length);
                        _packets.Add(new DataPacket
                        {
                            PipeIdx = pipeIdx,
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

        var mouse = ImGui.GetIO().MousePos;

        // 管道
        for (var i = 0; i < _pipes.Length; i++)
        {
            var pipe = _pipes[i];
            var near = IsMouseNearPipe(mouse, pipe);
            var wallAlpha = near ? 0.15f : 0.07f;
            var lineAlpha = near ? 0.4f : 0.15f;
            var thick = near ? 8f : 6f;

            // 管壁
            var wallColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.1f, 0.2f, 0.3f, wallAlpha));
            if (pipe.Horizontal)
                dl.AddRectFilled(pipe.A - new Vector2(0, thick * 0.5f), pipe.B + new Vector2(0, thick * 0.5f), wallColor);
            else
                dl.AddRectFilled(pipe.A - new Vector2(thick * 0.5f, 0), pipe.B + new Vector2(thick * 0.5f, 0), wallColor);

            // 中心流线
            dl.AddLine(pipe.A, pipe.B,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, lineAlpha)), 1f);
        }

        // 数据包
        foreach (var packet in _packets)
        {
            if (packet.PipeIdx >= _pipes.Length) continue;
            var pipe = _pipes[packet.PipeIdx];
            var t = Math.Clamp(packet.T, 0f, 1f);
            var pos = Vector2.Lerp(pipe.A, pipe.B, t);
            var fade = 1f - MathF.Abs(t - 0.5f) * 2f;
            var alpha = Math.Max(0f, fade) * 0.9f;

            dl.AddRectFilled(pos - new Vector2(packet.Size, packet.Size * 0.6f),
                pos + new Vector2(packet.Size, packet.Size * 0.6f),
                ImGui.ColorConvertFloat4ToU32(new Vector4(packet.Color.X, packet.Color.Y, packet.Color.Z, alpha)));
        }

        // 节点
        for (var i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            var pulse = EffectUtils.StatePulse(node.PulseFreq);
            var alpha = 0.2f + pulse * 0.3f;
            var r = 4f + pulse * 2f;

            dl.AddCircleFilled(node.Pos, r + 5f,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, alpha * 0.1f)));
            dl.AddCircleFilled(node.Pos, r,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, alpha)));
        }

        dl.PopClipRect();
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
}
