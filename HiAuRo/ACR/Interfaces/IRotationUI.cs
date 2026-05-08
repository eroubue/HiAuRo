namespace HiAuRo.ACR;

/// <summary>
/// ACR 作者悬浮窗 UI 接口
/// </summary>
public interface IRotationUI
{
    /// <summary>注册 UI 控件，HiAuRo 自动转为 Web 前端渲染</summary>
    void RegisterControls(IUiBuilder builder);
}
