using System.Numerics;
using Dalamud.Interface.Windowing;
using HiAuRo.Infrastructure;
using HiAuRo.Runtime;

namespace HiAuRo.UI;

/// <summary>
/// HiAuRo ImGui 主界面 —— 状态 / 设置 / 窗口设置 / Debug
/// </summary>
public sealed class MainWindow : Window
{
    private readonly PluginConfig _config;
    private readonly Action _saveConfig;

    public MainWindow(PluginConfig config, Action saveConfig) : base("HiAuRo##Main")
    {
        _config = config;
        _saveConfig = saveConfig;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(300, 200), MaximumSize = new Vector2(520, 600) };
        IsOpen = false;
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("##HiAuRoTabs"))
        {
            if (ImGui.BeginTabItem("状态"))
            {
                DrawStatus();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("设置"))
            {
                DrawSettings();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("窗口设置"))
            {
                DrawOverlaySettings();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Debug"))
            {
                DrawDebug();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void DrawStatus()
    {
        ImGui.Spacing();
        ImGui.Text("ACR 运行状态");

        var running = RuntimeCore.IsRunning;
        var paused = ACR.MainControlHelper.IsPaused;

        string state = running ? (paused ? "⏸ 已暂停" : "● 运行中") : "○ 已停止";
        Vector4 color = running ? (paused ? new Vector4(1f, 0.65f, 0, 1f) : new Vector4(0, 1, 0, 1)) : new Vector4(1, 0.3f, 0.3f, 1);
        ImGui.TextColored(color, state);

        ImGui.SameLine();
        if (ImGui.Button(running ? "停止" : "启动"))
        {
            if (running) RuntimeCore.Stop();
            else RuntimeCore.Start();
        }
        if (running)
        {
            ImGui.SameLine();
            if (ImGui.Button(paused ? "继续" : "暂停"))
                ACR.MainControlHelper.TogglePause();
        }

        ImGui.Spacing();
        ImGui.Separator();

        if (!HiAuRo.Data.Data.IsReady)
        {
            ImGui.Text("等待角色加载...");
            return;
        }

        ImGui.Text($"战斗状态: {CombatContext.CurrentState}");
        ImGui.Text($"当前职业: {Data.Me.ClassJob}");
        ImGui.Text($"当前 ACR: {ACRLifecycle.CurrentAcrName}");
        ImGui.Text($"GCD 剩余: {ACR.GCDHelper.GetGCDCooldown():F0}ms");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("游戏内 CEF 悬浮窗（浏览器打开测试）:");
        ImGui.BulletText("http://localhost:5678/main.html — 主控制栏");
        ImGui.BulletText("http://localhost:5678/qt.html   — QT 开关");
        ImGui.BulletText("http://localhost:5678/hotkey.html — 热键按钮");
        ImGui.Spacing();
    }

    private void DrawSettings()
    {
        ImGui.Spacing();

        var changed = false;
        var aq = _config.ActionQueueInMs;
        var maxAb = _config.MaxAbilityTimesInGcd;
        var aoe = _config.AoeCount;
        var range = _config.AttackRange;
        var debug = _config.DebugEnabled;

        ImGui.Text("全局设置");
        ImGui.Separator();

        ImGui.PushItemWidth(100);
        changed |= ImGui.InputInt("技能队列窗口 (ms)", ref aq, 50);
        changed |= ImGui.InputInt("GCD 内能力技上限", ref maxAb, 1);
        changed |= ImGui.InputInt("AOE 判定敌人数", ref aoe, 1);
        changed |= ImGui.SliderFloat("攻击距离", ref range, 5f, 40f, "%.1f");
        ImGui.PopItemWidth();

        ImGui.Separator();
        changed |= ImGui.Checkbox("Debug 日志", ref debug);

        if (changed)
        {
            _config.ActionQueueInMs = aq;
            _config.MaxAbilityTimesInGcd = maxAb;
            _config.AoeCount = aoe;
            _config.AttackRange = range;
            _config.DebugEnabled = debug;
            _saveConfig();
        }
    }

    private void DrawDebug()
    {
        ImGui.Spacing();
        ImGui.Text("运行时信息");

        if (ImGui.Button("重载 ACR"))
            ACRLifecycle.Runner.Reset();

        ImGui.SameLine();
        if (ImGui.Button("清空协程"))
            Coroutine.Instance.Clear();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text($"ACR: {ACRLifecycle.CurrentAcrName}");
        ImGui.Text($"SpellQueue: {ACRLifecycle.Runner.SpellQueue.QueueSize}");
        ImGui.Text($"OpenerMgr: {ACRLifecycle.Runner.OpenerMgr.CurrentState}");
    }

    private void DrawOverlaySettings()
    {
        ImGui.Spacing();
        ImGui.Text("CEF 游戏内悬浮窗");
        ImGui.Separator();

        var overlays = _config.Overlays;
        if (overlays == null || overlays.Length == 0) return;

        var changed = false;
        var host = Plugin.BrowserHost;

        for (int i = 0; i < overlays.Length; i++)
        {
            var ol = overlays[i];

            ImGui.PushID(i);
            var vis = ol.Visible;
            if (ImGui.Checkbox(ol.Name, ref vis))
            {
                ol.Visible = vis;
                changed = true;
            }

            ImGui.Indent(16);

            var url = ol.Url;
            ImGui.SetNextItemWidth(300);
            if (ImGui.InputText("URL", ref url, 256))
            {
                ol.Url = url;
                host?.UpdateOverlay(ol.Name, url: url);
                changed = true;
            }

            var w = ol.Width;
            ImGui.SetNextItemWidth(80);
            if (ImGui.InputInt("宽", ref w, 10))
            {
                ol.Width = w;
                host?.UpdateOverlay(ol.Name, width: w, height: ol.Height);
                changed = true;
            }
            ImGui.SameLine();
            var h = ol.Height;
            ImGui.SetNextItemWidth(80);
            if (ImGui.InputInt("高", ref h, 10))
            {
                ol.Height = h;
                host?.UpdateOverlay(ol.Name, width: ol.Width, height: h);
                changed = true;
            }

            var zoom = ol.Zoom;
            ImGui.SetNextItemWidth(150);
            if (ImGui.SliderFloat("缩放 %", ref zoom, 50f, 200f, "%.0f%%"))
            {
                ol.Zoom = zoom;
                host?.UpdateOverlay(ol.Name, zoom: zoom);
                changed = true;
            }

            var locked = ol.Locked;
            if (ImGui.Checkbox("锁定窗口", ref locked))
            {
                ol.Locked = locked;
                host?.UpdateOverlay(ol.Name, locked: locked);
                changed = true;
            }

            ImGui.Unindent(16);
            ImGui.Spacing();
            ImGui.PopID();
        }

        if (changed) _saveConfig();
    }
}
