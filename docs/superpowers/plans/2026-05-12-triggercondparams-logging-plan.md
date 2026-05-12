# ITriggerCondParams 事件日志 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 GameEventHook 事件分发点为所有 ITriggerCondParams 实例添加日志，写入专用日志文件，并在 MainWindow 新增"日志"Tab 实时显示。

**Architecture:** 新增单例 LogManager 管理环形缓冲区+文件写入；在 GameEventHook 中将所有 `OnEventFired?.Invoke` 替换为统一的 `Fire()` 辅助方法内嵌日志；MainWindow 新增 Tab 读取 LogManager 缓冲展示。

**Tech Stack:** C# .NET 10, ImGui (Dalamud Window), ConcurrentQueue, StreamWriter

---

### Task 1: 创建 LogManager

**Files:**
- Create: `HiAuRo/Infrastructure/LogManager.cs`

- [ ] **Step 1: 写入 LogManager.cs 完整代码**

```csharp
using System.Collections.Concurrent;
using HiAuRo.ACR;

namespace HiAuRo.Infrastructure;

public record LogEntry(DateTime Timestamp, string Type, string Content);

public sealed class LogManager : IDisposable
{
    public static LogManager Instance { get; } = new();

    private const int MaxEntries = 5000;
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private StreamWriter? _writer;
    private string? _configDir;

    public void Init(string configDir)
    {
        _configDir = configDir;
        var logPath = Path.Combine(configDir, "hiauro_events.log");
        try
        {
            _writer = new StreamWriter(logPath, append: true)
            {
                AutoFlush = true
            };
            DService.Instance().Log.Information($"[LogManager] 日志文件: {logPath}");
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[LogManager] 无法创建日志文件: {ex.Message}");
        }
    }

    public void Log(ITriggerCondParams p)
    {
        var type = p.GetType().Name;
        var content = SerializeFields(p);
        var entry = new LogEntry(DateTime.Now, type, content);

        // 内存缓冲（环形）
        while (_entries.Count >= MaxEntries)
            _entries.TryDequeue(out _);
        _entries.Enqueue(entry);

        // 写文件
        try
        {
            _writer?.WriteLine($"{entry.Timestamp:HH:mm:ss.fff} | {entry.Type} | {entry.Content}");
        }
        catch
        {
            // 忽略写入错误，避免影响游戏帧
        }
    }

    public IReadOnlyList<LogEntry> GetEntries()
    {
        return _entries.ToArray();
    }

    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
    }

    public void Dispose()
    {
        _writer?.Flush();
        _writer?.Dispose();
        _writer = null;
    }

    /// <summary>
    /// 反射读取所有 public 字段，格式化为 KV 串
    /// </summary>
    private static string SerializeFields(ITriggerCondParams p)
    {
        var fields = p.GetType().GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        var parts = new List<string>(fields.Length);
        foreach (var f in fields)
        {
            var val = f.GetValue(p);
            var str = val switch
            {
                null => "null",
                string s => $"\"{s}\"",
                float fv => fv.ToString("F2"),
                _ => val.ToString()
            };
            parts.Add($"{f.Name}={str}");
        }
        return parts.Count > 0 ? string.Join(", ", parts) : "(empty)";
    }
}
```

- [ ] **Step 2: 验证编译**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```
Expected: Build succeeded. Error if any type/method missing.

- [ ] **Step 3: Commit**

```bash
git add HiAuRo/Infrastructure/LogManager.cs
git commit -m "feat: add LogManager for ITriggerCondParams event logging"
```

---

### Task 2: 在 Plugin.cs 中初始化/释放 LogManager

**Files:**
- Modify: `HiAuRo/Plugin.cs`

- [ ] **Step 1: 在构造函数中初始化 LogManager**

在 `Plugin.cs` 第44行（`_config = LoadConfig();`）之后，第45行之前插入：

```csharp
            LogManager.Instance.Init(_pluginInterface.ConfigDirectory.FullName);
```

注意：`HiAuRo.Infrastructure` 命名空间已在文件头 `using HiAuRo.Infrastructure;` 引入。

- [ ] **Step 2: 在 SafeDispose 中释放 LogManager**

在 `SafeDispose()` 方法末尾（第177行 `DService.Uninit();` 之前）插入：

```csharp
            LogManager.Instance.Dispose();
```

- [ ] **Step 3: 在 Dispose 中释放 LogManager**

在 `Dispose()` 方法末尾（第242行 `DService.Uninit();` 之前）插入：

```csharp
            LogManager.Instance.Dispose();
```

- [ ] **Step 4: 验证编译**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add HiAuRo/Plugin.cs
git commit -m "feat: init/dispose LogManager in Plugin lifecycle"
```

---

### Task 3: 在 GameEventHook 中集成日志

**Files:**
- Modify: `HiAuRo/Execution/Events/GameEventHook.cs`

核心思路：添加 `Fire()` 辅助方法，将全部 `OnEventFired?.Invoke(...)` 替换为 `Fire(...)`。

- [ ] **Step 1: 在 GameEventHook 类中添加 using 和 Fire 辅助方法**

在文件头添加 `using HiAuRo.Infrastructure;`（检查是否已存在）。

在 `GameEventHook` 类中添加方法（放在 `OnEventFired` 事件声明之后，约第15行后）：

```csharp
    private void Fire(ITriggerCondParams p)
    {
        LogManager.Instance.Log(p);
        OnEventFired?.Invoke(p);
    }

- [ ] **Step 2: 替换所有 OnEventFired?.Invoke 调用**

**方式：** 用 `Fire(` 替换文件中所有 `OnEventFired?.Invoke(` 。

共约 23 处替换（查找结果包括：BuffGainParams, BuffRemoveParams, TetherRemoveParams, TetherCreateParams, MapEffectParams, ActorCastParams, NpcYellParams, EnvControlParams, ChatMessageParams, ActorControlParams, ActorControlDeathParams, ActorControlTargetIconParams, ActorControlTargetableParams, ActorControlTimelineParams, UnitCreateParams, UnitDeleteParams, WeatherChangedParams, ActionEffectParams, NoTargetAbilityEffectParams）。

**重要：** 部分 Invoke 是 `OnEventFired?.Invoke(raw)` 传的局部变量，`Fire(raw)` 同样可以工作。

- [ ] **Step 3: 验证编译**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add HiAuRo/Execution/Events/GameEventHook.cs
git commit -m "feat: integrate LogManager into GameEventHook event dispatch"
```

---

### Task 4: 在 MainWindow 新增"日志"Tab

**Files:**
- Modify: `HiAuRo/UI/MainWindow.cs`

- [ ] **Step 1: 在 Draw 方法的 TabBar 中添加新 Tab**

在 `MainWindow.cs` 第59行 `录制` Tab 闭合之后（`ImGui.EndTabItem()` 之后，`ImGui.EndTabBar()` 之前），插入：

```csharp
            if (ImGui.BeginTabItem("日志"))
            {
                DrawLog();
                ImGui.EndTabItem();
            }
```

- [ ] **Step 2: 添加 DrawLog 方法**

在 `MainWindow` 类最后（第498行之前），添加：

```csharp
    private string _logFilter = "";

    private void DrawLog()
    {
        var entries = LogManager.Instance.GetEntries();
        var filtered = string.IsNullOrEmpty(_logFilter)
            ? entries
            : entries.Where(e => e.Type.Contains(_logFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        // 工具栏
        ImGui.Spacing();
        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##LogFilter", "筛选类型...", ref _logFilter, 64);
        ImGui.SameLine();
        ImGui.TextDisabled($"({filtered.Count}/{entries.Count})");
        ImGui.SameLine();

        if (ImGui.Button("清除"))
        {
            LogManager.Instance.Clear();
        }

        var showDebug = _config.DebugEnabled;
        ImGui.SameLine();
        if (ImGui.Checkbox("仅战斗中", ref showDebug))
        {
            // 预留：暂不实现战斗过滤
        }

        ImGui.Spacing();
        ImGui.Separator();

        // 表格
        if (ImGui.BeginTable("##LogTable", 3,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg |
            ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY,
            new Vector2(-1, -1)))
        {
            ImGui.TableSetupColumn("时间", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("类型", ImGuiTableColumnFlags.WidthFixed, 220);
            ImGui.TableSetupColumn("内容",
                ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            // 从最新到最旧显示
            for (int i = filtered.Count - 1; i >= 0 && i < filtered.Count; i--)
            {
                var e = filtered[i];
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                ImGui.Text(e.Timestamp.ToString("HH:mm:ss.fff"));

                ImGui.TableSetColumnIndex(1);
                if (ImGui.Selectable(e.Type))
                {
                    _logFilter = _logFilter == e.Type ? "" : e.Type;
                }

                ImGui.TableSetColumnIndex(2);
                ImGui.TextWrapped(e.Content);
            }

            ImGui.EndTable();
        }
    }
```

- [ ] **Step 3: 验证编译**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add HiAuRo/UI/MainWindow.cs
git commit -m "feat: add Log tab to MainWindow with real-time event viewer"
```

---

### Verification

全部 Task 完成后，最终验证：

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded with zero warnings. 所有 4 个文件变更一致，无编译错误。

运行后在游戏中可通过 `/hi` 打开主窗口，切换到"日志"Tab 查看实时事件日志。日志文件同步写入 `ConfigDirectory/hiauro_events.log`。
