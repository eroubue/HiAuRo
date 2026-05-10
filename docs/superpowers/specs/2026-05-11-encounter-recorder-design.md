# 副本事件记录器设计规格

## 概述

为 HiAuRo 增加副本事件录制子系统。玩家进入副本后自动录制战斗事件，保存为 EncounterRecord JSON 文件，事实轴编辑器可从记录中选择事件导入到时间线。

## 架构

```
C# (Dalamud Plugin)                    Web (纯前端)
┌──────────────────────┐     JSON      ┌──────────────────────────┐
│  EncounterRecorder   │ ←───文件───→ │  FactEditor (增强)       │
│  ├ Init/Shutdown      │   共享介质   │  ├ "从录制导入"按钮       │
│  ├ 自动录制生命周期    │              │  ├ 分类过滤面板           │
│  ├ CombatClock        │              │  ├ 事件预览列表           │
│  ├ 事件序列化          │              │  ├ 勾选+导入到时间线      │
│  └ 保存到文件          │              │  └ (RecordConverter 在JS)│
│                        │              │                           │
│  RecordImGui          │              │                           │
│  ├ 录制状态指示        │              │                           │
│  └ 录制历史列表        │              │                           │
└──────────────────────┘              └──────────────────────────┘
```

**关键设计决策：** C# 和前端通过 JSON 文件共享数据，不经过 WebSocket。编辑器是纯本地的 File System Access API 应用，不需要运行在插件 Web UI 中也可以使用。

## C# 端

### 文件清单

- `HiAuRo/Recording/EncounterRecorder.cs` — 主控制器，生命周期/状态机/事件订阅
- `HiAuRo/Recording/EncounterRecord.cs` — C# 数据模型

- `HiAuRo/Recording/RecordImGui.cs` — MainWindow 录制面板

转换逻辑（EncounterEvent → FactEvent）在 JS 前端实现。

### 数据模型 — EncounterRecord

```csharp
public sealed class EncounterRecord
{
    public int Version { get; set; } = 1;
    public uint TerritoryId { get; set; }
    public string TerritoryName { get; set; } = "";
    public string RecordedAt { get; set; } = "";       // ISO 8601
    public long TotalTimeMs { get; set; }
    public int PartySize { get; set; }
    public List<string> JobComposition { get; set; } = [];
    public List<BossInfo> Bosses { get; set; } = [];
    public List<EncounterEvent> Events { get; set; } = [];
}

public sealed class BossInfo
{
    public uint NpcId { get; set; }
    public string Name { get; set; } = "";
    public uint HpMax { get; set; }
}

public sealed class EncounterEvent
{
    public long TimeMs { get; set; }                   // 战斗时间（毫秒），每次重置战斗归零
    public string Type { get; set; } = "";              // ITriggerCondParams 类名
    public string Category { get; set; } = "";          // 分类标签，用于编辑器过滤
    public Dictionary<string, object> Data { get; set; } = [];
}
```

### 类别分类

| category | 包含的 ITriggerCondParams 类型 |
|----------|-------------------------------|
| `cast` | ActorCastParams（非玩家/宠物来源）, AfterSpellParams |
| `ability` | ActionEffectParams, NoTargetAbilityEffectParams |
| `buff` | BuffGainParams, BuffRemoveParams |
| `tether` | TetherCreateParams, TetherRemoveParams |
| `spawn` | UnitCreateParams, UnitDeleteParams |
| `death` | ActorControlDeathParams |
| `target` | ActorControlTargetIconParams, ActorControlTargetableParams |
| `combat` | CombatStateParams, ActorControlCombatParams |
| `environment` | MapEffectParams, EnvControlParams, WeatherChangedParams |
| `npc` | NpcYellParams |
| `director` | DirectorUpdateParams, ActorControlTimelineParams |
| `actorControl` | ActorControlParams（未被其他分类覆盖的 command） |
| `chat` | ChatMessageParams |

### 录制生命周期

```
Plugin.Init → EncounterRecorder.Init()
  → 订阅 GameEventHook.OnEventFired
  → 订阅 CombatContext.CombatStateChanged
  → 订阅 OmenService.GameState.TerritoryType 变化

进入副本 → 状态 = Ready

进战 → CombatClock.Reset() → Events = [] → 状态 = Recording
  每帧：记录 OnEventFired 的所有事件 + CombatClock.Now
  → EncounterEvent { TimeMs, Type, Category, Data }

脱战 → CombatClock.Pause() → 保存到文件 → CombatClock.Reset()
  → 状态 = Ready

离开副本 → 无操作
```

### CombatClock

```csharp
public sealed class CombatClock
{
    private Stopwatch _sw = new();
    private long _baseMs;

    public void Reset() { _baseMs = 0; _sw.Restart(); }
    public void Pause() { _baseMs += _sw.ElapsedMilliseconds; _sw.Stop(); }
    public void Resume() { _sw.Start(); }
    public long Now => _baseMs + _sw.ElapsedMilliseconds;
}
```

### 事件序列化策略

每个 ITriggerCondParams 类型注册一个手写轻量序列化器，将字段写入 `Data` 字典。不依赖反射，性能好。

```csharp
_serializers[typeof(ActorCastParams)] = (p, timeMs) => {
    var cp = (ActorCastParams)p;
    return new EncounterEvent {
        TimeMs = timeMs,
        Type = "ActorCastParams",
        Category = "cast",
        Data = new() {
            ["actionId"] = cp.ActionID,
            ["castTime"] = cp.CastTime,
            ["sourceId"] = cp.SourceID,
            ["targetId"] = cp.TargetID,
            ["posX"] = cp.PosX,
            ["posY"] = cp.PosY,
            ["posZ"] = cp.PosZ,
        }
    };
};
```

### 保存文件名

`{中文副本名}_{YYYYMMDD_HHmmss}.json`

中文副本名通过 lumina `TerritoryType` → `PlaceName` 获取。保存在 `ConfigDirectory/Recordings/` 目录下。

### RecordImGui 面板

MainWindow 中新增"录制" Tab：
- 当前状态：Ready / Recording (mm:ss) / Just Saved
- 录制中时显示当前文件名
- 录制历史列表（按时间倒序）
- "打开录制目录" 按钮
- 点击历史项可复制文件路径

## Web 前端 — 事实轴编辑器适配

### 修改范围

- `fact-editor.html` — 工具栏新增"从录制导入"按钮，新增侧边栏区域
- `fact-editor.js` — 新增 EncounterRecord 加载、过滤、预览、导入逻辑

### 用户操作流程

1. 在事实轴编辑器中点击"从录制导入"
2. 系统弹出文件选择对话框，用户选择 .json 文件
3. 编辑器解析 EncounterRecord JSON，在右侧显示过滤面板
4. 过滤面板：13 个 category 勾选框，默认全选
5. 过滤后的事件预览列表（按 timeMs 排序）
6. 每行显示：`时间 | [分类] 事件名 | 来源名 | ActionID`
7. 用户勾选需要的事件
8. 点击"导入到当前阶段" → RecordConverter 批量转换为 FactEvent 并插入
9. 或点击"导入为新阶段" → 自动创建 FactPhase 并插入事件

### EncounterEvent → FactEvent 转换逻辑（前端实现）

```javascript
function convertToFactEvent(encEvent, phaseStartTimeMs) {
    var timeSec = (encEvent.timeMs - phaseStartTimeMs) / 1000;
    var actionId = encEvent.Data.actionId || encEvent.Data.abilityId || 0;
    var fe = {
        id: 'imported_' + counter++,
        name: '[' + encEvent.category + '] ' + (encEvent.Data.actionName || actionId),
        time: timeSec,
        actions: []
    };
    if (actionId > 0) {
        fe.startSync = { type: 'ability', abilityIds: [actionId] };
    }
    return fe;
}
```

## 非功能性需求

- 录制文件存储在 `{ConfigDirectory}/Recordings/`，不随插件卸载删除
- 录制不阻塞游戏线程 — 事件处理在 GameEventHook 已有的异步路径上
- 单个录制文件上限由战斗时长自然决定（1 小时约 1-2MB）
- 不支持的 ITriggerCondParams 类型静默跳过，不影响录制

## 实现顺序

1. `EncounterRecord.cs` — C# 数据模型
2. `EncounterRecorder.cs` — 主控制器 + CombatClock + 序列化器
3. `RecordImGui.cs` — MainWindow 录制面板
4. 前端 `fact-editor.js` 增强 — 加载/过滤/转换/导入
5. 前端 `fact-editor.html` — 按钮 + 侧边栏 UI
