# HiAuRo 事实轴编写详解

> 事实轴（FactAxis）是 HiAuRo 的副本时间线系统——声明 Boss 在什么时间做什么事，驱动 ACR 的策略切换、减伤分配、技能建议等。

---

## 目录

1. [核心概念](#1-核心概念)
2. [JSON 结构](#2-json-结构)
3. [事件](#3-事件)
4. [事件类型](#4-事件类型)
5. [Sync 校准](#5-sync-校准)
6. [阶段切换与分支](#6-阶段切换与分支)
7. [动作](#7-动作)
8. [变量与条件](#8-变量与条件)
9. [Targetable 目标可选中](#9-targetable-目标可选中)
10. [获取技能 ID](#10-获取技能-id)
11. [编辑器使用](#11-编辑器使用)
12. [完整示例](#12-完整示例)
13. [ACR 消费方式](#13-acr-消费方式)

---

## 1. 核心概念

一条时间线描述一个副本中 Boss 的行为序列。每条时间线由多个**阶段**组成，每个阶段包含一系列**事件**。事件在副本时间点上发生，可以"校准"（与游戏实际数据包同步对齐），也可以"触发动作"（切换 QT、发送技能建议等）。

```
时间线 (FactTimelineData)
  └─ 阶段 (FactPhase)
       ├─ 事件 (FactEvent)      ← Boss 做了什么
       ├─ 事件
       ├─ ...  
       └─ 切换点 (FactPhaseSwitch)
            ├─ 分支A (FactSwitchBranch)
            │    ├─ 事件
            │    └─ 子切换点
            └─ 分支B
                 └─ 事件
```

**时间推进**：时间轴按游戏内战斗时间自动推进。当到达一个事件的时间点时，如果该事件有 Sync，则等待游戏数据包匹配后继续；如果没有 Sync，则立即触发动作并继续。

**校准**：通过匹配游戏数据包（Boss 读条/技能效果），Sync 可以将时间轴的"理论时间"校准到"实际时间"，消除时间漂移。

---

## 2. JSON 结构

### 根对象

```json
{
  "name": "副本名称",
  "territoryId": 123,
  "author": "作者名",
  "phases": [...]
}
```

| 字段 | 类型 | 必须 | 说明 |
|------|------|------|------|
| `name` | string | 是 | 副本显示名称 |
| `territoryId` | number | 是 | 副本区域 ID（游戏内 TerritoryType） |
| `author` | string | 否 | 作者名 |
| `phases` | array | 是 | 阶段列表 |

### 阶段

```json
{
  "id": "p1",
  "name": "P1 开场",
  "events": [...],
  "switch": { ... }  // 可选
}
```

| 字段 | 类型 | 必须 | 说明 |
|------|------|------|------|
| `id` | string | 是 | 唯一标识符 |
| `name` | string | 是 | 显示名称 |
| `events` | array | 是 | 阶段内事件列表（按 time 排序） |
| `switch` | object | 否 | 阶段切换点（定义何时离开本阶段） |

---

## 3. 事件

事件是时间轴上最核心的单元——声明 Boss 在某个时间点做什么。

### 事件属性总览

```json
{
  "id": "raidwide1",
  "name": "第一次全场AOE",
  "time": 15.9,
  "type": "Ability",
  "abilityId": 46295,
  "duration": 3.0,
  "targetable": true,
  "startSync": { ... },
  "endSync": { ... },
  "actions": [ ... ]
}
```

| 字段 | 类型 | 必须 | 默认 | 说明 |
|------|------|------|------|------|
| `id` | string | 是 | — | 唯一标识符（建议用语义化命名如 `p1_raidwide`） |
| `name` | string | 是 | — | 人类可读的事件名 |
| `time` | number | 是 | — | 阶段内预期开始的秒数 |
| `type` | string | 否 | `"None"` | 游戏事件类型（见第4节） |
| `abilityId` | number | 否 | `0` | 主要 ID（技能ID/连线ID/BuffID，按 type 解释） |
| `duration` | number | 否 | `null` | 预期持续秒数（`null` 或 `0` = 瞬间事件） |
| `targetable` | boolean | 否 | `null` | 目标可选中状态声明（见第9节） |
| `startSync` | object | 否 | `null` | 开始校准配置 |
| `endSync` | object | 否 | `null` | 结束校准配置 |
| `actions` | array | 否 | `[]` | 到达时执行的动作用列表 |

### 简单事件（无 Sync）

不需要与游戏数据包同步的事件只需声明 `time`：

```json
{ "id": "pull", "name": "开战", "time": 0,
  "actions": [{ "type": "logMessage", "message": "开怪！" }] }
```

这类事件到达时间后立即触发动作。

### 带 Sync 的事件

需要与游戏数据包校准的事件，加 `startSync` 和/或 `endSync`：

```json
{ "id": "cast", "name": "补天之手", "time": 15.9,
  "type": "Ability", "abilityId": 46295,
  "startSync": { "windowBefore": 10, "windowAfter": 5 } }
```

事件到达 `15.9s` 时不会立即触发，而是进入等待 Sync 状态——直到匹配到技能 ID=46295 的 Ability 数据包，才校准时间并触发动作。

### 新格式 vs 旧格式

新格式已将 `type` 和 `abilityId` 提到事件一级。旧格式把这些信息放在 Sync 里，编辑器保存时会自动迁移。

```json
// 新格式 ✅
{ "time": 15.9, "name": "补天之手", "type": "Ability", "abilityId": 46295,
  "startSync": { "windowBefore": 10 } }

// 旧格式（向后兼容，加载时自动迁移）
{ "time": 15.9, "name": "补天之手",
  "startSync": { "type": "Ability", "abilityIds": [46295], "windowBefore": 10 } }
```

---

## 4. 事件类型

`type` 字段（`FactEventType` 枚举）声明这个事件对应哪种游戏数据包。全部 12 种：

| 枚举值 | 中文名 | 对应游戏包 | abilityId 含义 |
|--------|--------|-----------|---------------|
| `"None"` | 无类型 | — | 不匹配数据包 |
| `"Ability"` | 技能效果 | `ActionEffectParams` / `NoTargetAbilityEffectParams` | 技能 ID |
| `"StartsUsing"` | 读条开始 | `ActorCastParams` | 技能 ID |
| `"HeadMarker"` | 点名标记 | `ActorControlTargetIconParams` | 标记 IconID |
| `"Tether"` | 连线 | `TetherCreateParams` | 连线 TetherID |
| `"AddedCombatant"` | 单位出现 | `UnitCreateParams` | NPC DataId |
| `"RemovedCombatant"` | 单位消失 | `UnitDeleteParams` | NPC DataId |
| `"WasDefeated"` | 单位死亡 | `ActorControlDeathParams` | NPC DataId |
| `"GainsEffect"` | Buff取得 | `BuffGainParams` | Buff StatusID |
| `"LosesEffect"` | Buff消失 | `BuffRemoveParams` | Buff StatusID |
| `"MapEffect"` | 地图特效 | `MapEffectParams` | EffectID |
| `"NPCYell"` | NPC喊话 | `NpcYellParams` | YellID |

> **全部 12 种事件类型均支持 Sync 匹配校准。** 此外以下游戏数据包类型也会被接收，但暂无对应的 `FactEventType`（不做 Sync 匹配，仅流向 ACR）：`TetherRemoveParams`、`ActorControlTargetableParams`、`ActorControlCombatParams`、`ActorControlTimelineParams`、`ActorControlParams`、`DirectorUpdateParams`、`EnvControlParams`、`WeatherChangedParams`、`AfterSpellParams`、`CombatStateParams`。

### 使用场景

- **Boss 技能释放**：`"type": "Ability", "abilityId": 46295`
- **Boss 开始读条**：`"type": "StartsUsing", "abilityId": 46295`
- **Boss 死亡**：`"type": "WasDefeated", "abilityId": 12345`（NPC DataId）
- **Boss 点名**：`"type": "HeadMarker", "abilityId": 0x017F`
- **连线出现**：`"type": "Tether", "abilityId": 0x0175`

---

## 5. Sync 校准

### 为什么要校准

如果时间轴固定按声明的时间推进，随着副本进行会产生累积偏差（玩家的 DPS 可能比标准快或慢 2-3 秒）。Sync 通过匹配游戏数据包来修正这种偏差。

### Sync 配置

```json
"startSync": {
  "windowBefore": 10.0,
  "windowAfter": 5.0,
  "jump": null,
  "forcejump": false,
  "forcejumpTarget": null
}
```

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `windowBefore` | number | 2.5 | 窗口提前打开秒数 |
| `windowAfter` | number | 2.5 | 窗口延后关闭秒数 |
| `jump` | number | null | 同步命中后跳转到目标时间（null = 跳转到事件自身的 Time） |
| `forcejump` | boolean | false | 超时后无条件跳转 |
| `forcejumpTarget` | number | null | 无条件跳转的目标时间 |

### 窗口的含义

窗口 `[time - windowBefore, time + windowAfter]` 定义了接受 Sync 匹配的时间范围。

```
|←──── 时间线 ────→|
           time - windowBefore    time    time + windowAfter
                ╰──────────────────|───────────────────╯
                  Sync 窗口（在此区间匹配到才有效）
```

- 事件声明 `time=15.9`, `windowBefore=10`, `windowAfter=5`
- 窗口：`[5.9, 20.9]`
- 如果在 `13.2s` 匹配到，时间线会跳校到 `15.9s`（+2.7s 修正）

### forcejump — 超时无条件跳转

如果过了窗口时间还没匹配到，避免无限等待：

```json
"startSync": { "windowBefore": 10, "windowAfter": 5, "forcejump": true, "forcejumpTarget": 15.9 }
```

窗口 `[5.9, 20.9]` — 如果在 20.9s 还没匹配，自动跳转到 15.9s 并继续。

### startSync vs endSync

- `startSync`：校准事件开始时间。事件到达后等待匹配，匹配到才触发动作。
- `endSync`：校准事件结束时间。用于有持续时间的技能——匹配到后记录 `ActualEnd`。

> **推荐窗口大小**：大多数高难副本用默认 `windowBefore=5, windowAfter=5`；对时间敏感的机制（如开门）可缩小到 `windowBefore=2, windowAfter=1`。

---

## 6. 阶段切换与分支

阶段之间的转换通过 `FactPhaseSwitch` 控制。

### 切换点结构

```json
"switch": {
  "sync": { "type": "startsUsing", "abilityIds": [10591] },
  "actions": [
    { "type": "setVariable", "variableName": "phase1Completed", "value": true },
    { "type": "logMessage", "message": "P1 结束" }
  ],
  "branches": [
    { "name": "默认P2", "events": [...] },
    { "name": "高速击杀P2", "condition": { "variableName": "fastKill", "expectedValue": true }, "events": [...] }
  ]
}
```

### 切换点字段

| 字段 | 类型 | 必须 | 说明 |
|------|------|------|------|
| `sync` | FactSyncDef | 是 | 触发切换的 Sync 配置 |
| `actions` | array | 否 | 切换时执行的动作 |
| `branches` | array | 是 | 分支列表（第一个满足条件的为选中分支） |

### 分支

| 字段 | 类型 | 必须 | 说明 |
|------|------|------|------|
| `name` | string | 否 | 分支名 |
| `condition` | object | 否 | 条件（null = 默认兜底分支） |
| `events` | array | 是 | 分支内的事件列表 |
| `switch` | object | 否 | 分支结束时的下一切换点 |

### 分支匹配规则

1. 按 `branches` 数组顺序依次检查条件
2. **第一个**满足条件的分支被选中
3. 如果没有分支满足条件，保持在当前状态
4. 通常最后一个分支不设 `condition`（作为默认兜底分支）

### 嵌套切换

分支可以有自己的子切换点，支持最多两层嵌套。这对于复杂的多分支副本流程很有用（如 P1→P2A/P2B→P3）。

---

## 7. 动作

事件到达时或切换触发时可以执行动作。

### 动作类型总览

| JSON type | C# 类 | 说明 |
|-----------|------|------|
| `"skillSuggestion"` | SkillSuggestionAction | 推荐玩家使用某技能 |
| `"demand"` | 需求动作 (已废弃) | 同时声明减伤和治疗需求 |
| `"需求减伤"` | 需求减伤动作 | 声明此处需要 X% 减伤 |
| `"需求治疗"` | 需求治疗动作 | 声明此处需要治疗量 |
| `"setVariable"` | SetVariableAction | 设置布尔变量 |
| `"toggleVariable"` | ToggleVariableAction | 切换布尔变量 |
| `"设置QT"` | 设置QT动作 | 设置 QT 开关值 |
| `"切换QT"` | 切换QT动作 | 切换 QT 开关 |
| `"站位需求"` | 站位需求动作 | 声明站位需求（配合辅助轴） |
| `"logMessage"` | LogMessageAction | 记录日志消息 |

### 技能建议

```json
{ "type": "skillSuggestion", "skillId": 7561, "label": "策动", "priority": "high" }
```

| 字段 | 说明 |
|------|------|
| `skillId` | 推荐使用的技能 ID |
| `label` | 显示标签 |
| `priority` | `"high"` / `"normal"` / `"optional"` |

### 减伤/治疗需求

```json
{ "type": "需求减伤", "value": 30 }
{ "type": "需求治疗", "value": 800 }
```

`value`：减伤百分比 / 治疗量。HiAuRo 的 DecisionEngine 会根据这些值自动分配团队减伤和治疗技能。

### 变量操作

```json
{ "type": "setVariable", "variableName": "phase1Done", "value": true }
{ "type": "toggleVariable", "variableName": "aoeMode" }
```

### QT 控制

```json
{ "type": "设置QT", "qtId": "Burst", "value": false, "offset": 5.0 }
{ "type": "切换QT", "qtId": "Hold", "offset": -3.0 }
```

| 字段 | 说明 |
|------|------|
| `qtId` | QT 开关 ID |
| `value` | 设置的目标值（仅 `设置QT`） |
| `offset` | 延迟执行秒数（正=延迟，负=提前） |

> QT 控制受 `FactAxisFlags.QtControl` 配置开关影响。

---

## 8. 变量与条件

事实轴支持运行时变量，用于跨阶段传递状态。

### 条件类型

当前仅支持 `VariableCondition`——检查布尔变量是否等于期望值：

```json
"condition": { "variableName": "fastKill", "expectedValue": true }
```

### 变量生命周期

- 通过 `setVariable` / `toggleVariable` 动作设置
- 作用域为整个时间轴（不是阶段级）
- 可通过 `FactState.Variables` 在 ACR 中查询

---

## 9. Targetable 目标可选中

Targetable 声明 Boss 在某个时间点变为可选中/不可选中。ACR 作者基于此信息规划输出窗口。

### 声明方式

```json
{ "time": 30.0, "name": "Boss 跳走", "targetable": false }
{ "time": 40.0, "name": "Boss 落地", "targetable": true }
```

`targetable` 可以独立声明（`type="None"`），也可以与任何事件类型共存：

```json
{ "time": 40.0, "name": "Boss 落地", "type": "Ability", "abilityId": 99999, "targetable": true }
```

> Targetable 是**时间驱动**的声明——事件到达后立即生效，不依赖游戏数据包 Sync。

### ACR 查询方式

```csharp
var s = Data.FactState;

// 当前是否可选中
s.IsTargetable;              // bool? (null = 未声明)

// 距下次变为可用还有多久
s.NextTargetableIn;          // double? (null = 不会再有可选中窗口)

// 距下次变为不可用还有多久
s.NextUntargetableIn;        // double? (null = 不会再变不可选中)

// 前向扫描自定义预测
s.PendingEvents.Where(e => e.Targetable != null).ToList();
```

---

## 10. 获取技能 ID

> **最可靠的方法**：用 HiAuRo 的副本录制功能录一次副本，然后在编辑器中点击"从录制导入"，选择对应的时间点导入为事件——这样技能 ID 和准确时间都是实测数据。

### 其他获取方式

1. **Dalamud Data Window**：查看 Action Info / Status Info
2. **XivAlexander + ACT**：查看日志中的技能 ID
3. **Garland Tools / XIVAPI**：在线数据查询
4. **cactbot 时间轴**：`.txt` 文件中 `Ability { id: "XXXX" }` 的 ID

### ID 格式

- 游戏内技能 ID 是十进制（如 `7561`）
- cactbot 使用十六进制（如 `"B4D7"` → 十进制 `46295`）
- 事实轴编辑器接受十进制整数输入

---

## 11. 编辑器使用

事实轴编辑器位于 `localhost:5678/fact-editor`。

### 基本操作

| 操作 | 方法 |
|------|------|
| 创建新的时间轴 | 点击"新建"按钮 |
| 加载已有时间轴 | 点击"加载"，选择 JSON 文件 |
| 保存 | 点击"保存" |
| 添加事件 | 在画布上右键 → "添加事件" |
| 选中事件 | 单击事件节点 |
| 编辑属性 | 选中后在右侧面板修改 |
| 移动事件 | 拖拽事件节点到其他位置 |
| 删除事件 | 选中后按 Delete 键两次 |
| 添加动作 | 右键事件节点 → "添加动作" |
| 添加阶段 | 点击阶段列表的 + 按钮 |
| 从录制导入 | 点击"从录制导入"，选择录制 JSON 文件，勾选事件 → "导入到当前阶段" |

### 界面布局

```
┌─────────┬──────────────────┬─────────┐
│ 阶段列表 │    时间线画布      │ 属性面板 │
│         │   (垂直滚动)      │  [选中]  │
│  P1     │    0s ─·· ··     │  名称    │
│  事件A   │     5s ─·        │  时间    │
│  事件B   │    10s ─●──       │  类型    │
│  P2     │    15s ─·        │  Sync   │
│         │    20s ─·· ··     │  动作    │
└─────────┴──────────────────┴─────────┘
```

### 事件节点颜色含义

| 颜色 | 事件类型 |
|------|---------|
| 青色 | Ability |
| 橙色 | StartsUsing |
| 粉色 | HeadMarker |
| 墨绿 | Tether |
| 绿色 | AddedCombatant |
| 红色 | WasDefeated |
| 黄色 | GainsEffect |
| 紫色 | MapEffect |
| 灰色 | None |

---

## 12. 完整示例

```json
{
  "name": "极朱雀诗魂战",
  "territoryId": 297,
  "author": "示例作者",
  "phases": [
    {
      "id": "p1_opening",
      "name": "P1 开场",
      "events": [
        {
          "id": "pull",
          "name": "开战",
          "time": 0,
          "targetable": true,
          "actions": [{ "type": "logMessage", "message": "极朱雀 战斗开始" }]
        },
        {
          "id": "raidwide",
          "name": "第一次全场AOE",
          "time": 8,
          "type": "StartsUsing",
          "abilityId": 10589,
          "duration": 3,
          "startSync": { "windowBefore": 10, "windowAfter": 5 },
          "endSync": { "windowBefore": 2.5, "windowAfter": 2.5 },
          "actions": [
            { "type": "需求减伤", "value": 30 },
            { "type": "需求治疗", "value": 800 },
            { "type": "skillSuggestion", "skillId": 7561, "label": "策动 (10%减伤)", "priority": "high" },
            { "type": "logMessage", "message": "全场AOE → 减伤30% + 治疗800" }
          ]
        },
        {
          "id": "tankbuster",
          "name": "死刑",
          "time": 16,
          "type": "StartsUsing",
          "abilityId": 10590,
          "duration": 2,
          "startSync": { "windowBefore": 8, "windowAfter": 4 },
          "actions": [{ "type": "skillSuggestion", "skillId": 7559, "label": "行吟", "priority": "optional" }]
        },
        {
          "id": "boss_jump",
          "name": "Boss 跳走",
          "time": 30,
          "targetable": false
        },
        {
          "id": "boss_land",
          "name": "Boss 落地",
          "time": 40,
          "targetable": true
        }
      ],
      "switch": {
        "sync": { "windowBefore": 5, "windowAfter": 5 },
        "actions": [
          { "type": "setVariable", "variableName": "p1Completed", "value": true },
          { "type": "logMessage", "message": "P1 结束" }
        ],
        "branches": [
          {
            "name": "进入P2",
            "events": [
              {
                "id": "p2_start",
                "name": "P2开始",
                "time": 0,
                "actions": [{ "type": "logMessage", "message": "进入P2" }]
              },
              {
                "id": "p2_aoe",
                "name": "P2全场AOE",
                "time": 8,
                "type": "StartsUsing",
                "abilityId": 10592,
                "startSync": { "windowBefore": 10, "windowAfter": 5 },
                "actions": [
                  { "type": "需求减伤", "value": 20 },
                  { "type": "skillSuggestion", "skillId": 7561, "label": "策动", "priority": "high" }
                ]
              }
            ],
            "switch": {
              "sync": { "windowBefore": 5, "windowAfter": 5 },
              "branches": [
                {
                  "name": "P3 终结阶段",
                  "events": [
                    { "id": "p3_start", "name": "P3开始", "time": 0 },
                    {
                      "id": "enrage_warning",
                      "name": "即将狂暴",
                      "time": 60,
                      "actions": [{ "type": "logMessage", "message": "接近狂暴时间！" }]
                    }
                  ]
                }
              ]
            }
          }
        ]
      }
    }
  ]
}
```

---

## 13. ACR 消费方式

ACR 作者通过 `Data.FactState` 查询事实轴状态：

```csharp
using HiAuRo.FactAxis;

public void OnBattleUpdate(int battleTimeMs)
{
    var s = Data.FactState;
    if (s == null) return;

    // 时间维度
    var phaseName = s.PhaseName;       // "P1 开场"
    var phaseTime = s.PhaseTime;       // 当前阶段已过秒数
    var totalTime = s.TotalTime;       // 战斗总秒数

    // 目标可选中预测
    if (s.IsTargetable == false && s.NextTargetableIn > 3.0)
    {
        // Boss不可选中且3s内不会恢复 → 囤资源
        _holdResources = true;
    }
    if (s.NextUntargetableIn != null && s.NextUntargetableIn < 10.0)
    {
        // 10s内Boss会变不可选中 → 赶紧泄资源
        _dumpResources = true;
    }

    // 游戏事件时间查询（按类型）
    double? nextCast   = s.NextEventTimeOfType(FactEventType.StartsUsing);
    double? nextDamage = s.NextEventTimeOfType(FactEventType.Ability);

    // 游戏事件时间查询（按类型 + 特定技能ID）
    double? nextAoe    = s.NextEventTimeOfType(FactEventType.Ability, 10589);  // 只查ID=10589
    double? nextSpread = s.NextEventTimeOfType(FactEventType.HeadMarker, 0x017F);

    // 自定义前向扫描
    var nextMitEvent = s.PendingEvents
        .FirstOrDefault(e => e.Actions.Any(a => a is 需求减伤动作));
    if (nextMitEvent != null)
    {
        var timeUntilMit = nextMitEvent.Time - s.PhaseTime;
        // 准备减伤...
    }

    // 技能建议
    foreach (var sug in s.Suggestions)
    {
        if (sug.Priority == "high" && SpellHelper.CanUseSpell(sug.SkillId))
        {
            // 执行高优先级建议
        }
    }
}
```

---

## 参考

- [HiAuRo 架构设计](./ARCHITECTURE.md)
- [ACR 作者上手指南](./ACR_AUTHOR_GUIDE.md)
- [cactbot Timeline Guide](https://github.com/OverlayPlugin/cactbot/blob/main/docs/TimelineGuide.md)（时间轴编写参考）
