using System.Numerics;

namespace HiAuRo.ImGuiLib.Effects;

public sealed class LeyLinesEffect
{
    private static readonly Vector2[] FCenters =
    [
        new(4f, 0f),
        new(2f, 3.464102f),
        new(-2f, 3.464102f),
        new(-4f, 0f),
        new(-2f, -3.464102f),
        new(2f, -3.464102f),
    ];

    private static readonly Vector2[] TriBase1 =
    [
        new(4.005988f, 2.970220f),
        new(-0.569292f, 4.954398f),
        new(-4.575280f, 1.984178f),
        new(-4.005988f, -2.970220f),
        new(0.569292f, -4.954398f),
        new(4.575280f, -1.984178f),
    ];

    private static readonly Vector2[] TriBase2 =
    [
        new(4.575280f, 1.984178f),
        new(0.569292f, 4.954398f),
        new(-4.005988f, 2.970220f),
        new(-4.575280f, -1.984178f),
        new(-0.569292f, -4.954398f),
        new(4.005988f, -2.970220f),
    ];

    private static readonly Vector2[] TriTips =
    [
        new(5.152851f, 2.975000f),
        new(0.000000f, 5.950000f),
        new(-5.152851f, 2.975000f),
        new(-5.152851f, -2.975000f),
        new(0.000000f, -5.950000f),
        new(5.152851f, -2.975000f),
    ];

    private static readonly Vector2[] TriCentroids =
    [
        new(4.577851f, 2.643024f),
        new(0.000000f, 5.286047f),
        new(-4.577851f, 2.643024f),
        new(-4.577851f, -2.643024f),
        new(0.000000f, -5.286047f),
        new(4.577851f, -2.643024f),
    ];

    private static readonly Vector2[] DiamondC =
    [
        new(4f, 0f), new(0f, 4f), new(-4f, 0f), new(0f, -4f),
    ];

    private static readonly Vector2[] SquareD =
    [
        new(2f, 2f), new(-2f, 2f), new(-2f, -2f), new(2f, -2f),
    ];

    private static readonly (int A, int B)[] DiamondEdges =
    [
        (0, 1), (1, 2), (2, 3), (3, 0),
    ];

    private static readonly (int A, int B)[] SquareEdges =
    [
        (0, 1), (1, 2), (2, 3), (3, 0),
    ];

    // 6 组三角形，每组 3 条边
    private static readonly (int Tri, int A, int B)[] TriEdges =
    [
        (0, 0, 1), (0, 1, 2), (0, 2, 0),
        (1, 0, 1), (1, 1, 2), (1, 2, 0),
        (2, 0, 1), (2, 1, 2), (2, 2, 0),
        (3, 0, 1), (3, 1, 2), (3, 2, 0),
        (4, 0, 1), (4, 1, 2), (4, 2, 0),
        (5, 0, 1), (5, 1, 2), (5, 2, 0),
    ];

    private float _rotX;
    private float _rotY;
    private float _flatRot;
    private float _time;
    private float _flickerAlpha = 0.2f;
    private float _flickerTimer = 2f;
    private bool _inFlicker;

    public void Update(float dt, Vector2 min, Vector2 max)
    {
        _time += dt;
        _rotY += 0.3f * dt;
        _flatRot += 0.1f * dt;

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

        _flickerTimer -= dt;
        if (_flickerTimer <= 0f)
        {
            _flickerTimer = 2f + Random.Shared.NextSingle() * 1f;
            _inFlicker = true;
        }

        if (_inFlicker)
        {
            _flickerAlpha = Random.Shared.NextSingle() < 0.3f
                ? 0.05f
                : 0.15f + Random.Shared.NextSingle() * 0.15f;
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
        var scale = MathF.Min(winMax.X - winMin.X, winMax.Y - winMin.Y) * 0.06f;
        var baseAlpha = _flickerAlpha;

        DrawBaseGradient(dl, winMin, winMax);

        // 预投影所有几何到屏幕坐标
        var projDiamond = ProjectVerts(DiamondC, center, scale);
        var projSquare = ProjectVerts(SquareD, center, scale);
        var projFCenters = ProjectVerts(FCenters, center, scale);

        // 6 组三角形投影
        var projTriVerts = new Vector2[6][];
        for (var i = 0; i < 6; i++)
        {
            projTriVerts[i] =
            [
                Project(new Vector3(TriBase1[i].X * scale, TriBase1[i].Y * scale, 0), center, _rotX, _rotY, _flatRot),
                Project(new Vector3(TriBase2[i].X * scale, TriBase2[i].Y * scale, 0), center, _rotX, _rotY, _flatRot),
                Project(new Vector3(TriTips[i].X * scale, TriTips[i].Y * scale, 0), center, _rotX, _rotY, _flatRot),
            ];
        }

        // 色差偏移
        var cyanOff = new Vector2(-2f, 0);
        var magentaOff = new Vector2(2f, 0);

        // --- 色差 pass 1: 青 (偏移 -2, 0) ---
        var cyanCol = ColorU32(new Vector4(0, 1, 1, 1), baseAlpha * 0.5f);
        DrawAllWireframe(dl, cyanOff, cyanCol, 3f, projDiamond, DiamondEdges, projSquare, SquareEdges, projTriVerts);
        DrawAllCircles(dl, cyanOff, cyanCol, 3f, center, _rotX, _rotY, _flatRot, scale);

        // --- 色差 pass 2: 品红 (偏移 +2, 0) ---
        var magCol = ColorU32(new Vector4(1, 0, 1, 1), baseAlpha * 0.5f);
        DrawAllWireframe(dl, magentaOff, magCol, 3f, projDiamond, DiamondEdges, projSquare, SquareEdges, projTriVerts);
        DrawAllCircles(dl, magentaOff, magCol, 3f, center, _rotX, _rotY, _flatRot, scale);

        // --- 色差 pass 3: 主色 (无偏移) ---
        var mainCol = ColorU32(new Vector4(0.7f, 0.8f, 1f, 1), baseAlpha);
        DrawAllWireframe(dl, Vector2.Zero, mainCol, 4.5f, projDiamond, DiamondEdges, projSquare, SquareEdges, projTriVerts);
        DrawAllCircles(dl, Vector2.Zero, mainCol, 4.5f, center, _rotX, _rotY, _flatRot, scale);

        // 顶点亮点
        foreach (var p in projDiamond)
            dl.AddCircleFilled(p, 6f, ColorU32(new Vector4(0.7f, 0.8f, 1f, 1), baseAlpha));
        foreach (var p in projSquare)
            dl.AddCircleFilled(p, 6f, ColorU32(new Vector4(0.7f, 0.8f, 1f, 1), baseAlpha * 0.8f));
        foreach (var p in projFCenters)
            dl.AddCircleFilled(p, 4.5f, ColorU32(new Vector4(0.7f, 0.8f, 1f, 1), baseAlpha * 0.7f));
        foreach (var tri in projTriVerts)
            foreach (var p in tri)
                dl.AddCircleFilled(p, 4.5f, ColorU32(new Vector4(0.7f, 0.8f, 1f, 1), baseAlpha * 0.5f));

        DrawScanNoise(dl, winMin, winMax);

        dl.PopClipRect();
    }

    private static void DrawAllWireframe(ImDrawListPtr dl, Vector2 offset, uint col, float thickness,
        Vector2[] diamond, (int A, int B)[] diamondEdges,
        Vector2[] square, (int A, int B)[] squareEdges,
        Vector2[][] triVerts)
    {
        for (var i = 0; i < diamondEdges.Length; i++)
            dl.AddLine(diamond[diamondEdges[i].A] + offset, diamond[diamondEdges[i].B] + offset, col, thickness);
        for (var i = 0; i < squareEdges.Length; i++)
            dl.AddLine(square[squareEdges[i].A] + offset, square[squareEdges[i].B] + offset, col, thickness);
        for (var t = 0; t < triVerts.Length; t++)
        {
            var tri = triVerts[t];
            dl.AddLine(tri[0] + offset, tri[1] + offset, col, thickness);
            dl.AddLine(tri[1] + offset, tri[2] + offset, col, thickness);
            dl.AddLine(tri[2] + offset, tri[0] + offset, col, thickness);
        }
    }

    private static void DrawAllCircles(ImDrawListPtr dl, Vector2 offset, uint col, float thickness,
        Vector2 screenCenter, float rotX, float rotY, float flatRot, float scale)
    {
        var origin3D = new Vector3(0, 0, 0);
        DrawProjectedCircle(dl, origin3D, 6f * scale, screenCenter, rotX, rotY, flatRot, offset, col, thickness, 128);
        DrawProjectedCircle(dl, origin3D, 4f * scale, screenCenter, rotX, rotY, flatRot, offset, col, thickness, 128);
        for (var i = 0; i < FCenters.Length; i++)
            DrawProjectedCircle(dl, new Vector3(FCenters[i].X * scale, FCenters[i].Y * scale, 0), 2f * scale,
                screenCenter, rotX, rotY, flatRot, offset, col, thickness, 96);
        for (var i = 0; i < TriCentroids.Length; i++)
            DrawProjectedCircle(dl, new Vector3(TriCentroids[i].X * scale, TriCentroids[i].Y * scale, 0), 0.23f * scale,
                screenCenter, rotX, rotY, flatRot, offset, col, thickness, 48);
    }

    private static void DrawProjectedCircle(ImDrawListPtr dl,
        Vector3 center3D, float radius3D,
        Vector2 screenCenter, float rotX, float rotY, float flatRot,
        Vector2 offset, uint color, float thickness, int segs)
    {
        var screenPts = new Vector2[segs];
        for (var i = 0; i < segs; i++)
        {
            var angle = i * MathF.Tau / segs;
            var localPt = new Vector3(
                center3D.X + radius3D * MathF.Cos(angle),
                center3D.Y + radius3D * MathF.Sin(angle),
                center3D.Z);
            screenPts[i] = Project(localPt, screenCenter, rotX, rotY, flatRot) + offset;
        }
        for (var i = 0; i < segs; i++)
            dl.AddLine(screenPts[i], screenPts[(i + 1) % segs], color, thickness);
    }

    private Vector2[] ProjectVerts(Vector2[] localVerts, Vector2 screenCenter, float scale)
    {
        var result = new Vector2[localVerts.Length];
        for (var i = 0; i < localVerts.Length; i++)
            result[i] = Project(new Vector3(localVerts[i].X * scale, localVerts[i].Y * scale, 0),
                screenCenter, _rotX, _rotY, _flatRot);
        return result;
    }

    private static Vector2 Project(Vector3 v, Vector2 center, float rotX, float rotY, float flatRot)
    {
        // Z轴自转（平面内旋转，0.1°/s）
        var zCos = MathF.Cos(flatRot);
        var zSin = MathF.Sin(flatRot);
        var x0 = v.X * zCos - v.Y * zSin;
        var y0 = v.X * zSin + v.Y * zCos;

        var cosY = MathF.Cos(rotY);
        var sinY = MathF.Sin(rotY);
        var x1 = x0 * cosY - v.Z * sinY;
        var z1 = x0 * sinY + v.Z * cosY;

        var cosX = MathF.Cos(rotX);
        var sinX = MathF.Sin(rotX);
        var y1 = y0 * cosX - z1 * sinX;
        var z2 = y0 * sinX + z1 * cosX;

        var perspective = 400f;
        var s = perspective / (perspective + z2);
        return center + new Vector2(x1, y1) * s;
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

    private static uint ColorU32(Vector4 c, float a)
        => ImGui.ColorConvertFloat4ToU32(new Vector4(c.X, c.Y, c.Z, a));
}
