using HiAuRo.ACR;
using System.Linq;

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

    /// <summary>添加标签页</summary>
    public void AddTab(string id, string title)
    {
        _currentTab = id;
        _currentGroup = string.Empty;
        _controls.Add(new UiControlDef(id, "tab", null, title, null));
    }

    /// <summary>添加分组</summary>
    public void AddGroup(string id, string title)
    {
        _currentGroup = id;
        _controls.Add(new UiControlDef(id, "group", _currentTab, title, null));
    }

    /// <summary>添加分隔线</summary>
    public void AddSeparator() =>
        _controls.Add(new UiControlDef("__sep__", "separator", _currentGroup, string.Empty, null));

    /// <summary>添加同行标记</summary>
    public void AddSameLine() =>
        _controls.Add(new UiControlDef("__sameline__", "sameLine", _currentGroup, string.Empty, null));

    /// <summary>添加复选框</summary>
    public void AddCheckbox(string id, string label, bool defaultValue) =>
        _controls.Add(new UiControlDef(id, "checkbox", _currentGroup, label, defaultValue));

    /// <summary>添加滑块</summary>
    public void AddSlider(string id, string label, float min, float max, float defaultValue) =>
        _controls.Add(new UiControlDef(id, "slider", _currentGroup, label, defaultValue,
            Options: new { min, max }));

    /// <summary>添加下拉框</summary>
    public void AddDropdown(string id, string label, string[] options, string defaultValue) =>
        _controls.Add(new UiControlDef(id, "dropdown", _currentGroup, label, defaultValue,
            Options: options));

    /// <summary>添加热键</summary>
    public void AddHotkey(string id, string label, string defaultKey, bool defaultVisible = true) =>
        _controls.Add(new UiControlDef(id, "hotkey", _currentGroup, label, defaultKey,
            Meta: new { defaultVisible }));

    /// <summary>添加 QT 热键</summary>
    public void AddQtHotkey(string label, IHotkeyResolver resolver, bool defaultVisible = true)
    {
        HotkeyHelper.Register(resolver);
        _controls.Add(new UiControlDef(resolver.Id, "qthotkey", _currentGroup, label, resolver.DefaultKey,
            Meta: new { defaultVisible }));
    }

    /// <summary>添加 QT 开关</summary>
    public void AddQtToggle(string id, string label, bool defaultValue, string? tooltip = null, string? color = null, bool defaultVisible = true)
    {
        QTHelper.Register(id, label, defaultValue, tooltip, color);
        _controls.Add(new UiControlDef(id, "qttoggle", _currentGroup, label, defaultValue,
            Meta: new { tooltip, color, defaultVisible }));
    }

    /// <summary>添加主控制区</summary>
    public void AddMainControl(bool showPause = true, bool showSave = true)
    {
        _controls.Add(new UiControlDef("__main__", "mainControl", null, string.Empty, true,
            Meta: new { showPause, showSave }));
    }

    /// <summary>添加整数输入</summary>
    public void AddIntInput(string id, string label, int defaultValue, int step = 1, int stepFast = 10) =>
        _controls.Add(new UiControlDef(id, "intInput", _currentGroup, label, defaultValue,
            Meta: new { step, stepFast }));

    /// <summary>添加文本标签</summary>
    public void AddLabel(string id, string text) =>
        _controls.Add(new UiControlDef(id, "label", _currentGroup, text, null));

    /// <summary>添加工具提示</summary>
    public void AddTooltip(string targetId, string tooltip) =>
        _controls.Add(new UiControlDef($"__tip__{targetId}", "tooltip", _currentGroup, string.Empty, tooltip));

    /// <summary>添加热键行</summary>
    public void AddHotkeyRow(params string[] hotkeyIds) =>
        _controls.Add(new UiControlDef("__hkrow__", "hotkeyRow", _currentGroup, string.Empty, null,
            Options: hotkeyIds));

    /// <summary>添加内置 QT 开关</summary>
    public void AddBuiltinQt(BuiltinQt type, bool? defaultValue = null)
    {
        var id = type.GetId();
        var label = type.GetLabel();
        var defaultVal = defaultValue ?? type.GetDefault();
        // 同一个内置 QT 不重复注册
        if (_controls.Any(c => c.Id == id)) return;
        QTHelper.Register(id, label, defaultVal);
        _controls.Add(new UiControlDef(id, "qttoggle", _currentGroup, label, defaultVal,
            Meta: new { defaultVisible = true }));
    }
}
