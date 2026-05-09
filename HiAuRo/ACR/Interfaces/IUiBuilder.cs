namespace HiAuRo.ACR;

/// <summary>
/// 描述性 UI 控件注册接口 —— C# 描述 UI，HiAuRo 转为 JSON → Web 前端渲染
/// </summary>
public interface IUiBuilder
{
    void AddTab(string id, string title);
    void AddGroup(string id, string title);
    void AddSeparator();
    void AddSameLine();

    void AddCheckbox(string id, string label, bool defaultValue);
    void AddSlider(string id, string label, float min, float max, float defaultValue);
    void AddDropdown(string id, string label, string[] options, string defaultValue);
    void AddHotkey(string id, string label, string defaultKey, bool defaultVisible = true);
    void AddIntInput(string id, string label, int defaultValue, int step = 1, int stepFast = 10);
    void AddLabel(string id, string text);

    void AddQtHotkey(string label, IHotkeyResolver resolver, bool defaultVisible = true);
    void AddQtToggle(string id, string label, bool defaultValue, string? tooltip = null, string? color = null, bool defaultVisible = true);
    void AddMainControl(bool showPause = true, bool showSave = true);
    void AddTooltip(string targetId, string tooltip);
}
