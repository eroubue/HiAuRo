using System.Numerics;
using Dalamud.Interface.Windowing;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// ImGui 组件展示窗口 — /hi gallery 打开
/// </summary>
public sealed class DemoWindow : Window
{
    private bool _demoSwitch = true;
    private bool _demoSwitch2;
    private float _demoSlider = 50f;
    private int _demoSliderInt = 3;
    private int _demoSelect;
    private readonly string[] _demoOptions = ["选项A", "选项B", "选项C", "选项D"];
    private int _demoInput = 42;
    private int _demoTab;
    private readonly string[] _demoTabNames = ["常规", "高级", "关于"];

    public DemoWindow() : base("HiAuRo 组件展示##Gallery")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        IsOpen = false;
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("##galleryMainTabs"))
        {
            if (ImGui.BeginTabItem("组件预览"))
            {
                DrawComponentPreview();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("主题色"))
            {
                DrawThemePreview();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void DrawComponentPreview()
    {
        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "Button");
        ComponentLibrary.Button("主按钮");
        ImGui.SameLine();
        ComponentLibrary.Button("次要按钮");
        ImGui.SameLine();
        ComponentLibrary.Button("禁用按钮", disabled: true);
        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "Switch");
        ComponentLibrary.Switch("sw1", "AoE 技能", ref _demoSwitch);
        ComponentLibrary.Switch("sw2", "爆发药", ref _demoSwitch2);
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "Slider");
        ComponentLibrary.Slider("sl1", "攻击距离", ref _demoSlider, 5, 40);
        ComponentLibrary.SliderInt("sl2", "AOE 数量", ref _demoSliderInt, 1, 10);
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "Select");
        ComponentLibrary.Select("sel1", "技能顺序", ref _demoSelect, _demoOptions);
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "Tabs");
        ComponentLibrary.Tabs("demoTabs", ref _demoTab, _demoTabNames);
        ImGui.Text($"当前标签: {_demoTabNames[_demoTab]}");
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "Card");
        ComponentLibrary.CardBegin("基本设置");
        ComponentLibrary.Switch("cardSw", "启用功能", ref _demoSwitch);
        ComponentLibrary.InputNumber("cardNum", "最大数量", ref _demoInput, 1, 10);
        ComponentLibrary.CardEnd();
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "Tag");
        ComponentLibrary.Tag("AoE", true, Theme.Colors.AccentGreen);
        ImGui.SameLine();
        ComponentLibrary.Tag("爆发", true, Theme.Colors.AccentBlue);
        ImGui.SameLine();
        ComponentLibrary.Tag("疾跑", true, Theme.Colors.AccentOrange);
        ImGui.SameLine();
        ComponentLibrary.Tag("吃药", false);
        ImGui.SameLine();
        ComponentLibrary.Tag("防击退", false);
        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "Badge");
        ComponentLibrary.Badge(true, Theme.Colors.AccentGreen);
        ImGui.SameLine(); ImGui.Text("运行中");
        ImGui.SameLine();
        ComponentLibrary.Badge(true, Theme.Colors.AccentOrange);
        ImGui.SameLine(); ImGui.Text("已暂停");
        ImGui.SameLine();
        ComponentLibrary.Badge(false);
        ImGui.SameLine(); ImGui.Text("已停止");
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "InputNumber");
        ComponentLibrary.InputNumber("num1", "数值输入", ref _demoInput);
        ImGui.Spacing();

        ComponentLibrary.Divider();
        ImGui.Text("上方为 Divider 分割线");
    }

    private static void DrawThemePreview()
    {
        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "背景色");
        DrawColorSwatch("BgLayout", Theme.Colors.BgLayout);
        DrawColorSwatch("BgContainer", Theme.Colors.BgContainer);
        DrawColorSwatch("BgElevated", Theme.Colors.BgElevated);
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "文字色");
        DrawColorSwatch("TextPrimary", Theme.Colors.TextPrimary);
        DrawColorSwatch("TextSecondary", Theme.Colors.TextSecondary);
        DrawColorSwatch("TextTertiary", Theme.Colors.TextTertiary);
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.4f, 0.6f, 1f, 1f), "品牌色");
        DrawColorSwatch("AccentBlue", Theme.Colors.AccentBlue);
        DrawColorSwatch("AccentGreen", Theme.Colors.AccentGreen);
        DrawColorSwatch("AccentRed", Theme.Colors.AccentRed);
        DrawColorSwatch("AccentOrange", Theme.Colors.AccentOrange);
    }

    private static void DrawColorSwatch(string name, Vector4 color)
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        dl.AddRectFilled(pos, pos + new Vector2(20, 20), ImGui.ColorConvertFloat4ToU32(color));
        dl.AddRect(pos, pos + new Vector2(20, 20), ImGui.ColorConvertFloat4ToU32(Theme.Colors.Border));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 28);
        ImGui.Text(name);
    }
}
