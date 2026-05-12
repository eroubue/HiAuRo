# ACR 自定义 ImGui 窗口 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 允许 ACR 作者创建不限数量的自定义 ImGui 窗口，同时增强 IUiBuilder 提供 hotkey 行布局控制。

**Architecture:** 新增 `ICustomWindow` 接口，`IRotationEntry` 通过 default method 暴露 `CustomWindows`；`UIManager` 负责创建/销毁 Dalamud Window 包装；`ACRLifecycle.LoadRotation` 加载时注册、`UnloadRotation` 时移除。`IUiBuilder.AddHotkeyRow` 生成 `hotkeyRow` 类型 `UiControlDef`，`ImGuiWidgetRenderer` 渲染。

**Tech Stack:** C# .NET 10, ImGui (Dalamud Window), default interface methods

---

### Task 1: 新增 ICustomWindow 接口 + IRotationEntry.CustomWindows

**Files:**
- Create: `HiAuRo/ACR/Interfaces/ICustomWindow.cs`
- Modify: `HiAuRo/ACR/Interfaces/IRotationEntry.cs:33`

- [ ] **Step 1: 创建 ICustomWindow.cs**

```csharp
using System.Numerics;

namespace HiAuRo.ACR;

/// <summary>
/// ACR 作者自定义 ImGui 窗口接口
/// </summary>
public interface ICustomWindow
{
    /// <summary>窗口唯一标识 & 标题</summary>
    string Name { get; }

    /// <summary>null = 自动大小</summary>
    Vector2? DefaultSize { get; }

    /// <summary>ACR 加载时是否自动打开</summary>
    bool IsOpenByDefault { get; }

    /// <summary>ACR 作者自由写 ImGui</summary>
    void Draw();
}
```

- [ ] **Step 2: IRotationEntry 添加 CustomWindows 默认方法**

在 `IRotationEntry.cs` 第33行 `TargetJobs` 之后（类闭合 `}` 之前）插入：

```csharp
    /// <summary>ACR 作者自定义 ImGui 窗口（不限数量），默认 null 表示无</summary>
    IEnumerable<ICustomWindow>? CustomWindows => null;
```

- [ ] **Step 3: 验证编译**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add HiAuRo/ACR/Interfaces/ICustomWindow.cs HiAuRo/ACR/Interfaces/IRotationEntry.cs
git commit -m "feat: add ICustomWindow interface and IRotationEntry.CustomWindows"
```

---

### Task 2: UIManager 添加自定义窗口管理

**Files:**
- Modify: `HiAuRo/UI/UIManager.cs`

- [ ] **Step 1: 添加字段和 using**

在文件头部检查是否有 `using HiAuRo.ACR;`，没有则添加。

在 `private OverlayHotkeyPanel? _overlayHotkeyPanel;`（第25行）之后添加：

```csharp
    private readonly List<Window> _customWindows = [];
```

- [ ] **Step 2: 添加 AddCustomWindow 方法**

在 `ShowDemoWindow()` 方法之后（第143行后）添加：

```csharp
    /// <summary>注册 ACR 自定义窗口</summary>
    public void AddCustomWindow(ICustomWindow cw)
    {
        var window = new CustomWindowHost(cw);
        _customWindows.Add(window);
        _windowSystem.AddWindow(window);
        if (cw.IsOpenByDefault && !_config.DisableCEF)
            window.IsOpen = true;
        DService.Instance().Log.Information($"[UIManager] 自定义窗口已添加: {cw.Name}");
    }

    /// <summary>移除所有 ACR 自定义窗口（ACR 卸载时调用）</summary>
    public void RemoveCustomWindows()
    {
        foreach (var w in _customWindows)
        {
            w.IsOpen = false;
            _windowSystem.RemoveWindow(w);
        }
        _customWindows.Clear();
        DService.Instance().Log.Information($"[UIManager] 自定义窗口已全部移除");
    }
```

- [ ] **Step 3: 创建 CustomWindowHost 内部类（同一文件末尾）**

在 `UIManager` 类结束的 `}` 之前（第154行前），添加：

```csharp
    /// <summary>
    /// ICustomWindow → Dalamud Window 适配器
    /// </summary>
    private sealed class CustomWindowHost : Window
    {
        private readonly ICustomWindow _cw;

        public CustomWindowHost(ICustomWindow cw) : base($"{cw.Name}##Custom")
        {
            _cw = cw;
            var sz = cw.DefaultSize;
            if (sz.HasValue)
                SizeConstraints = new WindowSizeConstraints
                {
                    MinimumSize = sz.Value,
                    MaximumSize = sz.Value
                };
            else
                SizeConstraints = new WindowSizeConstraints
                {
                    MinimumSize = new Vector2(100, 50),
                    MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
                };
            IsOpen = cw.IsOpenByDefault;
        }

        public override void Draw()
        {
            try
            {
                _cw.Draw();
            }
            catch (Exception ex)
            {
                ImGui.TextColored(new Vector4(1, 0.3f, 0.3f, 1), $"Draw error: {ex.Message}");
            }
        }
    }
```

- [ ] **Step 4: 验证编译**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add HiAuRo/UI/UIManager.cs
git commit -m "feat: add custom window management to UIManager"
```

---

### Task 3: ACRLifecycle 集成自定义窗口加载/卸载

**Files:**
- Modify: `HiAuRo/Runtime/ACRLifecycle.cs`

- [ ] **Step 1: 在 LoadRotation 中注册自定义窗口**

在 `LoadRotation` 方法的 UI 控件收集完成后（`IsLoadingRotation = false;` 之前，第325行前），插入：

```csharp
        // 注册 ACR 自定义 ImGui 窗口
        var customWindows = entry.CustomWindows;
        if (customWindows != null)
        {
            var uiMgr = Plugin.Instance._uiManager;
            if (uiMgr != null)
            {
                foreach (var cw in customWindows)
                    uiMgr.AddCustomWindow(cw);
                DService.Instance().Log.Information($"[ACR] 自定义窗口已加载: {customWindows.Count()}个");
            }
        }
```

- [ ] **Step 2: 在 UnloadRotation 中移除自定义窗口**

在 `UnloadRotation` 方法中，`Runner.Unload()` 之前（第334行前），插入：

```csharp
        Plugin.Instance._uiManager?.RemoveCustomWindows();
```

- [ ] **Step 3: 验证编译**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add HiAuRo/Runtime/ACRLifecycle.cs
git commit -m "feat: integrate custom window loading/unloading into ACRLifecycle"
```

---

### Task 4: IUiBuilder 增强 — AddHotkeyRow

**Files:**
- Modify: `HiAuRo/ACR/Interfaces/IUiBuilder.cs`
- Modify: `HiAuRo/UI/UiBuilderImpl.cs`
- Modify: `HiAuRo/UI/ImGui/ImGuiWidgetRenderer.cs`

- [ ] **Step 1: IUiBuilder 添加 AddHotkeyRow**

在 `IUiBuilder.cs` 第23行 `AddTooltip` 之后（`}` 之前）插入：

```csharp
    /// <summary>一行多个 hotkey（按传入 ID 顺序排列）</summary>
    void AddHotkeyRow(params string[] hotkeyIds);
```

- [ ] **Step 2: UiBuilderImpl 实现 AddHotkeyRow**

在 `UiBuilderImpl.cs` 第79行 `AddTooltip` 之后（`}` 之前）插入：

```csharp
    public void AddHotkeyRow(params string[] hotkeyIds) =>
        _controls.Add(new UiControlDef("__hkrow__", "hotkeyRow", _currentGroup, string.Empty, null,
            Options: hotkeyIds));
```

- [ ] **Step 3: ImGuiWidgetRenderer 添加 hotkeyRow 渲染**

在 `ImGuiWidgetRenderer.cs` 第69行 `case "sameLine":` 之后添加：

```csharp
                case "hotkeyRow":
                    RenderHotkeyRow(item);
                    break;
```

- [ ] **Step 4: 添加 RenderHotkeyRow 方法**

在 `RenderItems` 方法之后（第72行后）添加：

```csharp
    private static void RenderHotkeyRow(UiControlDef ctrl)
    {
        var ids = ctrl.Options switch
        {
            JsonElement el => el.EnumerateArray().Select(e => e.GetString() ?? "").ToArray(),
            string[] arr => arr,
            _ => Array.Empty<string>()
        };

        for (int i = 0; i < ids.Length; i++)
        {
            var allHotkeys = HiAuRo.ACR.HotkeyHelper.GetAll();
            var hk = allHotkeys.FirstOrDefault(h => h.Id == ids[i]);
            if (hk == null) continue;

            if (i > 0) ImGui.SameLine();
            var iconId = hk.IconId;
            var label = hk.Label;
            var available = hk.Check() >= 0;
            var binding = HiAuRo.ACR.HotkeyHelper.GetBinding(hk.Id);

            // 尝试加载游戏图标
            var tex = iconId > 0
                ? DService.Instance().TextureProvider.GetFromGameIcon(new Dalamud.Interface.Textures.GameIconLookup(iconId))
                : null;

            if (tex != null)
            {
                var wrap = tex.GetWrapOrEmpty();
                if (ImGui.ImageButton($"{label}###hkbtn-{hk.Id}", wrap.ImGuiHandle, new Vector2(36, 36)))
                    HiAuRo.ACR.HotkeyHelper.ExecuteById(hk.Id);
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text(label);
                    if (!string.IsNullOrEmpty(binding))
                    {
                        ImGui.SameLine();
                        ImGui.TextDisabled($"({binding})");
                    }
                    ImGui.EndTooltip();
                }
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button, available
                    ? Theme.Colors.AccentBlue
                    : new Vector4(0.3f, 0.3f, 0.3f, 1));
                if (ImGui.Button($"{label}###hkbtn-{hk.Id}"))
                    HiAuRo.ACR.HotkeyHelper.ExecuteById(hk.Id);
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text(label);
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
```

检查文件头部已有 `using System.Linq;`（需要 `.ToArray()` 和 `.Select()`）。

- [ ] **Step 5: 验证编译**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add HiAuRo/ACR/Interfaces/IUiBuilder.cs HiAuRo/UI/UiBuilderImpl.cs HiAuRo/UI/ImGui/ImGuiWidgetRenderer.cs
git commit -m "feat: add AddHotkeyRow to IUiBuilder with ImGui rendering"
```

---

### Verification

全部 Task 完成后：

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeded, 0 errors.

ACR 作者使用示例：
```csharp
public class MyACR : IRotationEntry
{
    public IEnumerable<ICustomWindow>? CustomWindows => [new MyHotkeyWindow()];

    public IRotationUI? GetRotationUI() => new MyRotationUI();

    // MyRotationUI.RegisterControls:
    // builder.AddHotkeyRow("hk1", "hk2", "hk3");
}
```
