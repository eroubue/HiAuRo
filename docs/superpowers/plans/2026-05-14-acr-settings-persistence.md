# ACR Settings 持久化托管 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让 ACR 作者通过实现 `ISettingsProvider<T>` + 继承 `AcrSettings` 获得宿主全托管的 settings 加载/保存，消除手动订阅样板代码。

**Architecture:** 新增两个类型（接口 + 基类），修改 ACRLifecycle 在 LoadRotation 中反射检测接口、自动调用 SettingMgr 加载/注入，并订阅 MainControlHelper.OnSave 自动写回。

**Tech Stack:** C# (System.Text.Json for SettingMgr), reflection (Type.GetInterface), no external deps

---

### Task 1: 新增 ISettingsProvider<T> 接口

**Files:**
- Create: `HiAuRo/ACR/Interfaces/ISettingsProvider.cs`

- [ ] **Step 1: 写入接口文件**

```csharp
namespace HiAuRo.ACR;

/// <summary>
/// 实现此接口后，宿主自动托管 Settings 的加载与显式保存
/// ACR 作者在 IRotationEntry 实现类上同时实现此接口即可
/// </summary>
public interface ISettingsProvider<T> where T : class, new()
{
    /// <summary>宿主自动注入已加载的 settings 对象</summary>
    T Settings { get; set; }
}
```

- [ ] **Step 2: 验证 build 通过**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```
Expected: 编译成功，无新增错误。

- [ ] **Step 3: Commit**

```bash
git add HiAuRo/ACR/Interfaces/ISettingsProvider.cs
git commit -m "feat: add ISettingsProvider<T> interface for ACR settings hosting"
```

---

### Task 2: 新增 AcrSettings 基类

**Files:**
- Create: `HiAuRo/ACR/AcrSettings.cs`

- [ ] **Step 1: 写入基类文件**

```csharp
using HiAuRo.Setting;

namespace HiAuRo.ACR;

/// <summary>
/// ACR 作者继承此类获得 .Save() 方法，宿主自动回填 _author / _jobId
/// </summary>
public abstract class AcrSettings
{
    internal string? _author;
    internal uint _jobId;

    /// <summary>立即将当前 settings 写入磁盘</summary>
    public void Save()
    {
        if (_author == null) return;
        SettingMgr.SaveAcrJobSetting(_author, _jobId, this);
    }
}
```

- [ ] **Step 2: 验证 build 通过**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```
Expected: 编译成功。

- [ ] **Step 3: Commit**

```bash
git add HiAuRo/ACR/AcrSettings.cs
git commit -m "feat: add AcrSettings base class with .Save() for ACR authors"
```

---

### Task 3: 更新 IRotationEntry 注释

**Files:**
- Modify: `HiAuRo/ACR/Interfaces/IRotationEntry.cs`

- [ ] **Step 1: 在接口注释中补充 ISettingsProvider 说明**

修改第5行的 summary comment，追加 ISettingsProvider 引导：

Old (line 3-5):
```csharp
/// <summary>
/// ACR 作者入口接口 —— 两种 UI 模式可选
/// </summary>
```

New:
```csharp
/// <summary>
/// ACR 作者入口接口 —— 两种 UI 模式可选。
/// 如需宿主托管 Settings 持久化，同时实现 ISettingsProvider&lt;T&gt; 并让 Settings 类继承 AcrSettings。
/// </summary>
```

- [ ] **Step 2: 验证 build 通过**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```
Expected: 编译成功。

- [ ] **Step 3: Commit**

```bash
git add HiAuRo/ACR/Interfaces/IRotationEntry.cs
git commit -m "docs: add ISettingsProvider<T> usage hint to IRotationEntry comment"
```

---

### Task 4: ACRLifecycle 反射检测 + 自动托管

**Files:**
- Modify: `HiAuRo/Runtime/ACRLifecycle.cs`

本次改动分三个部分：字段缓存、LoadRotation 注入、UnloadRotation 清理。

---

- [ ] **Step 1: 添加缓存字段**

在第18行 `CurrentJobId` 之后插入：

```csharp
/// <summary>ISettingsProvider 缓存（供显式 save 遍历）</summary>
private static readonly Dictionary<string, (IRotationEntry Entry, Type SettingsType)> _settingsProviders = [];
```

代码位置：`ACRLifecycle.cs:18` 之后。

---

- [ ] **Step 2: 在 LoadRotation 中注入 settings（Build 之前）**

在第179行（`CurrentJobId = _lastJob;` 之后）和第180行（`Runner.Load(entry, settingFolder);` 之前）之间插入注入代码：

```csharp
        CurrentJobId = _lastJob;

        // === 新增：自动检测 ISettingsProvider<T> 并注入 ===
        var providerInterface = entry.GetType()
            .GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISettingsProvider<>));
        if (providerInterface != null)
        {
            var tType = providerInterface.GetGenericArguments()[0];
            var loadMethod = typeof(HiAuRo.Setting.SettingMgr).GetMethod(nameof(HiAuRo.Setting.SettingMgr.GetAcrJobSetting))!
                .MakeGenericMethod(tType);
            var settings = loadMethod.Invoke(null, [entry.AuthorName, CurrentJobId]);
            providerInterface.GetProperty("Settings")!.SetValue(entry, settings);
            if (settings is AcrSettings acr)
            {
                acr._author = entry.AuthorName;
                acr._jobId = CurrentJobId;
            }
            _settingsProviders[GetProviderKey(entry.AuthorName, CurrentJobId)] = (entry, tType);
            DService.Instance().Log.Information($"[ACR] ISettingsProvider<{tType.Name}> 已注入: author={entry.AuthorName} jobId={CurrentJobId}");
        }
        // === 新增结束 ===

        DService.Instance().Log.Information($"[ACR] LoadRotation 开始: author={entry.AuthorName}, jobId={CurrentJobId}, settingFolder={settingFolder}");
        Runner.Load(entry, settingFolder);
```

需要同步调整：将 `_lastJob` 在 `CheckJobSwitch` 中传入 `LoadRotation` 时已被 `CurrentJob` 赋值，但 `LoadRotation` 内部在第178行读了 `_lastJob`。看一下当前代码：

```csharp
// CheckJobSwitch 第91-99行:
var currentJob = Data.Me.ClassJob;
if (currentJob == _lastJob && currentJob != 0) return;
_lastJob = currentJob;
...
LoadRotation(reg.Factory(), reg.SettingDir);

// LoadRotation 第178行:
CurrentJobId = _lastJob;
```

所以 `CurrentJobId` 来自 `_lastJob`，是 `Data.Me.ClassJob`（uint）。`entry.AuthorName` 是 string。这两个就是 `SaveAcrJobSetting` 的参数。

---

- [ ] **Step 3: 订阅 MainControlHelper.OnSave（LoadRotation 末尾）**

在 `LoadRotation` 方法末尾（第329行 `IsLoadingRotation = false;` 之前）插入：

```csharp
        // === 新增：宿主订阅保存事件 ===
        ACR.MainControlHelper.OnSave += HostSaveAllSettings;
        // === 新增结束 ===

        IsLoadingRotation = false;
```

---

- [ ] **Step 4: 在 UnloadRotation 中退订事件 + 清缓存**

在 `UnloadRotation` 方法开头（第334行附近），`MainControlHelper.Reset()` 之后插入缓存清理：

`MainControlHelper.Reset()` 当前在 `UnloadRotation` 末尾第345行被调用。但 `Reset()` 会将 `OnSave = null` 也清掉我们的订阅。我们需要：

a) 在 `Reset()` 之前先退订
b) 清空 `_settingsProviders`

修改 `UnloadRotation` 末尾（第340-346行附近）：

Old:
```csharp
        Runner.Unload();
        CurrentEntry = null;
        CurrentJobId = 0;
        ACR.HotkeyHelper.Clear();
        ACR.QTHelper.Clear();
        ACR.MainControlHelper.Reset();
    }
```

New:
```csharp
        Runner.Unload();
        CurrentEntry = null;
        CurrentJobId = 0;
        ACR.HotkeyHelper.Clear();
        ACR.QTHelper.Clear();
        ACR.MainControlHelper.OnSave -= HostSaveAllSettings;
        _settingsProviders.Clear();
        ACR.MainControlHelper.Reset();
    }
```

注意：`Reset()` 会把 `OnSave` 设为 null，但我们在它之前已经退订并清缓存，顺序正确。

---

- [ ] **Step 5: 添加辅助方法**

在 `ACRLifecycle` 类的末尾（第378行之后、闭合大括号之前）插入两个辅助方法：

```csharp
    /// <summary>宿主 save handler —— 通知所有已加载 ISettingsProvider 保存</summary>
    private static void HostSaveAllSettings()
    {
        foreach (var (entry, _) in _settingsProviders.Values)
        {
            var providerInterface = entry.GetType()
                .GetInterfaces()
                .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISettingsProvider<>));
            var settings = providerInterface.GetProperty("Settings")!.GetValue(entry);
            if (settings is AcrSettings acr)
                acr.Save();
        }
    }

    private static string GetProviderKey(string author, uint jobId) => $"{author}_{jobId}";
```

注意：`HostSaveAllSettings` 中的反射调用只在用户点击保存按钮时执行（不在热路径），性能可接受。

---

- [ ] **Step 6: 验证 build 通过**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```
Expected: 编译成功，0 errors。

- [ ] **Step 7: 代码审阅 —— 确认 LoadRotation 插入位置正确**

确认 `CurrentJobId`（`LoadRotation:178`）在注入代码之前已完成赋值，且 `Runner.Load()` 在注入代码之后调用，确保 `Build()` 内 Settings 已就绪。

- [ ] **Step 8: Commit**

```bash
git add HiAuRo/Runtime/ACRLifecycle.cs
git commit -m "feat: auto-detect ISettingsProvider<T>, inject settings before Build, host auto-save"
```

---

### Task 5: 最终验证

- [ ] **Step 1: 完整 build**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```
Expected: Build succeeded. 0 Error(s).

- [ ] **Step 2: 检查新增文件完整性**

```bash
ls -la HiAuRo/ACR/Interfaces/ISettingsProvider.cs HiAuRo/ACR/AcrSettings.cs
```
Expected: 两个文件都存在。

- [ ] **Step 3: 代码审阅**

审查 `ACRLifecycle.cs` 中 `UnloadRotation` 的退订顺序确认：`OnSave -= HostSaveAllSettings` → `_settingsProviders.Clear()` → `Reset()` 顺序无误。
