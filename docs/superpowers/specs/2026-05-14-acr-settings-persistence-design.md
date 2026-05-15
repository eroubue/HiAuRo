# ACR Settings 持久化托管设计

## 概述

让 ACR 作者的 Settings 对象被宿主自动加载/保存，消除手动订阅 `MainControlHelper.OnSave` 和手写 `SettingMgr` 调用的样板代码。

## 核心接口

### ISettingsProvider<T> — 标记接口

```csharp
// HiAuRo/ACR/Interfaces/ISettingsProvider.cs
public interface ISettingsProvider<T> where T : class, new()
{
    T Settings { get; set; }
}
```

ACR 作者在 `IRotationEntry` 实现类上同时实现此接口，宿主通过反射检测并自动托管。

### AcrSettings — 基类（提供 .Save()）

```csharp
// HiAuRo/ACR/AcrSettings.cs
public abstract class AcrSettings
{
    internal string? _author;
    internal uint _jobId;

    public void Save()
    {
        if (_author == null) return;
        SettingMgr.SaveAcrJobSetting(_author, _jobId, this);
    }
}
```

ACR 作者的 Settings 类继承 `AcrSettings`，获得 `.Save()` 实例方法。`_author` / `_jobId` 由宿主在加载时回填。

## 宿主行为

| 时机 | 行为 | 实现位置 |
|------|------|---------|
| ACR 加载（Job 切换） | 反射检测 `ISettingsProvider<T>` → `SettingMgr.GetAcrJobSetting<T>` 加载 → 注入 Settings 属性 → 回填 `_author` / `_jobId` | `ACRLifecycle.LoadRotation` |
| 用户点击保存按钮 | 遍历已缓存的 provider → 调 `AcrSettings.Save()` | `ACRLifecycle.Init` 中订阅 `MainControlHelper.OnSave` |
| ACR 作者调 `_settings.Save()` | 立即写盘 | 基类 `AcrSettings.Save()` |
| ACR 卸载 | 从 provider 缓存移除 | `ACRLifecycle.ClearCurrent` |

## ACR 作者示例

```csharp
public class BRDSettings : AcrSettings
{
    public bool UseSongs { get; set; } = true;
    public int PreferredSong { get; set; } = 1;
}

public class BRDRotationEntry : IRotationEntry, ISettingsProvider<BRDSettings>
{
    public string AuthorName => "MyName";
    public IEnumerable<Jobs> TargetJobs => [Jobs.BRD];

    public BRDSettings Settings { get; set; } = new();

    public Rotation? Build(string settingFolder)
    {
        // Settings 已由宿主自动加载并注入，直接使用
        if (Settings.UseSongs) { ... }
        return ...;
    }

    // 运行时修改并保存
    void SomeMethod()
    {
        Settings.PreferredSong = 0;
        Settings.Save();
    }
}
```

## 实现细节

### 反射检测（LoadRotation 中，Build() 之前执行）

```csharp
var providerInterface = entry.GetType()
    .GetInterfaces()
    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISettingsProvider<>));
if (providerInterface != null)
{
    var tType = providerInterface.GetGenericArguments()[0];
    var load = typeof(SettingMgr).GetMethod(nameof(SettingMgr.GetAcrJobSetting))!
        .MakeGenericMethod(tType);
    var settings = load.Invoke(null, [author, jobId]);
    providerInterface.GetProperty("Settings")!.SetValue(entry, settings);
    if (settings is AcrSettings acrSettings)
    {
        acrSettings._author = author;
        acrSettings._jobId = jobId;
    }
    _settingsCache[key] = (entry, tType);  // 缓存供显式 save 用
}
```

### 显式保存（宿主订阅 MainControlHelper.OnSave）

```csharp
MainControlHelper.OnSave += () =>
{
    foreach (var (entry, tType) in _settingsCache.Values)
    {
        var settings = entry.GetType()
            .GetInterface(typeof(ISettingsProvider<>).Name)!
            .GetProperty("Settings")!.GetValue(entry);
        if (settings is AcrSettings acr)
            acr.Save();
    }
};
```

### 性能

- 反射仅在 Job 切换时执行一次，不在 Tick 热路径
- Settings JSON 文件通常几 KB，`SaveAcrJobSetting` 同步 I/O 可接受
- 无新增 GC 压力（无订阅委托泄漏，全由宿主静态管理）

## 文件清单

| 文件 | 动作 |
|------|------|
| `HiAuRo/ACR/Interfaces/ISettingsProvider.cs` | 新增 |
| `HiAuRo/ACR/AcrSettings.cs` | 新增 |
| `HiAuRo/ACR/Interfaces/IRotationEntry.cs` | 改：注释补 `ISettingsProvider<T>` 说明 |
| `HiAuRo/Runtime/ACRLifecycle.cs` | 改：LoadRotation 注入 + Init 订阅 OnSave |
