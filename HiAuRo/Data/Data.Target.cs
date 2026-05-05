using OmenTools.OmenService;
using OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds;

namespace HiAuRo.Data;

/// <summary>
/// 目标数据 —— 转发 TargetManager.*
/// </summary>
public static class Target
{
    public static IGameObject? Current => TargetManager.Target;

    public static IGameObject? Focus => TargetManager.FocusTarget;

    public static IGameObject? MouseOver => TargetManager.MouseOverTarget;

    public static IGameObject? Soft => TargetManager.SoftTarget;

    public static IGameObject? Previous => TargetManager.PreviousTarget;
}
