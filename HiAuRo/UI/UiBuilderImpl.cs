using HiAuRo.ACR;

namespace HiAuRo.UI;

/// <summary>
/// IUiBuilder 实现 —— 收集控件定义为 UiControlDef 列表
/// </summary>
public sealed class UiBuilderImpl : HiAuRo.ACR.IUiBuilder
{
    private readonly List<UiControlDef> _controls = [];
    private string _currentTab = string.Empty;
    private string _currentGroup = string.Empty;

    /// <summary>获取收集到的所有控件定义</summary>
    public List<UiControlDef> GetControls() => [.. _controls];

    public void AddTab(string id, string title)
    {
        _currentTab = id;
        _currentGroup = string.Empty;
        _controls.Add(new UiControlDef(id, "tab", null, title, null));
    }

    public void AddGroup(string id, string title)
    {
        _currentGroup = id;
        _controls.Add(new UiControlDef(id, "group", _currentTab, title, null));
    }

    public void AddSeparator() =>
        _controls.Add(new UiControlDef("__sep__", "separator", _currentGroup, string.Empty, null));

    public void AddSameLine() =>
        _controls.Add(new UiControlDef("__sameline__", "sameLine", _currentGroup, string.Empty, null));

    public void AddCheckbox(string id, string label, bool defaultValue) =>
        _controls.Add(new UiControlDef(id, "checkbox", _currentGroup, label, defaultValue));

    public void AddSlider(string id, string label, float min, float max, float defaultValue) =>
        _controls.Add(new UiControlDef(id, "slider", _currentGroup, label, defaultValue,
            Options: new { min, max }));

    public void AddDropdown(string id, string label, string[] options, string defaultValue) =>
        _controls.Add(new UiControlDef(id, "dropdown", _currentGroup, label, defaultValue,
            Options: options));

    public void AddHotkey(string id, string label, string defaultKey) =>
        _controls.Add(new UiControlDef(id, "hotkey", _currentGroup, label, defaultKey));

    public void AddQtHotkey(string label, IHotkeyResolver resolver)
    {
        HotkeyHelper.Register(resolver);
        _controls.Add(new UiControlDef(resolver.Id, "qthotkey", _currentGroup, label, resolver.DefaultKey));
    }

    public void AddQtToggle(string id, string label, bool defaultValue, string? tooltip = null, string? color = null)
    {
        QTHelper.Register(id, label, defaultValue, tooltip, color);
        _controls.Add(new UiControlDef(id, "qttoggle", _currentGroup, label, defaultValue,
            Meta: new { tooltip, color }));
    }

    public void AddMainControl(bool showPause = true, bool showSave = true)
    {
        _controls.Add(new UiControlDef("__main__", "mainControl", null, string.Empty, true,
            Meta: new { showPause, showSave }));
    }

    public void AddIntInput(string id, string label, int defaultValue, int step = 1, int stepFast = 10) =>
        _controls.Add(new UiControlDef(id, "intInput", _currentGroup, label, defaultValue,
            Meta: new { step, stepFast }));

    public void AddLabel(string id, string text) =>
        _controls.Add(new UiControlDef(id, "label", _currentGroup, text, null));

    public void AddTooltip(string targetId, string tooltip) =>
        _controls.Add(new UiControlDef($"__tip__{targetId}", "tooltip", _currentGroup, string.Empty, tooltip));
}
