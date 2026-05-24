#if DEBUG
using System.Numerics;
using System.Diagnostics;
using Dalamud.Interface.Windowing;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// DEBUG 性能监控窗口 —— 显示各模块每帧耗时(μs)
/// </summary>
public sealed class DebugPerfWindow : Window
{
    /// <summary>全局单例，供 /hi debug 命令调用</summary>
    public static DebugPerfWindow? Instance { get; private set; }

    public DebugPerfWindow() : base("HiAuRo 性能监控##Debug")
    {
        Instance = this;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        IsOpen = false;
    }

    public override void Draw()
    {
        var data = PerfMonitor.LastFrame;
        var total = PerfMonitor.TotalUs;
        var maxData = PerfMonitor.Max;

        // 读完旧帧数据后立即重置，为下一帧做准备
        PerfMonitor.BeginFrame();

        if (data.Count == 0)
        {
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), "等待数据... (启动 ACR 后采集)");
            return;
        }

        var sorted = data.OrderByDescending(kv => kv.Value).ToList();

        if (ImGui.BeginTable("perfTable", 4,
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("模块", ImGuiTableColumnFlags.WidthStretch, 0.5f);
            ImGui.TableSetupColumn("耗时(μs)", ImGuiTableColumnFlags.WidthStretch, 0.2f);
            ImGui.TableSetupColumn("占比", ImGuiTableColumnFlags.WidthStretch, 0.15f);
            ImGui.TableSetupColumn("最大(μs)", ImGuiTableColumnFlags.WidthStretch, 0.15f);
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableHeadersRow();

            foreach (var kv in sorted)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(kv.Key);
                ImGui.TableNextColumn();
                ImGui.Text($"{kv.Value:F1}");
                ImGui.TableNextColumn();
                var pct = total > 0 ? kv.Value / total * 100 : 0;
                ImGui.Text($"{pct:F1}%");
                ImGui.TableNextColumn();
                maxData.TryGetValue(kv.Key, out var maxVal);
                ImGui.Text($"{maxVal:F1}");
            }

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Separator();
            ImGui.Text("总计");
            ImGui.TableNextColumn();
            ImGui.Text($"{total:F1}");
            ImGui.TableNextColumn();
            ImGui.Text("100%");
            ImGui.TableNextColumn();
            ImGui.Text("");

            ImGui.EndTable();
        }

        ImGui.Spacing();
        if (ImGui.Button("重置最大值"))
            PerfMonitor.ResetMax();
    }
}
#endif
