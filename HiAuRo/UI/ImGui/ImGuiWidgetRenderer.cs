using System.Linq;
using System.Text.Json;
using HiAuRo.Setting;
using HiAuRo.UI;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// UiControlDef → ImGui 组件映射渲染器
/// 遍历控件列表，按 Tab → Group → Items 结构渲染
/// </summary>
public static class ImGuiWidgetRenderer
{
    /// <summary>渲染指定 Tab 下的所有控件</summary>
    public static void Render(List<UiControlDef> controls, string activeTab)
    {
        if (controls.Count == 0) return;

        // 渲染 mainControl
        var mainCtrl = controls.FirstOrDefault(c => c.Type == "mainControl");
        if (mainCtrl != null) RenderMainControl(mainCtrl);

        // 渲染该 Tab 下的 Groups
        var groups = controls.Where(c => c.Type == "group" && c.ParentId == activeTab).ToList();
        if (groups.Count == 0)
        {
            RenderItems(controls.Where(c => c.ParentId == null && c.Type is not ("tab" or "mainControl")));
            return;
        }

        foreach (var group in groups)
        {
            ComponentLibrary.CardBegin(group.Label);
            var items = controls.Where(c => c.ParentId == group.Id);
            RenderItems(items);
            ComponentLibrary.CardEnd();
            ImGui.Spacing();
        }
    }

    private static void RenderItems(IEnumerable<UiControlDef> items)
    {
        foreach (var item in items)
        {
            switch (item.Type)
            {
                case "checkbox":
                    RenderCheckbox(item);
                    break;
                case "slider":
                    RenderSlider(item);
                    break;
                case "dropdown":
                    RenderDropdown(item);
                    break;
                case "intInput":
                    RenderIntInput(item);
                    break;
                case "label":
                    ComponentLibrary.Label(item.Label);
                    break;
                case "separator":
                    ComponentLibrary.Divider();
                    break;
                case "sameLine":
                    ImGui.SameLine();
                    break;
            }
        }
    }

    private static void RenderMainControl(UiControlDef ctrl)
    {
        var meta = ctrl.Meta as JsonElement?;
        var showPause = true;
        var showSave = true;
        if (meta.HasValue)
        {
            showPause = meta.Value.TryGetProperty("showPause", out var p) ? p.GetBoolean() : true;
            showSave = meta.Value.TryGetProperty("showSave", out var s) ? s.GetBoolean() : true;
        }

        ComponentLibrary.Badge(ImGuiOverlayState.IsRunning, Theme.Colors.AccentGreen);
        ImGui.SameLine();
        ComponentLibrary.Label(ImGuiOverlayState.AcrName);

        ImGui.SameLine();
        if (ComponentLibrary.Button(ImGuiOverlayState.IsRunning ? "停止" : "启动"))
        {
            if (HiAuRo.Runtime.RuntimeCore.IsRunning) HiAuRo.Runtime.RuntimeCore.Stop();
            else HiAuRo.Runtime.RuntimeCore.Start();
        }

        if (showPause && ImGuiOverlayState.IsRunning)
        {
            ImGui.SameLine();
            if (ComponentLibrary.Button(ImGuiOverlayState.IsPaused ? "继续" : "暂停"))
                HiAuRo.ACR.MainControlHelper.TogglePause();
        }

        if (showSave)
        {
            ImGui.SameLine();
            if (ComponentLibrary.Button("保存"))
                HiAuRo.ACR.MainControlHelper.Save();
        }
    }

    private static void RenderCheckbox(UiControlDef ctrl)
    {
        var val = ImGuiOverlayState.GetValue(ctrl.Id, ctrl.Value is bool b && b);
        if (ComponentLibrary.Switch(ctrl.Id, ctrl.Label, ref val))
        {
            ImGuiOverlayState.SetValue(ctrl.Id, val);
            SaveSettings();
        }
    }

    private static void RenderSlider(UiControlDef ctrl)
    {
        var val = ImGuiOverlayState.GetValue(ctrl.Id, ctrl.Value is float f ? f : 0f);
        float min = 0, max = 100;
        if (ctrl.Options is JsonElement opts)
        {
            min = opts.TryGetProperty("min", out var mn) ? mn.GetSingle() : 0;
            max = opts.TryGetProperty("max", out var mx) ? mx.GetSingle() : 100;
        }
        if (ComponentLibrary.Slider(ctrl.Id, ctrl.Label, ref val, min, max))
        {
            ImGuiOverlayState.SetValue(ctrl.Id, val);
            SaveSettings();
        }
    }

    private static void RenderDropdown(UiControlDef ctrl)
    {
        var options = Array.Empty<string>();
        if (ctrl.Options is JsonElement opts)
        {
            options = opts.EnumerateArray().Select(e => e.GetString() ?? "").ToArray();
        }
        var selectedIdx = ImGuiOverlayState.GetValue(ctrl.Id, 0);
        if (options.Length > 0 && selectedIdx >= options.Length) selectedIdx = 0;
        if (ComponentLibrary.Select(ctrl.Id, ctrl.Label, ref selectedIdx, options))
        {
            ImGuiOverlayState.SetValue(ctrl.Id, selectedIdx);
            SaveSettings();
        }
    }

    private static void RenderIntInput(UiControlDef ctrl)
    {
        var val = ImGuiOverlayState.GetValue(ctrl.Id, ctrl.Value is int i ? i : 0);
        var step = 1;
        var stepFast = 10;
        if (ctrl.Meta is JsonElement meta)
        {
            step = meta.TryGetProperty("step", out var s) ? s.GetInt32() : 1;
            stepFast = meta.TryGetProperty("stepFast", out var sf) ? sf.GetInt32() : 10;
        }
        if (ComponentLibrary.InputNumber(ctrl.Id, ctrl.Label, ref val, step, stepFast))
        {
            ImGuiOverlayState.SetValue(ctrl.Id, val);
            SaveSettings();
        }
    }

    private static void SaveSettings()
    {
        var author = HiAuRo.Runtime.ACRLifecycle.CurrentAuthor;
        var jobId = HiAuRo.Runtime.ACRLifecycle.CurrentJobId;
        if (string.IsNullOrEmpty(author) || jobId == 0) return;
        SettingMgr.SaveAcrUiSettings(author, jobId, ImGuiOverlayState.UiSettings);
    }
}
