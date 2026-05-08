namespace HiAuRo.ACR;

/// <summary>
/// ACR 作者入口接口 —— 两种 UI 模式可选
/// </summary>
public interface IRotationEntry
{
    /// <summary>作者名</summary>
    string AuthorName { get; }

    /// <summary>false=HiAuRo IUiBuilder / true=ACR 自带 HTML</summary>
    bool UseCustomUi { get; }

    /// <summary>settingFolder = ACR DLL 所在目录</summary>
    Rotation? Build(string settingFolder);

    /// <summary>UseCustomUi=true 时返回 null</summary>
    IRotationUI? GetRotationUI();

    /// <summary>可选设置页</summary>
    void OnDrawSetting();

    /// <summary>资源释放</summary>
    void Dispose();

    /// <summary>进入战斗循环时</summary>
    void OnEnterRotation();

    /// <summary>退出战斗循环时</summary>
    void OnExitRotation();

    /// <summary>支持的职业列表（用于 ACRLoader 注册）</summary>
    IEnumerable<Jobs> TargetJobs { get; }
}
