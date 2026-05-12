using System.Numerics;
using Dalamud.Interface.Windowing;
using HiAuRo.Infrastructure;
using HiAuRo.ImGuiLib;
using HiAuRo.Runtime;
using HiAuRo.Recording;

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
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(300, 200), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };
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
            if (ImGui.BeginTabItem("ACR Debug"))
            {
                DrawAcrDebug();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("录制"))
            {
                DrawRecording();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void DrawStatus()
    {
        // UI 渲染模式切换
        ImGui.TextColored(Theme.Colors.AccentBlue, "UI 渲染模式:");
        ImGui.SameLine();

        var isWebUI = _config.UIMode == Infrastructure.UIMode.WebUI;
        var cefDisabled = _config.DisableCEF;

        // WebUI 选项 —— 低配模式下灰掉
        if (cefDisabled)
        {
            ImGui.BeginDisabled();
            ImGui.RadioButton("WebUI (CEF 已禁用)", false);
            ImGui.EndDisabled();
        }
        else if (ImGui.RadioButton("WebUI", isWebUI))
        {
            Plugin.Instance._uiManager?.SwitchTo(Infrastructure.UIMode.WebUI);
        }

        ImGui.SameLine();
        if (ImGui.RadioButton("ImGui", !isWebUI))
        {
            Plugin.Instance._uiManager?.SwitchTo(Infrastructure.UIMode.ImGui);
        }

        // 低配置模式复选框
        ImGui.Spacing();
        var newCefDisabled = cefDisabled;
        if (ImGui.Checkbox("低配置模式 (禁用 CEF 以节省 ~200MB 内存)", ref newCefDisabled))
        {
            _config.DisableCEF = newCefDisabled;
            _saveConfig();
        }

        if (_config.DisableCEF)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.Colors.AccentOrange);
            ImGui.TextWrapped("CEF 已禁用，WebUI 不可用。切换此选项需重启插件生效。");
            ImGui.PopStyleColor();
        }

        // ImGui 主题模式（仅 ImGui 模式时显示）
        if (!isWebUI)
        {
            ImGui.Spacing();
            var isLight = _config.ImGuiThemeMode == ImGuiThemeMode.Light;
            ImGui.TextColored(Theme.Colors.AccentBlue, "ImGui 主题:");
            ImGui.SameLine();
            if (ImGui.RadioButton("亮色", isLight))
            {
                _config.ImGuiThemeMode = ImGuiThemeMode.Light;
                Theme.Mode = Theme.ThemeMode.Light;
                _saveConfig();
            }
            ImGui.SameLine();
            if (ImGui.RadioButton("暗色", !isLight))
            {
                _config.ImGuiThemeMode = ImGuiThemeMode.Dark;
                Theme.Mode = Theme.ThemeMode.Dark;
                _saveConfig();
            }
        }

        ImGui.Separator();
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
        ImGui.BulletText("http://localhost:5678/main.html   — 主控制栏");
        ImGui.BulletText("http://localhost:5678/action.html — QT + 热键面板");
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

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("触发器目录同步 (GitHub)");
        ImGui.Separator();

        var ghToken = _config.GitHubToken ?? "";
        var ghRepo = _config.CatalogRepo ?? "";
        var ghBranch = _config.CatalogBranch ?? "";

        ImGui.SetNextItemWidth(250);
        changed |= ImGui.InputTextWithHint("GitHub Token", "ghp_... (repo 权限)", ref ghToken, 128, ImGuiInputTextFlags.Password);

        ImGui.SetNextItemWidth(200);
        changed |= ImGui.InputText("仓库", ref ghRepo, 128);

        ImGui.SetNextItemWidth(150);
        changed |= ImGui.InputText("分支", ref ghBranch, 64);

        if (changed)
        {
            _config.ActionQueueInMs = aq;
            _config.MaxAbilityTimesInGcd = maxAb;
            _config.AoeCount = aoe;
            _config.AttackRange = range;
            _config.DebugEnabled = debug;
            _config.GitHubToken = ghToken.Length > 0 ? ghToken : null;
            _config.CatalogRepo = ghRepo;
            _config.CatalogBranch = ghBranch;
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

    private void DrawAcrDebug()
    {
        ImGui.Spacing();
        ImGui.Text("SlotResolver 实时状态");
        ImGui.Separator();

        var runner = ACRLifecycle.Runner;
        if (runner.AiLoop is not AILoop_Normal loop)
        {
            ImGui.Text("无活跃 ACR 或 IAILoop 类型不匹配");
            return;
        }

        var resolvers = loop.DebugResolvers;
        if (resolvers.Count == 0)
        {
            ImGui.Text("没有已注册的 SlotResolver");
            return;
        }

        // GCD 状态条
        float gcdRemain = ACR.GCDHelper.GetGCDCooldown();
        bool gcdReady = gcdRemain <= 0;
        bool ogcdWindow = ACR.GCDHelper.CanUseOffGcd();
        ImGui.TextColored(gcdReady ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0.6f, 0, 1),
            $"GCD: {(gcdReady ? "就绪" : $"{gcdRemain:F0}ms")} | oGCD窗口: {(ogcdWindow ? "开" : "关")}");

        ImGui.SameLine();
        ImGui.TextDisabled($"(共 {resolvers.Count} 个)");

        ImGui.Spacing();

        if (!ImGui.BeginTable("##AcrDebugTable", 5,
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
            return;

        ImGui.TableSetupColumn("Resolver", ImGuiTableColumnFlags.WidthFixed, 160);
        ImGui.TableSetupColumn("Mode", ImGuiTableColumnFlags.WidthFixed, 55);
        ImGui.TableSetupColumn("Check", ImGuiTableColumnFlags.WidthFixed, 45);
        ImGui.TableSetupColumn("窗口", ImGuiTableColumnFlags.WidthFixed, 40);
        ImGui.TableSetupColumn("产出技能", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();

        foreach (var info in resolvers)
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.Text(info.Name);

            ImGui.TableNextColumn();
            ImGui.Text(info.Mode.ToString());

            ImGui.TableNextColumn();
            if (info.CheckThrew)
            {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), "ERR");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(info.CheckError);
            }
            else if (info.CheckResult >= 0)
            {
                ImGui.TextColored(new Vector4(0, 1, 0, 1), info.CheckResult.ToString());
            }
            else
            {
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), info.CheckResult.ToString());
            }

            ImGui.TableNextColumn();
            if (info.BuiltSlot)
            {
                ImGui.TextColored(new Vector4(0, 1, 0, 1), "●");
            }
            else if (info.PassedWindow)
            {
                ImGui.TextColored(new Vector4(1, 1, 0, 1), "○");
            }
            else if (info.CheckResult >= 0)
            {
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), "✗");
            }
            else
            {
                ImGui.Text("-");
            }

            ImGui.TableNextColumn();
            if (info.BuiltSlot && info.BuiltSkills.Length > 0)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0, 1, 0, 1));
                ImGui.TextWrapped(info.BuiltSkills);
                ImGui.PopStyleColor();
            }
            else if (info.CheckResult >= 0 && !info.PassedWindow)
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "等待窗口");
            }
        }

        ImGui.EndTable();
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

            ImGui.SameLine();
            if (ImGui.Button("CEF DevTools"))
                host?.DebugOverlay(ol.Name);

            ImGui.Unindent(16);
            ImGui.Spacing();
            ImGui.PopID();
        }

        if (changed) _saveConfig();
    }

    private static void DrawRecording()
    {
        ImGui.Spacing();
        ImGui.Text("副本录制状态");

        var recorder = EncounterRecorder.Instance;
        var isRecording = recorder.IsRecording;

        if (isRecording)
        {
            var seconds = recorder.ElapsedSeconds;
            ImGui.TextColored(new Vector4(1, 0.3f, 0.3f, 1),
                $"● 录制中 ({seconds / 60:D2}:{seconds % 60:D2})");
            ImGui.Text($"文件名: {recorder.CurrentFileName}");
        }
        else
        {
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), "○ 就绪");
        }

        ImGui.Spacing();
        ImGui.Separator();

        ImGui.Text("录制历史:");
        ImGui.Spacing();

        var files = recorder.GetRecordFiles();
        if (files.Length == 0)
        {
            ImGui.TextDisabled("暂无录制记录");
        }
        else
        {
            ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1);
            ImGui.BeginChild("##RecordingList",
                new Vector2(-1, 80), true);

            foreach (var (name, path) in files.Take(20))
            {
                ImGui.Text(name);
                ImGui.SameLine();
                ImGui.TextDisabled($"({path})");
            }

            ImGui.EndChild();
            ImGui.PopStyleVar();
        }

        ImGui.Spacing();
        if (ImGui.Button("打开录制目录"))
        {
            var dir = Path.Combine(
                DService.Instance().PI.ConfigDirectory.FullName, "Recordings");
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", dir);
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Debug($"[Recording] 无法打开目录: {ex.Message}");
            }
        }
    }
}
