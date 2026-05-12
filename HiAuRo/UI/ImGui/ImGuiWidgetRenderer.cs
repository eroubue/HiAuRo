using System.Linq;
using System.Numerics;
using System.Text.Json;
using Dalamud.Interface.Textures;
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

        // 渲染该 Tab 下的 Groups
        var groups = controls.Where(c => c.Type == "group" && c.ParentId == activeTab).ToList();
        if (groups.Count == 0)
        {
            // 无 group 时直接渲染此 tab 下的 item（或无 tab 时的顶层 item）
            RenderItems(controls.Where(c =>
                (c.ParentId == activeTab || c.ParentId == null) &&
                c.Type is not ("tab" or "mainControl")));
            return;
        }

        foreach (var group in groups)
        {
            ImGui.PushFont(UiBuilder.DefaultFont);
            ImGui.TextColored(Theme.Colors.TextPrimary, group.Label);
            ImGui.PopFont();
            ImGui.Spacing();
            var items = controls.Where(c => c.ParentId == group.Id);
            RenderItems(items);
            ImGui.Spacing();
            ComponentLibrary.Divider();
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
                case "hotkeyRow":
                    RenderHotkeyRow(item);
                    break;
            }
        }
    }

    private static void RenderHotkeyRow(UiControlDef ctrl)
    {
        var ids = ctrl.Options switch
        {
            JsonElement el when el.ValueKind == JsonValueKind.Array =>
                el.EnumerateArray().Select(e => e.GetString() ?? "").ToArray(),
            string[] arr => arr,
            _ => Array.Empty<string>()
        };
        if (ids.Length == 0) return;

        var allHotkeys = HiAuRo.ACR.HotkeyHelper.GetAll();

        for (int i = 0; i < ids.Length; i++)
        {
            var hk = allHotkeys.FirstOrDefault(h => h.Id == ids[i]);
            if (hk == null) continue;

            if (i > 0) ImGui.SameLine();

            var available = hk.Check() >= 0;
            var binding = HiAuRo.ACR.HotkeyHelper.GetBinding(hk.Id);

            var tex = hk.IconId > 0
                ? DService.Instance().Texture.GetFromGameIcon(
                    new GameIconLookup(hk.IconId))
                : null;

            if (tex != null)
            {
                var wrap = tex.GetWrapOrEmpty();
                var handle = wrap?.Handle ?? 0;
                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4);
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4, 4));
                var clicked = ImGui.Button($"##hkbtn-{hk.Id}", new Vector2(36, 36));
                var rectMin = ImGui.GetItemRectMin();
                var rectMax = ImGui.GetItemRectMax();
                var pad = 4f;
                if (handle != (nint)0)
                    ImGui.GetWindowDrawList().AddImage(
                        handle, rectMin + new Vector2(pad), rectMax - new Vector2(pad));
                ImGui.PopStyleVar(2);
                if (clicked)
                    HiAuRo.ACR.HotkeyHelper.ExecuteById(hk.Id);
                if (ImGui.IsItemHovered())
                {
                    var tip = string.IsNullOrEmpty(binding) ? hk.Label : $"{hk.Label}   {binding}";
                    ImGui.SetTooltip(tip);
                }
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button, available
                    ? Theme.Colors.AccentBlue
                    : new Vector4(0.3f, 0.3f, 0.3f, 1));
                if (ImGui.Button($"{hk.Label}###hkbtn-{hk.Id}"))
                    HiAuRo.ACR.HotkeyHelper.ExecuteById(hk.Id);
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text(hk.Label);
                    if (!string.IsNullOrEmpty(binding))
                    {
                        ImGui.SameLine();
                        ImGui.TextDisabled($"({binding})");
                    }
                    ImGui.EndTooltip();
                }
            }
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
