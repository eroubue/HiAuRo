using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

/// <summary>
/// 雨特效 — 斜线雨滴 + 细雨丝，均匀分布在整个窗口
/// </summary>
public sealed class RainEffect
{
    private struct Drop
    {
        public float X, Y;
        public float Length;
        public float Speed;
        public float Alpha;
    }

    private readonly Drop[] _drops;
    private readonly Drop[] _mists;
    private bool _initialized;

    public RainEffect(int dropCount = 70)
    {
        _drops = new Drop[dropCount];
        _mists = new Drop[(int)(dropCount * 0.2f)];
    }

    private void InitAll(float minX, float minY, float maxX, float maxY)
    {
        var w = maxX - minX;
        var h = maxY - minY;
        for (var i = 0; i < _drops.Length; i++)
            InitDrop(ref _drops[i], minX, minY, w, h, true);
        for (var i = 0; i < _mists.Length; i++)
            InitDrop(ref _mists[i], minX, minY, w, h, true);
        _initialized = true;
    }

    private static void InitDrop(ref Drop d, float minX, float minY, float w, float h, bool scatterY)
    {
        d.X = minX + Random.Shared.NextSingle() * w;
        d.Y = scatterY ? minY + Random.Shared.NextSingle() * h : minY;
        d.Length = 8f + Random.Shared.NextSingle() * 8f;
        d.Speed = 150f + Random.Shared.NextSingle() * 150f;
        d.Alpha = 0.3f + Random.Shared.NextSingle() * 0.3f;
    }

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        if (!_initialized)
            InitAll(min.X, min.Y, max.X, max.Y);

        var w = max.X - min.X;
        var h = max.Y - min.Y;

        for (var i = 0; i < _drops.Length; i++)
        {
            ref var d = ref _drops[i];
            d.Y += d.Speed * dt;
            if (d.Y - d.Length > max.Y)
                InitDrop(ref d, min.X, min.Y, w, h, false);
        }

        for (var i = 0; i < _mists.Length; i++)
        {
            ref var m = ref _mists[i];
            m.Y += m.Speed * 0.7f * dt;
            if (m.Y - m.Length > max.Y)
            {
                InitDrop(ref m, min.X, min.Y, w, h, false);
                m.Length = 4f + Random.Shared.NextSingle() * 4f;
                m.Alpha = 0.15f + Random.Shared.NextSingle() * 0.15f;
            }
        }
    }

    public void Draw(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax)
    {
        dl.PushClipRect(winMin, winMax, true);

        var isLight = Theme.Mode == Theme.ThemeMode.Light;
        var baseColor = isLight
            ? new Vector3(0.2f, 0.3f, 0.5f)
            : new Vector3(0.6f, 0.75f, 0.95f);

        // 雨滴斜 15°
        var slope = MathF.Tan(15f * MathF.PI / 180f);

        for (var i = 0; i < _drops.Length; i++)
        {
            ref var d = ref _drops[i];
            var dx = d.Length * slope;
            var p1 = new Vector2(d.X, d.Y - d.Length);
            var p2 = new Vector2(d.X + dx, d.Y);
            var c = ImGui.ColorConvertFloat4ToU32(new Vector4(baseColor.X, baseColor.Y, baseColor.Z, d.Alpha));
            dl.AddLine(p1, p2, c, 1.5f);
        }

        // 细雨丝
        for (var i = 0; i < _mists.Length; i++)
        {
            ref var m = ref _mists[i];
            var dx = m.Length * slope;
            var p1 = new Vector2(m.X, m.Y - m.Length);
            var p2 = new Vector2(m.X + dx, m.Y);
            var c = ImGui.ColorConvertFloat4ToU32(new Vector4(baseColor.X, baseColor.Y, baseColor.Z, m.Alpha));
            dl.AddLine(p1, p2, c, 0.5f);
        }

        dl.PopClipRect();
    }
}
