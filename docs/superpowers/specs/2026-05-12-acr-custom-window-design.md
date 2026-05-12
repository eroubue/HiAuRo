# ACR 自定义 ImGui 窗口

**日期**: 2026-05-12  
**状态**: 已确认

## 目标

允许 ACR 作者创建不限数量的自定义 ImGui 窗口（主要用于自定义快捷键按钮面板），同时增强 IUiBuilder 提供 hotkey 布局控制。

## 关键决策

| 决策点 | 选用方案 |
|--------|---------|
| 自定义方式 | ImGui Draw 回调 + IUiBuilder 增强，两种都支持 |
| 与内置窗口关系 | 作为额外窗口，不替代内置 HotkeyPanel |
| 窗口数量 | 不限 |

## 组件设计

### 1. ICustomWindow 接口（新文件 `ACR/Interfaces/ICustomWindow.cs`）

```csharp
public interface ICustomWindow
{
    string Name { get; }           // 窗口唯一标识 & 标题
    Vector2? DefaultSize { get; }  // null = 自动大小
    bool IsOpenByDefault { get; }  // ACR 加载时自动打开
    void Draw();                   // ACR 作者自由写 ImGui
}
```

### 2. IRotationEntry 新增默认方法

```csharp
/// <summary>ACR 作者自定义 ImGui 窗口（不限数量），默认 null 表示无</summary>
IEnumerable<ICustomWindow>? CustomWindows => null;
```

使用 default interface method，不破坏现有 ACR。

### 3. HiAuRo 端窗口管理（ACRLifecycle + UIManager）

ACRLifecycle.LoadRotation() 中:
- 遍历 `entry.CustomWindows`，为每个创建 `Dalamud.Window` 包装
- 窗口注册到 `UIManager.WindowSystem`
- 窗口关闭/ACR 卸载时自动移除

ACRLifecycle.UnloadRotation() 中:
- 关闭并移除当前 ACR 的所有自定义窗口

### 4. IUiBuilder 增强 — AddHotkeyRow

```csharp
/// <summary>一行多个 hotkey（按传入顺序排列）</summary>
void AddHotkeyRow(params string[] hotkeyIds);
```

UiBuilderImpl 实现：生成一个 `UiControlDef(type: "hotkeyRow", options: hotkeyIds)`。

ImGuiWidgetRenderer 渲染：`ImGui.SameLine()` 串联多个 hotkey 按钮。

## 数据流

```
ACR Author
  │  IRotationEntry.CustomWindows → IEnumerable<ICustomWindow>
  │  IRotationUI.RegisterControls(builder)
  │    → builder.AddHotkeyRow("a", "b", "c")
  ▼
ACRLifecycle.LoadRotation()
  │
  ├──→ UIManager.AddCustomWindow(ICustomWindow)
  │      → 创建 Window, 注册到 WindowSystem
  │
  └──→ UiBuilderImpl.AddHotkeyRow(...)
         → UiControlDef(type: "hotkeyRow")
```

## 文件变更

| 操作 | 文件 | 说明 |
|------|------|------|
| 新增 | `ACR/Interfaces/ICustomWindow.cs` | 自定义窗口接口 |
| 修改 | `ACR/Interfaces/IRotationEntry.cs` | 加 `CustomWindows` 默认方法 |
| 修改 | `ACR/Interfaces/IUiBuilder.cs` | 加 `AddHotkeyRow` |
| 修改 | `UI/UiBuilderImpl.cs` | 实现 `AddHotkeyRow` |
| 修改 | `UI/ImGui/ImGuiWidgetRenderer.cs` | 渲染 `hotkeyRow` |
| 修改 | `Runtime/ACRLifecycle.cs` | 加载/卸载自定义窗口 |
| 修改 | `UI/UIManager.cs` | 暴露 WindowSystem + AddCustomWindow |
