# 内置通用 QT — Implementation Plan

> **For agentic workers:** Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 为所有 ACR 提供 5 个职业通用的标准 Quick Toggle。

**Architecture:** 新增 `BuiltinQt` 枚举 + `IUiBuilder.AddBuiltinQt()` 方法，`UiBuilderImpl` 根据内置映射表自动注册到 `QTHelper`。

---

### Task 1: 创建 BuiltinQt 枚举 + IUiBuilder 方法 + UiBuilderImpl 实现

**Files:**
- Create: `HiAuRo/ACR/Interfaces/BuiltinQt.cs`
- Modify: `HiAuRo/ACR/Interfaces/IUiBuilder.cs`
- Modify: `HiAuRo/UI/UiBuilderImpl.cs`

- [ ] **Step 1: 创建 BuiltinQt.cs**

```csharp
namespace HiAuRo.ACR;

/// <summary>
/// HiAuRo 内置通用 QT 类型
/// </summary>
public enum BuiltinQt
{
    Burst,
    Potion,
    Hold,
    Mitigation,
    Dump,
}
```

- [ ] **Step 2: IUiBuilder 添加 AddBuiltinQt**

在 `IUiBuilder.cs` 的 `AddTooltip` 之后插入：

```csharp
    /// <summary>注册 HiAuRo 内置通用 QT</summary>
    void AddBuiltinQt(BuiltinQt type);
```

- [ ] **Step 3: UiBuilderImpl 实现 AddBuiltinQt + 内置映射表**

在 `UiBuilderImpl.cs` 中：

**3a.** 添加静态映射表（放在类顶部，`_controls` 字段之后）：

```csharp
    private static readonly Dictionary<BuiltinQt, (string Id, string Label, bool Default)> BuiltinQtMap = new()
    {
        [BuiltinQt.Burst] = ("__builtin_burst", "爆发", false),
        [BuiltinQt.Potion] = ("__builtin_potion", "爆发药", false),
        [BuiltinQt.Hold] = ("__builtin_hold", "停手", false),
        [BuiltinQt.Mitigation] = ("__builtin_mitigation", "自动减伤", true),
        [BuiltinQt.Dump] = ("__builtin_dump", "清空资源", false),
    };
```

**3b.** 实现方法（在 `AddTooltip` 之后）：

```csharp
    public void AddBuiltinQt(BuiltinQt type)
    {
        if (!BuiltinQtMap.TryGetValue(type, out var info)) return;
        // 同一个内置 QT 不重复注册
        if (_controls.Any(c => c.Id == info.Id)) return;
        QTHelper.Register(info.Id, info.Label, info.Default);
        _controls.Add(new UiControlDef(info.Id, "qttoggle", _currentGroup, info.Label, info.Default,
            Meta: new { defaultVisible = true }));
    }
```

- [ ] **Step 4: 验证编译**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

- [ ] **Step 5: Commit**

```bash
git add HiAuRo/ACR/Interfaces/BuiltinQt.cs HiAuRo/ACR/Interfaces/IUiBuilder.cs HiAuRo/UI/UiBuilderImpl.cs
git commit -m "feat: add built-in QTs (Burst/Potion/Hold/Mitigation/Dump) via IUiBuilder.AddBuiltinQt"
```
