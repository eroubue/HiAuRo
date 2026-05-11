using System.Numerics;
using Dalamud.Interface.Windowing;
using HiAuRo.Infrastructure;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// 无边框 Overlay 窗口基类 — 毛玻璃背景 + 拖动 + 位置持久化
/// </summary>
public abstract class OverlayBase : Window
{
    protected readonly PluginConfig _config;
    private bool _isDragging;
    private Vector2 _dragOffset;

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
        HandleDrag();

        // 绘制毛玻璃背景
        ComponentLibrary.GlassBackground(Theme.RadiusSM);

        // 紧凑内边距
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Theme.PaddingXS);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 2));

        DrawContent();

        ImGui.PopStyleVar(2);
    }

    private void HandleDrag()
    {
        var mousePos = ImGui.GetMousePos();
        var windowPos = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();

        if (!_isDragging && ImGui.IsMouseDragging(ImGuiMouseButton.Left) &&
            mousePos.X >= windowPos.X && mousePos.X <= windowPos.X + windowSize.X &&
            mousePos.Y >= windowPos.Y && mousePos.Y <= windowPos.Y + windowSize.Y)
        {
            _isDragging = true;
            _dragOffset = mousePos - windowPos;
        }

        if (_isDragging)
        {
            if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                ImGui.SetWindowPos(mousePos - _dragOffset);
            else
            {
                _isDragging = false;
                SavePosition(ImGui.GetWindowPos());
            }
        }
    }

    protected abstract void SavePosition(Vector2 pos);
}
