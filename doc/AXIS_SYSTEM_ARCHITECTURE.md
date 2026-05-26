# HiAuRo 轴系统架构文档

> 最后更新: 2026-05-26 — 全面分析结果

## 一、三种轴

```
┌──────────────────────────────────────────────────┐
│              AIRunner.Update() 每帧               │
│                                                   │
│  1. ExecutionAxis  → PauseACR / ForceSpell / ForceTarget │
│  2. FactTimeline   → FactState (观察) + DecisionEngine (决策) │
│  3. AssistAxis     → 始终运行的独立触发树         │
│                                                   │
│  4. ACR 正常循环 → Opener → SpellQueue → SlotResolver → Execute  │
└──────────────────────────────────────────────────┘
```

| 轴 | 驱动模型 | 数据格式 | 是否阻塞 ACR |
|---|---|---|---|
| **执行轴 (ExecutionAxis)** | async 触发树 AST | `ExecutionTimelineData` (PascalCase) | ✅ 可暂停/强制技能/skip帧 |
| **辅助轴 (AssistAxis)** | 同执行轴，但无事件唤醒 | 同执行轴，从 `.txt` 加载 | ❌ 始终并列运行 |
| **事实轴 (FactTimeline)** | 时间驱动线性时间线 + Sync 校准 | `FactTimelineData` (camelCase) | ❌ 不阻塞，提供 FactState |

### 1.1 执行轴 (ExecutionAxis)

- **引擎**: `HiAuRo/Execution/ExecutionAxis.cs` — 单例，async 触发树
- **节点**: `HiAuRo/Execution/ExecutionNode.cs` — 11 种 AST 节点
  - 组合: TreeSequence, TreeParallel, TreeSelect, TreeLoop
  - 叶子: TreeCondNode, TreeActionNode, TreeDelayNode, TreeScriptNode, TreePrintDebugInfoNode, TreeClearWaitNode
- **WaitCond 机制**: `TaskCompletionSource<bool>` 挂起 → 每帧 CheckWaitingConds() 或事件驱动 UseCondParams() 唤醒
- **输出**: `ExecutionOutput` (ForceSpell, ForceTarget, PauseAcr, ConsumeFrame)
- **JSON**: `{ConfigDir}/ExecutionTimelines/{territoryId}.json`
- **注册**: `ExecutionJsonLoader.RegisterBuiltInTypes()` 扫描所有 ITriggerCond/ITriggerAction 到 STJ 字典

### 1.2 辅助轴 (AssistAxis)

- **引擎**: `HiAuRo/Execution/AssistAxis.cs` — 与执行轴同一套 AST 引擎
- **与执行轴差异**:
  - 无事件驱动唤醒（`UseCondParams`），仅每帧轮询
  - WaitCond 的 key 类型仅支持 TreeCondNode（不支持 TreeScriptNode）
  - 始终运行，不受 ModeSwitch 控制
  - 从 `{ConfigDir}/AssistTimelines/{territoryId}.txt` 加载
  - `.txt` 文件内实际是相同的 JSON 格式

### 1.3 事实轴 (FactTimeline)

- **引擎**: `HiAuRo/FactAxis/FactTimeline.cs` — 单例，时间驱动
- **数据模型**: `HiAuRo/FactAxis/FactNode.cs`
  - `FactTimelineData` → `FactPhase[]` → `FactEvent[]` → `FactAction[]`
  - 每个阶段有 `FactPhaseSwitch` 包含分支 `FactSwitchBranch[]`
- **Sync 校准**: `startSync`/`endSync` 窗口内匹配游戏包 → 校准 `_timebase`
- **动作类型** (FactAction): setVariable, toggleVariable, skillSuggestion, logMessage, 需求减伤, 需求治疗, 设置QT, 切换QT, 站位需求, switchPhase, switchBranch
- **输出**: `FactState` 快照（CurrentEvent, PendingEvents, Variables, Suggestions, IsTargetable）
- **JSON**: `{ConfigDir}/FactTimelines/{territoryId}.json`

---

## 二、运行时协作（AIRunner）

```
每帧 AIRunner.Update():
  │
  ├─ ModeSwitch == ExecutionAxis:
  │   ├─ 进战斗 → ExecutionAxis.Start() → RunTreeAsync()（一次 async Task）
  │   ├─ ExecutionAxis.Update(battleTimeMs) → ExecutionOutput
  │   │   ├─ PauseAcr → return（跳过 ACR）
  │   │   ├─ ForceTarget → 切换目标
  │   │   ├─ ForceSpell → SlotExecutor.ExecuteSlot()
  │   │   └─ ConsumeFrame → return
  │   └─ Rotation 级 TriggerConditions/TriggerActions 配对检查
  │
  ├─ ModeSwitch == FactAxis:
  │   ├─ 进战斗 → FactTimeline.Start()
  │   ├─ FactTimeline.Update() → FactState
  │   ├─ UpdateDecisions() → 需求检测 → DecisionEngine 分配
  │   │   ├─ 治疗需求 → 立即执行
  │   │   └─ 减伤需求 → _pendingMits → CheckPendingMitigations() 延迟执行
  │   └─ IntelligenceEngine → MovementExecutor（不阻塞 ACR）
  │
  ├─ ALWAYS: AssistAxis update（独立，不阻塞）
  │   └─ ForceSpell → SlotExecutor
  │
  └─ ACR 正常循环: Opener → SpellQueue → SlotResolver → SlotExecutor
```

### ModeSwitch 控制

- `Mode.None` / `Mode.ExecutionAxis` / `Mode.FactAxis` 三种模式
- 执行轴和事实轴**互斥**，辅助轴始终运行
- 切图时 `TryAutoSwitch()` 根据目录中是否存在对应文件自动切换
- 优先级在 `PluginConfig.AutoSwitch` 中配置

### 事实轴 → 决策层

```
FactEvent.Actions (需求减伤/需求治疗)
  → AIRunner.UpdateDecisions() → DecisionEngine
    → 遍历队伍成员 → DecisionSkillRegistry 查可用技能
    → 过滤冷却中的 → 按 CD 排序（短优先） → 贪心分配
    → DecisionOutput (减伤分配 + 治疗分配 + 技能ID列表)
  → FactSpellTable.构造Spell(id) → SlotExecutor.ExecuteSlot()
```

### 执行轴 → 事实轴 协作

- `TreeScriptNode.FactNodeId` 可绑定事实轴的事件/阶段 ID
- TreeScriptNode 脚本内可通过 `FactTimeline.Instance.State` 读取事实轴状态
- 执行轴的条件/动作也可直接查 `Data.FactState` 或 `FactTimeline.Instance.State`

---

## 三、JSON 数据格式

### 3.1 执行轴/辅助轴 (ExecutionTimelineData)

```json
{
  "Name": "Timeline",
  "TerritoryTypeId": 123,
  "Note": "",
  "ExposedVars": [],
  "TreeRoot": {
    "$type": "HiAuRo.Execution.TreeRoot, HiAuRo",
    "DisplayName": "Root",
    "Id": 1,
    "Childs": [
      {
        "$type": "HiAuRo.Execution.TreeCondNode, HiAuRo",
        "CheckOnce": false,
        "CondLogicType": 0,
        "TriggerConds": [
          { "$type": "TriggerCondActorDeath", "DataId": 5411 }
        ]
      },
      {
        "$type": "HiAuRo.Execution.TreeActionNode, HiAuRo",
        "TriggerActions": [
          { "$type": "TriggerActionCastSpell", "SpellId": 3559, "TargetType": 0 }
        ]
      },
      {
        "$type": "HiAuRo.Execution.TreeScriptNode, HiAuRo",
        "Script": "return true;",
        "OnlyCheck": false,
        "factNodeId": "evt_12345"
      }
    ]
  }
}
```

- `$type` 格式: `"Namespace.ShortName, AssemblyName"`
- 节点属性 PascalCase（除 `factNodeId` 是 camelCase）
- Trigger 实例的 property 名 = C# 属性名（`PropertyNameCaseInsensitive` STJ 匹配）
- `TriggerCond_Variable.VariableVaule` 是 AE 拼写（有 bug 但保持兼容）
- axflow-editor 额外字段 `_drawflow.positions` / `zoom` —— 仅编辑器使用

### 3.2 事实轴 (FactTimelineData)

```json
{
  "name": "Timeline",
  "territoryId": 123,
  "author": "",
  "phases": [
    {
      "id": "p1",
      "name": "Phase 1",
      "events": [
        {
          "id": "evt_uuid",
          "name": "Raidwide",
          "time": 8.0,
          "duration": 3.0,
          "type": "StartsUsing",
          "abilityId": 12345,
          "startSync": { "windowBefore": 2.5, "windowAfter": 2.5 },
          "actions": [
            { "type": "skillSuggestion", "skillId": 456, "label": "Use Shield", "priority": "high" },
            { "type": "需求减伤", "需求减伤": 30, "需求治疗": 800 }
          ]
        }
      ],
      "switch": {
        "sync": { "windowBefore": 2.5, "windowAfter": 2.5 },
        "branches": [
          { "name": "Branch1", "condition": null, "events": [], "switch": null }
        ]
      }
    }
  ]
}
```

- 全部使用 camelCase
- FactAction 使用 `type` 字段区分行为类型
- 事件 ID (`FactEvent.Id`) 和阶段 ID (`FactPhase.Id`) 为 string 类型

### 3.3 Trigger Catalog (trigger-catalog.json)

```json
{
  "conditions": [{
    "typeName": "HiAuRo.Execution.Triggers.Cond.TriggerCond_经过时间",
    "typeDiscriminator": "TriggerCondAfterBattleStart",
    "displayName": "经过时间",
    "description": "...",
    "category": "builtin",
    "cloudSync": true,
    "parameters": [...],
    "controls": [
      { "label": "TimeMs", "type": "intInput", "defaultValue": 0, "options": null }
    ]
  }],
  "actions": [...],
  "scripts": []
}
```

- camelCase 命名（`JsonNamingPolicy.CamelCase` 序列化）
- `parameters` 是旧格式（构造函数参数），`controls` 是新格式（Draw() 声明）
- Web 编辑器优先用 `controls`，回退到 `parameters`
- `typeDiscriminator` 是 JSON `$type` 值，由 `[TriggerTypeName]` 属性定义

---

## 四、Web 编辑器

### 4.1 编辑器清单

| 编辑器 | 文件 | 编辑对象 | JSON 格式 |
|---|---|---|---|
| Tree 编辑器 | `editor.html/js/css` | 执行轴/辅助轴 | ExecutionTimelineData (PascalCase) |
| Drawflow 编辑器 | `axflow-editor.html/js/css` | 执行轴/辅助轴 | 同上 + _drawflow 元数据 |
| Fact 编辑器 | `fact-editor.html/js/css` | 事实轴 | FactTimelineData (camelCase) |

### 4.2 执行轴编辑器节点类型覆盖

| 节点类型 | editor.html | axflow-editor | 说明 |
|---|---|---|---|
| treeRoot | ✅ | ✅ | 根节点 |
| treeSequence | ✅ | ✅ | 序列 |
| treeParallel | ✅ | ✅ | 并行 |
| treeSelect | ✅ | ✅ | 选择 |
| treeLoop | ✅ | ✅ | 循环 |
| treeCondNode | ✅ | ✅ | 条件 |
| treeActionNode | ✅ | ✅ | 动作 |
| treeDelayNode | ✅ | ✅ | 延迟 |
| treeScriptNode | ✅ | ✅ | 脚本 |
| treePrintNode | ✅ | ❌ 缺失 | 调试输出 |
| treeClearWait | ✅ | ❌ 缺失 | 清除等待 |

### 4.3 属性面板功能覆盖

| 功能 | editor.html | axflow-editor | C# 有 |
|---|---|---|---|
| CondLogicType 编辑 | ❌ | ❌ | ✅ (And/Or) |
| FactNodeId 编辑 | ✅ (文本框) | ❌ | ✅ |
| OnlyCheck 编辑 | ❌ | ✅ | ✅ |
| IgnoreNodeResult (Sequence) | ✅ | ✅ | ✅ |
| AnyReturn (Parallel) | ✅ | ✅ | ✅ |
| Times (Loop) | ✅ | ✅ | ✅ |
| Delay (DelayNode) | ✅ | ✅ | ✅ |
| 触发条件增删改 | ✅ | ✅ | — |
| 触发动作增删改 | ✅ | ✅ | — |
| 触发参数编辑 | ✅ | ✅ | — |

### 4.4 事实轴编辑器

- 完全不同的事件/动作体系（16 种事件类型，11 种动作类型）
- 支持阶段管理、分支/切换
- 支持录制数据导入
- **加载了 catalog 但不使用**

### 4.5 两套重复代码

执行轴编辑器和 Drawflow 编辑器各自独立实现了以下函数：
- `getEntryControls()` — 获取控件定义
- `findCatalogEntry()` — 查找目录条目
- `renderTriggerField()` — 渲染触发参数输入
- `addTriggerCond()` / `removeTriggerCond()` — 条件 CRUD
- `addTriggerAction()` / `removeTriggerAction()` — 动作 CRUD
- `typeToFull()` / `TYPE_FULL` — `$type` 字符串映射

---

## 五、已知问题清单

### 🔴 关键缺陷

1. **执行轴编辑器无法加载事实轴文件** — FactNodeId 是空文本框，用户需手写 UUID
2. **axflow-editor 缺 2 种节点类型** — TreePrintDebugInfoNode, TreeClearWaitNode
3. **CondLogicType 在所有编辑器都不可编辑** — C# 支持但属性面板不展示

### 🟡 功能缺失

4. **属性面板不一致**: FactNodeId 只有 editor.js 有，OnlyCheck 只有 axflow-editor 有
5. **fact-editor 加载 catalog 但无消费** — 死代码
6. **两套重复的 trigger 编辑代码** — 任何改动需两边同步
7. **iosSelect 自定义下拉框用 div 但 onclick 访问 .options** — 已修复

### 🔵 数据格式不一致

8. **执行轴 PascalCase vs 事实轴 camelCase** — 历史原因
9. **factNodeId 是 camelCase（ExecutionJson.cs line 122）** — 唯一例外
10. **TriggerCond_Variable.VariableVaule** — AE 原始拼写 bug 保留

### ⚪ 架构局限

11. **C# 端无 AST→JSON 反序列化** — 只能从 JSON 加载，不能保存回 JSON；Web 编辑器在前端维护 JSON 状态
12. **catalog 无自动推送** — 用户手动加载文件，C# 与 Web 无网络连接
13. **GitHub catalog 推送有 sha 字段 bug** — 已修复

---

## 六、关键路径

### catalog 流水线

```
C# 启动 → TriggerCatalogBuilder 反射扫描 → trigger-catalog.json
                                               ↓
Web 编辑器 "加载目录" → localStorage.hiAutoLocalTriggers
                                               ↓
               getEntryControls() → renderTriggerField() → 参数输入
                                               ↓
            addTriggerCond() → { $type, Property: value }
                                               ↓
                         保存为 .json 文件 → C# STJ 反序列化
```

### 时间线数据流

```
Web 编辑器 → 编辑树 → 保存 JSON 文件
                            ↓
C# ExecutionAxis.Init() → ExecutionJsonLoader.FromJson()
                            ↓
              TriggerNodeData.ToNode() → AST 节点
                            ↓
          TreeCondNode.TriggerConds ← STJ 反序列化 → ITriggerCond 实例
                            ↓
              ExecutionAxis.Start() → RunTreeAsync()
```
