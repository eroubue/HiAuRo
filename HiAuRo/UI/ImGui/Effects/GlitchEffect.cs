using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

/// <summary>
/// 故障艺术特效 — 水平撕裂、色彩通道分离、噪点带、边缘锯齿波、鼠标互动
/// </summary>
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

        // 鼠标速度
        var mouse = ImGui.GetIO().MousePos;
        var dv = mouse - _lastMouse;
        _mouseSpeed = Vector2.Dot(dv, dv);
        _lastMouse = mouse;

        // 大故障定时
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
            if (_burstDuration <= 0f)
                _inBurst = false;
        }

        // 生成撕裂条
        var isBurst = _inBurst || _mouseSpeed > 2500f;
        var count = isBurst ? BurstTearCount : NormalTearCount;
        for (var i = _tears.Count; i < count; i++)
            SpawnTear(min, max);

        // 补充撕裂
        while (_tears.Count < count)
            SpawnTear(min, max);

        // 更新撕裂条
        for (var i = _tears.Count - 1; i >= 0; i--)
        {
            var t = _tears[i];
            t.Life -= dt;
            if (t.Life <= 0f)
            {
                _tears.RemoveAt(i);
                continue;
            }
            t.Alpha = t.Life / t.MaxLife;
            _tears[i] = t;
        }

        // EMP 环更新
        for (var i = _empRings.Count - 1; i >= 0; i--)
        {
            var r = _empRings[i];
            r.Radius += dt * 300f;
            r.Life -= dt * 2.5f;
            if (r.Life <= 0f || r.Radius > r.MaxRadius)
                _empRings.RemoveAt(i);
            else
                _empRings[i] = r;
        }

        // 点击触发 EMP
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            var mp = ImGui.GetMousePos();
            if (mp.X >= min.X && mp.X <= max.X && mp.Y >= min.Y && mp.Y <= max.Y)
            {
                _empRings.Add(new EmpRing
                {
                    Center = mp,
                    Radius = 0f,
                    MaxRadius = Math.Max(max.X - min.X, h) * 0.5f,
                    Life = 1f,
                });
            }
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

        var w = winMax.X - winMin.X;
        var h = winMax.Y - winMin.Y;

        // 边缘锯齿波形线
        DrawEdgeWave(dl, winMin, winMax);

        // 撕裂条 + 通道分离
        foreach (var tear in _tears)
        {
            var alpha = tear.Alpha;
            // 三色通道分离
            DrawChannel(dl, winMin, w, tear, Cyan, -2f, alpha);
            DrawChannel(dl, winMin, w, tear, Magenta, 0f, alpha);
            DrawChannel(dl, winMin, w, tear, Yellow, 2f, alpha);
        }

        // 噪点带
        DrawNoise(dl, winMin, winMax);

        // EMP 圆环
        foreach (var ring in _empRings)
        {
            var a = ring.Life * 0.6f;
            dl.AddCircle(ring.Center, ring.Radius,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, a)),
                0, 2f);
            dl.AddCircle(ring.Center, ring.Radius * 0.7f,
                ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 1, a * 0.5f)),
                0, 1f);
        }

        dl.PopClipRect();
    }

    private static void DrawChannel(ImDrawListPtr dl, Vector2 winMin, float w, TearLine tear, Vector3 color, float extraOffset, float alpha)
    {
        var x = winMin.X + tear.OffsetX + extraOffset;
        var y = tear.Y;
        var h = tear.Height;
        var col = ImGui.ColorConvertFloat4ToU32(new Vector4(color.X, color.Y, color.Z, alpha * 0.4f));
        dl.AddRectFilled(new Vector2(x, y), new Vector2(x + w, y + h), col);
    }

    private void DrawNoise(ImDrawListPtr dl, Vector2 min, Vector2 max)
    {
        var count = 20 + (_inBurst ? 40 : 0);
        for (var i = 0; i < count; i++)
        {
            var x = min.X + Random.Shared.NextSingle() * (max.X - min.X);
            var y = min.Y + Random.Shared.NextSingle() * (max.Y - min.Y);
            var len = 3f + Random.Shared.NextSingle() * 15f;
            var colIdx = Random.Shared.Next(3);
            var color = colIdx == 0 ? Cyan : colIdx == 1 ? Magenta : Yellow;
            var a = 0.1f + Random.Shared.NextSingle() * 0.3f;
            dl.AddLine(new Vector2(x, y), new Vector2(x + len, y),
                ImGui.ColorConvertFloat4ToU32(new Vector4(color.X, color.Y, color.Z, a)),
                1f);
        }
    }

    private void DrawEdgeWave(ImDrawListPtr dl, Vector2 min, Vector2 max)
    {
        var color = ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, 0.15f));
        var segLen = 20f;
        var amp = 3f;

        // 上下边
        for (var x = min.X; x < max.X - segLen; x += segLen)
        {
            var off = MathF.Sin(_time * 5f + x * 0.05f) * amp;
            dl.PathLineTo(new Vector2(x, min.Y + off));
        }
        dl.PathStroke(color, ImDrawFlags.None, 1f);

        for (var x = min.X; x < max.X - segLen; x += segLen)
        {
            var off = MathF.Sin(_time * 4.5f + x * 0.06f) * amp;
            dl.PathLineTo(new Vector2(x, max.Y + off));
        }
        dl.PathStroke(color, ImDrawFlags.None, 1f);
    }
}
