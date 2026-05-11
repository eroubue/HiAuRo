using System.Numerics;
using Dalamud.Interface.Windowing;
using HiAuRo.Infrastructure;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// 无边框 Overlay 窗口基类 — 毛玻璃背景 + 拖动 + 边缘缩放 + 位置持久化
/// </summary>
public abstract class OverlayBase : Window
{
    protected readonly PluginConfig _config;
    private bool _isDragging;
    private bool _isResizing;
    private Vector2 _dragStartMouse;
    private Vector2 _dragStartPos;
    private Vector2 _dragStartSize;
    private ResizeEdge _resizeEdge;
    private const float EdgeThickness = 6f;

    [Flags]
    private enum ResizeEdge { None = 0, Left = 1, Right = 2, Top = 4, Bottom = 8 }

    protected OverlayBase(string name, PluginConfig config) : base(name)
    {
        _config = config;
        Flags = ImGuiWindowFlags.NoTitleBar
              | ImGuiWindowFlags.NoScrollbar
              | ImGuiWindowFlags.NoFocusOnAppearing
              | ImGuiWindowFlags.NoBackground;
        IsOpen = true;
        RespectCloseHotkey = false;
        ShowCloseButton = false;
    }

    protected abstract void DrawContent();

    public override void Draw()
    {
        HandleInteraction();

        // 毛玻璃背景（先画，内容叠在上方）
        ComponentLibrary.GlassBackground(Theme.RadiusSM);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Theme.PaddingSM);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(6, 4));

        DrawContent();

        ImGui.PopStyleVar(2);

        // 右下角 resize 指示器
        if (!_isDragging && !_isResizing)
        {
            var min = ImGui.GetWindowPos() + ImGui.GetWindowSize() - new Vector2(12, 12);
            ImGui.GetWindowDrawList().AddTriangleFilled(
                min + new Vector2(10, 10),
                min + new Vector2(10, 4),
                min + new Vector2(4, 10),
                ImGui.ColorConvertFloat4ToU32(Theme.Colors.Border));
        }
    }

    private void HandleInteraction()
    {
        var mousePos = ImGui.GetMousePos();
        var winPos = ImGui.GetWindowPos();
        var winSize = ImGui.GetWindowSize();

        if (!_isDragging && !_isResizing)
        {
            // 检测边缘 hover
            _resizeEdge = ResizeEdge.None;
            if (mousePos.X >= winPos.X && mousePos.X <= winPos.X + EdgeThickness)
                _resizeEdge |= ResizeEdge.Left;
            if (mousePos.X >= winPos.X + winSize.X - EdgeThickness && mousePos.X <= winPos.X + winSize.X)
                _resizeEdge |= ResizeEdge.Right;
            if (mousePos.Y >= winPos.Y && mousePos.Y <= winPos.Y + EdgeThickness)
                _resizeEdge |= ResizeEdge.Top;
            if (mousePos.Y >= winPos.Y + winSize.Y - EdgeThickness && mousePos.Y <= winPos.Y + winSize.Y)
                _resizeEdge |= ResizeEdge.Bottom;

            // 鼠标不在窗口内 → 不处理
            var inside = mousePos.X >= winPos.X && mousePos.X <= winPos.X + winSize.X
                      && mousePos.Y >= winPos.Y && mousePos.Y <= winPos.Y + winSize.Y;
            if (!inside) return;

            if (_resizeEdge != ResizeEdge.None && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                _isResizing = true;
                _dragStartMouse = mousePos;
                _dragStartPos = winPos;
                _dragStartSize = winSize;
            }
            else if (_resizeEdge == ResizeEdge.None && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                _isDragging = true;
                _dragStartMouse = mousePos;
                _dragStartPos = winPos;
                _dragStartSize = winSize;
            }
        }

        if (_isResizing)
        {
            var delta = mousePos - _dragStartMouse;
            var newPos = _dragStartPos;
            var newSize = _dragStartSize;

            if (_resizeEdge.HasFlag(ResizeEdge.Left))
            {
                newPos.X = _dragStartPos.X + delta.X;
                newSize.X = _dragStartSize.X - delta.X;
            }
            if (_resizeEdge.HasFlag(ResizeEdge.Right))
                newSize.X = _dragStartSize.X + delta.X;
            if (_resizeEdge.HasFlag(ResizeEdge.Top))
            {
                newPos.Y = _dragStartPos.Y + delta.Y;
                newSize.Y = _dragStartSize.Y - delta.Y;
            }
            if (_resizeEdge.HasFlag(ResizeEdge.Bottom))
                newSize.Y = _dragStartSize.Y + delta.Y;

            // 最小尺寸
            if (newSize.X < 260) { newSize.X = 260; if (_resizeEdge.HasFlag(ResizeEdge.Left)) newPos.X = _dragStartPos.X + _dragStartSize.X - 260; }
            if (newSize.Y < 48) { newSize.Y = 48; if (_resizeEdge.HasFlag(ResizeEdge.Top)) newPos.Y = _dragStartPos.Y + _dragStartSize.Y - 48; }

            ImGui.SetWindowPos(newPos);
            ImGui.SetWindowSize(newSize);

            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                _isResizing = false;
                SavePosition(ImGui.GetWindowPos());
            }
        }

        if (_isDragging)
        {
            if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                ImGui.SetWindowPos(_dragStartPos + (mousePos - _dragStartMouse));
            else
            {
                _isDragging = false;
                SavePosition(ImGui.GetWindowPos());
            }
        }
    }

    protected abstract void SavePosition(Vector2 pos);
}
