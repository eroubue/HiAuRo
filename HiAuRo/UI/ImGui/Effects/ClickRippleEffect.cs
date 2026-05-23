using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

/// <summary>
/// 点击涟漪特效 — 点击时从点击点扩散圆环，短生命周期
/// </summary>
public sealed class ClickRippleEffect
{
    private struct Ripple
    {
        public Vector2 Center;
        public float Progress;
        public float MaxRadius;
    }

    private readonly Ripple[] _ripples;
    private const float Duration = 0.5f;

    public ClickRippleEffect(int maxRipples = 8)
    {
        _ripples = new Ripple[maxRipples];
    }

    /// <summary>触发一次点击涟漪</summary>
    public void Trigger(Vector2 pos, float maxRadius = 40f)
    {
        // 寻找空闲槽位或最旧的
        var idx = -1;
        var oldestProg = float.MaxValue;

        for (var i = 0; i < _ripples.Length; i++)
        {
            if (_ripples[i].Progress >= 1f)
            {
                idx = i;
                break;
            }
            if (_ripples[i].Progress < oldestProg)
            {
                oldestProg = _ripples[i].Progress;
                idx = i;
            }
        }

        if (idx < 0) return;

        _ripples[idx] = new Ripple
        {
            Center = pos,
            Progress = 0f,
            MaxRadius = maxRadius,
        };
    }

    /// <summary>更新所有涟漪进度</summary>
    public void Update(float dt)
    {
        for (var i = 0; i < _ripples.Length; i++)
        {
            if (_ripples[i].Progress < 1f)
                _ripples[i].Progress += dt / Duration;
        }
    }

    /// <summary>绘制所有活跃涟漪</summary>
    public void Draw(ImDrawListPtr dl)
    {
        var accent = Theme.Colors.AccentBlue;

        for (var i = 0; i < _ripples.Length; i++)
        {
            ref var r = ref _ripples[i];
            if (r.Progress >= 1f) continue;

            var easedProgress = Easing.EaseOutCubic(r.Progress);
            var radius = r.MaxRadius * easedProgress;
            var alpha = (1f - r.Progress) * 0.4f;
            var color = new Vector4(accent.X, accent.Y, accent.Z, alpha);

            var numSegments = Math.Clamp((int)(radius * 0.3f), 12, 32);
            dl.PathArcTo(r.Center, radius, 0f, MathF.PI * 2f, numSegments);
            dl.PathStroke(ImGui.ColorConvertFloat4ToU32(color), 0, 2f * (1f - r.Progress));
        }
    }
}
