# Browsingway 移除 Implementation Plan

> **For agentic workers:** Use subagent-driven-development or executing-plans to implement this plan task-by-task.

**Goal:** Remove all Browsingway CEF rendering code from HiAuRo, keep WebUiServer + frontend resources.

**Architecture:** Delete Browsingway/ dir + Plugin_Browsingway.cs, clean project refs, strip BrowserHost usage from UIManager/MainWindow/ACRLifecycle/Plugin, remove DisableCEF config. Keep UIMode enum for ImGui/WebUI switching (WebUI now only hides ImGui windows + future IPC).

**Tech Stack:** .NET 10, Dalamud, OmenTools (DService)

---

### Task 1: Delete directories & files

**Files:**
- Delete: `Browsingway/` (entire directory)
- Delete: `HiAuRo/Plugin_Browsingway.cs`

- [ ] **Delete Browsingway directory and Plugin_Browsingway.cs**

`rm -rf Browsingway/ HiAuRo/Plugin_Browsingway.cs`

---

### Task 2: Clean project configuration

**Files:**
- Modify: `HiAuRo.slnx`
- Modify: `HiAuRo/HiAuRo.csproj`
- Modify: `HiAuRo/packages.lock.json`

- [ ] **Remove Browsingway folder from .slnx**

Edit `HiAuRo.slnx` — delete lines 2-12 (`<Folder Name="/Browsingway/">...` block).

- [ ] **Remove Browsingway ProjectReferences from .csproj**

Edit `HiAuRo/HiAuRo.csproj` — delete lines 17-21 (3 ProjectReference items).

- [ ] **Remove CopyRendererOutput build target from .csproj**

Edit `HiAuRo/HiAuRo.csproj` — delete lines 45-63 (entire `CopyRendererOutput` target).

- [ ] **Remove Browsingway from packages.lock.json**

Edit `HiAuRo/packages.lock.json` — delete lines referencing `browsingway` and `browsingway.common`.

---

### Task 3: Clean PluginConfig.cs

**Files:**
- Modify: `HiAuRo/Infrastructure/PluginConfig.cs`

- [ ] **Remove DisableCEF field**

Edit `PluginConfig.cs` — delete line 43 (`public bool DisableCEF { get; set; } = false;`).

---

### Task 4: Clean UIManager.cs

**Files:**
- Modify: `HiAuRo/UI/UIManager.cs`

- [ ] **Remove Browsingway import, simplify IsWebUI, remove BrowserHost logic**

Changes:
1. Line 2: Delete `using Browsingway;`
2. Line 32: `IsWebUI` → `public bool IsWebUI => _config.UIMode == UIMode.WebUI;`
3. Lines 52-76: Remove the entire `if (!_config.DisableCEF)` block. Move `_uiBridge = new WebUiBridge(); _uiServer = new WebUiServer(...); _uiServer.Start();` outside the block (always create).
4. Line 90: Remove `if (mode == UIMode.WebUI && _config.DisableCEF) return;`
5. Lines 94-105: Simplify SwitchTo — remove BrowserHost.OverlaysVisible calls. WebUI → RemoveImGuiOverlays(), ImGui → CreateImGuiOverlays().
6. Lines 177-179: Remove `Plugin.Instance._browserHost?.Dispose(); Plugin.Instance._browserHost = null;`

---

### Task 5: Clean Plugin.cs

**Files:**
- Modify: `HiAuRo/Plugin.cs`

- [ ] **Replace Browsingway services, remove _browserHost references**

Changes:
1. Line 285: `Browsingway.Services.Framework.RunOnFrameworkThread(...)` → `DService.Instance().Framework.RunOnFrameworkThread(...)`
2. Line 351: Remove `Instance._browserHost?.UpdateOverlay(...)` call
3. Line 126: Remove `悬浮窗: localhost:5678/jobview.html` from startup message

---

### Task 6: Clean MainWindow.cs

**Files:**
- Modify: `HiAuRo/UI/MainWindow.cs`

- [ ] **Simplify DrawStatus — remove DisableCEF UI**

Replace lines 72-111 with simplified radio buttons (no DisableCEF):

```csharp
// UI 渲染模式切换
ImGui.TextColored(Theme.Colors.AccentBlue, "UI 渲染模式:");
ImGui.SameLine();

var isWebUI = _config.UIMode == Infrastructure.UIMode.WebUI;
if (ImGui.RadioButton("WebUI", isWebUI))
    Plugin.Instance._uiManager?.SwitchTo(Infrastructure.UIMode.WebUI);
ImGui.SameLine();
if (ImGui.RadioButton("ImGui", !isWebUI))
    Plugin.Instance._uiManager?.SwitchTo(Infrastructure.UIMode.ImGui);
```

- [ ] **Clean DrawOverlaySettings — remove BrowserHost calls + DevTools**

Changes in `DrawOverlaySettings()`:
1. Line 369: Delete `var host = Plugin.BrowserHost;`
2. Lines 390, 399, 408, 417, 425: Delete each `host?.UpdateOverlay(...)` call
3. Line 362: Change `CEF 游戏内悬浮窗` → `外部悬浮窗`
4. Lines 430-431: Delete CEF DevTools button block

---

### Task 7: Clean ACRLifecycle.cs

**Files:**
- Modify: `HiAuRo/Runtime/ACRLifecycle.cs`

- [ ] **Remove BrowserHost overlay resize restore**

Edit lines 316-325 — remove the `Plugin.BrowserHost?.UpdateOverlay(...)` foreach loop. Keep the `if (settings.OverlayContentWidth.Count > 0)` check and the brace block (remove inner content).

---

### Task 8: Build & verify

- [ ] **Run dotnet build**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: Build succeeds with no Browsingway-related errors.
