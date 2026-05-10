# GCD 窗口能力技计数与上限设计

## 背景

AILoop_Normal 内部有 private `_abilityCount` 字段用于追踪当前 GCD 窗口中已使用的能力技数量，但：
- ACR 作者无法读取当前计数
- `maxAbility = 2` 硬编码，不读取 `PluginConfig.MaxAbilityTimesInGcd`
- 没有暴露给 ACR 作者修改上限的途径

## 设计

### Data.Combat 新增属性

```csharp
// HiAuRo/Data/Data.Combat.cs

/// <summary>当前 GCD 窗口已使用的能力技数量（ACR 只读）</summary>
public static int AbilityCountInGcd { get; internal set; }

/// <summary>当前 GCD 窗口能力技上限（ACR 可读写，框架仅在生命周期事件时重置）</summary>
public static int MaxAbilityTimesInGcd { get; set; } = 2;
```

### 生命周期：谁在什么时候修改

| 时机 | `AbilityCountInGcd` | `MaxAbilityTimesInGcd` | 谁 |
|------|-------------------|---------------------|---|
| 游戏初始化 | = 0 | = `PluginConfig` 默认 | `RuntimeCore` / `AIRunner` |
| 切 ACR | = 0 | = `PluginConfig` 默认 | `ACRLifecycle` |
| 战斗重置（OnResetBattle） | = 0 | = `PluginConfig` 默认 | `AIRunner` |
| 切地图（OnTerritoryChanged） | = 0 | = `PluginConfig` 默认 | `RuntimeCore` |
| Gcd 模式的 Slot 被 Build | = 0 | 不变 | `AILoop_Normal.GetNextSlot()` |
| OffGcd 能力技被 Build | ++ | 不变 | `AILoop_Normal.GetNextSlot()` |
| ACR 作者运行时调整 | 不变 | 任意值 | ACR 作者代码 |

### AILoop_Normal 改动

```csharp
// 删除
private int _abilityCount;

// 改动 gate:
// 旧: isOffGcdWindow && _abilityCount < maxAbility
// 新: isOffGcdWindow && Data.Combat.AbilityCountInGcd < Data.Combat.MaxAbilityTimesInGcd

// 改动计数:
// 旧: _abilityCount = 0;  /  _abilityCount++;
// 新: Data.Combat.AbilityCountInGcd = 0;  /  Data.Combat.AbilityCountInGcd++;

// 删除
var maxAbility = 2;
```

### ACR 作者使用方式

```csharp
// 读取当前计数
if (Data.Combat.AbilityCountInGcd == 0)
    // 第一个能力技位，插双能力技的第一插

// 动态调整上限（如爆发期允许 3 插）
if (爆发中)
    Data.Combat.MaxAbilityTimesInGcd = 3;
else
    Data.Combat.MaxAbilityTimesInGcd = PluginConfig.Instance.MaxAbilityTimesInGcd;

// Check() 中用计数做判断
public int Check()
{
    if (Data.Combat.AbilityCountInGcd >= 1) return -1; // 已用过能力技，跳过
    return 1;
}
```

### 不变的设计

- `PluginConfig.MaxAbilityTimesInGcd` 仍是用户设置的全局默认值（界面可改）
- `Rotation` 上不需要 `MaxAbilityTimesInGcd` 字段
- 框架不主动管理上限（只在生命周期事件时重置为 PluginConfig 默认值）
- `AbilityCountInGcd` 用 `internal set` 防止外部 ACR 程序集篡改

## 涉及文件

| 文件 | 改动 |
|------|------|
| `HiAuRo/Data/Data.Combat.cs` | 新增 `AbilityCountInGcd`（`internal set`）+ `MaxAbilityTimesInGcd` |
| `HiAuRo/Runtime/AILoop_Normal.cs` | 删 private `_abilityCount` + `maxAbility`；改为读写 `Data.Combat.*` |
| `HiAuRo/Runtime/ACRLifecycle.cs` | 切 ACR 时重置 `Data.Combat.*` |
| `HiAuRo/Runtime/AIRunner.cs` | 战斗重置时重置 `Data.Combat.*` |
| `HiAuRo/Runtime/RuntimeCore.cs` | 切图时重置 `Data.Combat.*` |
| `HiAuRo/Infrastructure/PluginConfig.cs` | 不改（已有 `MaxAbilityTimesInGcd`） |
