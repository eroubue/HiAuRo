using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

/// <summary>
/// 全息投影特效 — 旋转线框几何体、色差、扫描线、闪烁、鼠标互动
/// </summary>
public sealed class HologramEffect
{
    private const int VertexCount = 6;
    private static readonly (int A, int B)[] EdgePairs =
    [
        (0, 1), (1, 2), (2, 3), (3, 4), (4, 5), (5, 0),
        (0, 2), (1, 3), (2, 4), (3, 5), (4, 0), (5, 1),
    ];

    private float _rotX;
    private float _rotY;
    private float _time;
    private float _flickerTimer;
    private bool _inFlicker;
    private float _flickerAlpha = 1f;

    private static readonly Vector3[] LocalVerts =
    [
        new(0, 1, 0),
        new(0.866f, 0.5f, 0),
        new(0.866f, -0.5f, 0),
        new(0, -1, 0),
        new(-0.866f, -0.5f, 0),
        new(-0.866f, 0.5f, 0),
    ];

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        _time += dt;

        // 基础自转
        _rotY += dt * 0.8f;
        _rotX += dt * 0.3f;

        // 鼠标影响旋转
        var mouse = ImGui.GetIO().MousePos;
        var center = (min + max) * 0.5f;
        var inWindow = mouse.X >= min.X && mouse.X <= max.X && mouse.Y >= min.Y && mouse.Y <= max.Y;
        if (inWindow)
        {
            var dx = (mouse.X - center.X) / Math.Max(1f, (max.X - min.X) * 0.5f);
            var dy = (mouse.Y - center.Y) / Math.Max(1f, (max.Y - min.Y) * 0.5f);
            _rotY += dx * dt * 1.5f;
            _rotX += dy * dt * 1.5f;
        }

        // 周期性闪烁故障
        _flickerTimer -= dt;
        if (_flickerTimer <= 0f)
        {
            _flickerTimer = 2f + Random.Shared.NextSingle() * 1f;
            _inFlicker = true;
        }

        if (_inFlicker)
        {
            _flickerAlpha = Random.Shared.NextSingle() < 0.3f ? 0.05f : 0.15f + Random.Shared.NextSingle() * 0.15f;
            _flickerTimer -= dt;
            if (_flickerTimer <= 0f || Random.Shared.NextSingle() < 0.1f)
            {
                _inFlicker = false;
                _flickerAlpha = 0.2f;
                _flickerTimer = 2f + Random.Shared.NextSingle() * 1f;
            }
        }
        else
        {
            _flickerAlpha = 0.2f + MathF.Sin(_time * 0.5f) * 0.05f;
        }
    }

    public void Draw(ImDrawListPtr dl, Vector2 winMin, Vector2 winMax)
    {
        dl.PushClipRect(winMin, winMax, true);

        var center = (winMin + winMax) * 0.5f;
        var scale = Math.Min(winMax.X - winMin.X, winMax.Y - winMin.Y) * 0.18f;

        // 底层淡蓝色渐变矩形
        DrawBaseGradient(dl, winMin, winMax);

        // 投影 3D→2D
        var projected = new Vector2[VertexCount];
        for (var i = 0; i < VertexCount; i++)
        {
            var v = LocalVerts[i] * scale;
            projected[i] = Project(v, center, _rotX, _rotY);
        }

        var baseAlpha = _flickerAlpha;

        // 色差 — 青色偏移
        var cyanOffset = new Vector2(-2f, 0);
        var magentaOffset = new Vector2(2f, 0);

        DrawWireframe(dl, projected, cyanOffset, new Vector3(0, 1, 1), baseAlpha * 0.5f);
        DrawWireframe(dl, projected, magentaOffset, new Vector3(1, 0, 1), baseAlpha * 0.5f);
        DrawWireframe(dl, projected, Vector2.Zero, new Vector3(0.7f, 0.8f, 1f), baseAlpha);

        // 顶点
        foreach (var p in projected)
        {
            dl.AddCircleFilled(p + cyanOffset, 2f,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, baseAlpha * 0.5f)));
            dl.AddCircleFilled(p + magentaOffset, 2f,
                ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 1, baseAlpha * 0.5f)));
            dl.AddCircleFilled(p, 3f,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.7f, 0.8f, 1f, baseAlpha)));
        }

        // 扫描线干扰
        DrawScanNoise(dl, winMin, winMax);

        dl.PopClipRect();
    }

    private static Vector2 Project(Vector3 v, Vector2 center, float rotX, float rotY)
    {
        // 绕 Y 轴
        var cosY = MathF.Cos(rotY);
        var sinY = MathF.Sin(rotY);
        var x1 = v.X * cosY - v.Z * sinY;
        var z1 = v.X * sinY + v.Z * cosY;

        // 绕 X 轴
        var cosX = MathF.Cos(rotX);
        var sinX = MathF.Sin(rotX);
        var y1 = v.Y * cosX - z1 * sinX;
        var z2 = v.Y * sinX + z1 * cosX;

        // 简单透视
        var perspective = 400f;
        var scale = perspective / (perspective + z2);
        return center + new Vector2(x1, y1) * scale;
    }

    private static void DrawWireframe(ImDrawListPtr dl, Vector2[] projected, Vector2 offset, Vector3 color, float alpha)
    {
        var col = ImGui.ColorConvertFloat4ToU32(new Vector4(color.X, color.Y, color.Z, alpha));
        for (var i = 0; i < EdgePairs.Length; i++)
        {
            var a = EdgePairs[i].A;
            var b = EdgePairs[i].B;
            if (a < projected.Length && b < projected.Length)
                dl.AddLine(projected[a] + offset, projected[b] + offset, col, 1f);
        }
    }

    private void DrawScanNoise(ImDrawListPtr dl, Vector2 min, Vector2 max)
    {
        var w = max.X - min.X;
        var count = 8 + (Random.Shared.NextSingle() < 0.3f ? 12 : 0);
        for (var i = 0; i < count; i++)
        {
            var x = min.X + Random.Shared.NextSingle() * w;
            var y = min.Y + Random.Shared.NextSingle() * (max.Y - min.Y);
            var len = 5f + Random.Shared.NextSingle() * 20f;
            var a = 0.05f + Random.Shared.NextSingle() * 0.15f;
            dl.AddLine(new Vector2(x, y), new Vector2(x + len, y),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.7f, 0.8f, 1f, a)), 1f);
        }
    }

    private static void DrawBaseGradient(ImDrawListPtr dl, Vector2 min, Vector2 max)
    {
        var h = max.Y - min.Y;
        var steps = 6;
        var stepH = h / steps;
        for (var i = 0; i < steps; i++)
        {
            var t = i / (float)steps;
            var alpha = 0.02f + t * 0.03f;
            var y1 = min.Y + i * stepH;
            var y2 = y1 + stepH;
            dl.AddRectFilled(new Vector2(min.X, y1), new Vector2(max.X, y2),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.1f, 0.2f, 0.4f, alpha)));
        }
    }
}
