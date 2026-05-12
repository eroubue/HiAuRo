# 内置通用 QT（Quick Toggle）

**日期**: 2026-05-12  
**状态**: 已确认

## 目标

为所有 ACR 提供 5 个职业通用的标准 QT：爆发、爆发药、停手、自动减伤、清空资源。ACR 通过 `IUiBuilder.AddBuiltinQt()` 显式注册，HiAuRo 自动绑定固定 ID 和标签。

## 关键决策

| 决策点 | 选用方案 |
|--------|---------|
| 注入方式 | ACR 手动调用 `AddBuiltinQt()` |
| QT 数量 | 5 个 |

## 组件设计

### 1. BuiltinQt 枚举（新文件 `ACR/Interfaces/BuiltinQt.cs`）

```csharp
namespace HiAuRo.ACR;

public enum BuiltinQt
{
    Burst,       // 爆发
    Potion,      // 爆发药
    Hold,        // 停手
    Mitigation,  // 自动减伤
    Dump,        // 清空资源
}
```

### 2. IUiBuilder 新增方法

```csharp
/// <summary>注册 HiAuRo 内置通用 QT</summary>
void AddBuiltinQt(BuiltinQt type);
```

### 3. 内置 QT 映射表

| 枚举 | 固定 ID | 标签 | 默认值 |
|------|---------|------|--------|
| Burst | `__builtin_burst` | 爆发 | false |
| Potion | `__builtin_potion` | 爆发药 | false |
| Hold | `__builtin_hold` | 停手 | false |
| Mitigation | `__builtin_mitigation` | 自动减伤 | true |
| Dump | `__builtin_dump` | 清空资源 | false |

### 4. UiBuilderImpl 实现

`AddBuiltinQt(type)` → 根据映射表获取 ID/Label/DefaultValue → 调用 `QTHelper.Register(id, label, default, ...)` → 生成 `UiControlDef(type: "qttoggle")`。重复调用相同 type 安全跳过。

## 文件变更

| 操作 | 文件 |
|------|------|
| 新增 | `ACR/Interfaces/BuiltinQt.cs` |
| 修改 | `ACR/Interfaces/IUiBuilder.cs` — 加 `AddBuiltinQt` |
| 修改 | `UI/UiBuilderImpl.cs` — 实现内置注册 |
