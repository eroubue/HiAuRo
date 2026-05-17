namespace HiAuRo.ACR;

/// <summary>
/// 描述性 UI 控件注册接口 —— C# 描述 UI，HiAuRo 转为 JSON → Web 前端渲染
/// 每个方法提供两个重载：带 id 参数（旧兼容）和不带 id（label 自动作为 id）
/// </summary>
public interface IUiBuilder
{
    // === 结构 ===
    void AddTab( string title);
    void EndTab();
    void AddGroup(string title);
    void AddSeparator();
    void AddSameLine();
    void AddMainControl(bool showPause = true, bool showSave = true);

    // === 控件（带 id） ===
    void AddCheckbox(string label, bool defaultValue);
    void AddSlider( string label, float min, float max, float defaultValue);
    void AddDropdown(string label, string[] options, string defaultValue);
    void AddIntInput(string label, int defaultValue, int step = 1, int stepFast = 10);
    void AddLabel(string text);
    void AddQtToggle(string label, bool defaultValue, string? tooltip = null, string? color = null, bool defaultVisible = true);

    /*// === 控件（自动 id=label） ===
    void AddCheckbox(string label, bool defaultValue) =>
        AddCheckbox(label, label, defaultValue);
    void AddSlider(string label, float min, float max, float defaultValue) =>
        AddSlider(label, label, min, max, defaultValue);
    void AddDropdown(string label, string[] options, string defaultValue) =>
        AddDropdown(label, label, options, defaultValue);
    void AddHotkey(string label, string defaultKey, bool defaultVisible = true) =>
        AddHotkey(label, label, defaultKey, defaultVisible);
    void AddIntInput(string label, int defaultValue, int step = 1, int stepFast = 10) =>
        AddIntInput(label, label, defaultValue, step, stepFast);
    void AddQtToggle(string label, bool defaultValue, string? tooltip = null, string? color = null, bool defaultVisible = true) =>
        AddQtToggle(label, label, defaultValue, tooltip, color, defaultVisible);*/

    // === QT / 热键（无 id，label 即标识） ===
    void AddQtHotkey(string label, IHotkeyResolver resolver, bool defaultVisible = true);
    void AddTooltip(string targetId, string tooltip);
    void AddHotkeyRow(IHotkeyResolver[] hotkeyIds);
    void AddBuiltinQt(BuiltinQt type, bool? defaultValue = null);
}
