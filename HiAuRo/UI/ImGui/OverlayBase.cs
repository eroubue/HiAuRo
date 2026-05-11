using System.Numerics;
using Dalamud.Interface.Windowing;
using HiAuRo.Infrastructure;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// 无边框 Overlay 窗口基类 — 拖动 + 位置持久化
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
              | ImGuiWindowFlags.NoResize
              | ImGuiWindowFlags.NoScrollbar
              | ImGuiWindowFlags.NoFocusOnAppearing;
        IsOpen = true;
        RespectCloseHotkey = false;
        ShowCloseButton = false;
    }

    /// <summary>子类重写此方法提供自定义渲染</summary>
    protected abstract void DrawContent();

    public override void Draw()
    {
        HandleDrag();

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Theme.PaddingMD);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, Theme.RadiusMD);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Theme.Colors.BgLayout);
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.Colors.Border);

        DrawContent();

        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(2);
    }

    private void HandleDrag()
    {
        var mousePos = ImGui.GetMousePos();
        var windowPos = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
            mousePos.X >= windowPos.X && mousePos.X <= windowPos.X + windowSize.X &&
            mousePos.Y >= windowPos.Y && mousePos.Y <= windowPos.Y + windowSize.Y)
        {
            _isDragging = false;
        }

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
            {
                ImGui.SetWindowPos(mousePos - _dragOffset);
            }
            else
            {
                _isDragging = false;
                SavePosition(ImGui.GetWindowPos());
            }
        }
    }

    /// <summary>子类实现：保存当前位置到 config</summary>
    protected abstract void SavePosition(Vector2 pos);
}
