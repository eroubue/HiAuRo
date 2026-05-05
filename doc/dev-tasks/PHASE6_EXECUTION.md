# Phase 6: Execution Axis（执行轴）

## 目标

落地条件驱动的执行控制层，形成默认模式完整链路。补齐 Phase 5 留空的触发器具体实现。

**依赖**: Phase 5（全部子阶段已完成）
**需求**: EXEC-01 ~ EXEC-04

## 实现原则

- 执行轴是 ACR 的上层指挥官，可控制但非必需（无轴模式照常运作）
- 节点结构对齐 AE 的 TriggerLine AST，但保持实现简单直接
- 触发器条件/动作直接操作游戏数据，不引入中间适配层
- Phase 6+ 后续补充的触发器按本阶段模式追加即可

## 架构

```
Execution Axis (执行轴)
  │
  ├── TriggerLine[]          ← 多条触发线，各自独立
  │     └── ExecutionEntry[] ← 每个条目 = 条件 + 动作
  │
  ├── TriggerConditions      ← Rotation 全局触发条件（扁平列表）
  └── TriggerActions         ← Rotation 全局触发动作（扁平列表）

AIRunner.Update()
  │
  ├── 1. 检查 ExecutionAxis 是否有待处理输出（ForceSpell / Pause / SwitchTarget）
  ├── 2. 检查 Rotation.TriggerConditions → TriggerActions 配对
  └── 3. 正常 ACR 循环（无轴时直接走这一步）
```

## 文件清单

### 执行轴核心
| 操作 | 文件 | 说明 |
|------|------|------|
| 新建 | `Execution/ExecutionAxis.cs` | 执行轴主逻辑：TriggerLine 管理 + 每帧更新 |
| 新建 | `Execution/ExecutionNode.cs` | 节点定义：触发条目 = 条件 + 动作 + 延迟 |
| 新建 | `Execution/NodeProgressor.cs` | 节点推进：逐个评估触发条目 |
| 新建 | `Execution/ExecutionDebug.cs` | 调试诊断：当前节点、触发状态、失败原因 |

### 触发条件（首批 5 个）
| 操作 | 文件 | 说明 |
|------|------|------|
| 新建 | `Execution/Triggers/Cond/TriggerCond_敌人读条.cs` | 检测敌人是否在读指定技能 |
| 新建 | `Execution/Triggers/Cond/TriggerCond_经过时间.cs` | 检测战斗经过时间 |
| 新建 | `Execution/Triggers/Cond/TriggerCond_技能后.cs` | 检测自己是否刚用过指定技能 |
| 新建 | `Execution/Triggers/Cond/TriggerCond_Actor死亡.cs` | 检测指定敌人是否死亡 |
| 新建 | `Execution/Triggers/Cond/TriggerCond_倒计时.cs` | 检测副本倒计时 |

### 触发动作（首批 4 个）
| 操作 | 文件 | 说明 |
|------|------|------|
| 新建 | `Execution/Triggers/Action/TriggerAction_切换目标.cs` | 切换到指定的敌人目标 |
| 新建 | `Execution/Triggers/Action/TriggerAction_释放技能.cs` | 强制释放指定技能 |
| 新建 | `Execution/Triggers/Action/TriggerAction_切换停手.cs` | 控制 ACR 停手/恢复 |
| 新建 | `Execution/Triggers/Action/TriggerAction_吃药.cs` | 使用爆发药/回复药 |

### 修改文件
| 操作 | 文件 | 说明 |
|------|------|------|
| 修改 | `Runtime/ModeSwitch.cs` | 接入 ExecutionAxis 初始化和清理 |
| 修改 | `Runtime/AIRunner.cs` | 每帧检查执行轴输出 |

## 任务

### Task 1: 触发器具体实现

**操作**:
1. 创建 `Execution/Triggers/` 目录结构
2. 为每个 TriggerCond 创建具体参数类（实现 ITriggerCondParams）和条件实现类
3. 为每个 TriggerAction 创建具体动作实现类
4. 所有条件实现通过 `Data.Objects` / `EventSystem` / `CombatContext` 读取游戏状态
5. 所有动作实现通过 DService / UseActionManager 执行游戏操作

**首批触发条件**:
| 类名 | 参数 | 检测逻辑 |
|------|------|----------|
| `TriggerCond_敌人读条` | SpellId, EnemyDataId? | 遍历 Data.Objects.Enemies，检查是否有敌人 CastActionId == SpellId |
| `TriggerCond_经过时间` | TimeMs | 检查战斗经过时间 >= TimeMs |
| `TriggerCond_技能后` | SpellId | 检查最近使用的技能 ID == SpellId（通过 EventSystem 回调跟踪） |
| `TriggerCond_Actor死亡` | DataId | 检查指定 DataId 的敌人是否死亡 |
| `TriggerCond_倒计时` | TimeLeftSec | 检查副本倒计时剩余秒数 |

**首批触发动作**:
| 类名 | 参数 | 执行逻辑 |
|------|------|----------|
| `TriggerAction_切换目标` | TargetDataId?, Nearest | 在 Enemies 中查找并切换目标 |
| `TriggerAction_释放技能` | SpellId, TargetType | 强制释放指定技能（通过 SpellQueue 高优插入） |
| `TriggerAction_切换停手` | Stop | 设置 AIRunner 的暂停状态 |
| `TriggerAction_吃药` | ItemId | 使用 UseActionManager 执行物品使用 |

**验证**: `dotnet build` 通过；每个触发器可独立创建和测试

---

### Task 2: 执行轴节点结构与运行约定

**操作**:
1. 创建 `Execution/ExecutionNode.cs`
   - `ExecutionNodeType` 枚举：Sequence / Parallel / Select / Loop / Delay / Cond / Action / Script / ClearTarget / ClearWait
   - `ExecutionNode` 类：包含子节点、条件、动作、循环次数、延迟等字段
   - `ExecutionEntry` 类：简化入口 = 条件 + 动作 + 可选延迟（ACR 作者常用此结构）

2. 创建 `Execution/ExecutionAxis.cs`
   - `TriggerLine` 类：Id / Name / 条目列表 / Loop / Enabled
   - `ExecutionAxis` 单例：管理多条 TriggerLine、输出当前执行状态
   - `ExecutionOutput` 类：当前帧的输出（ForceSpell / ForceTarget / PauseAcr / 描述）
   - `Update(battleTimeMs)` → 推进节点+返回输出
   - `LoadLines(List<TriggerLine>)` → 加载触发线
   - 支持 Rotation 全局 TriggerConditions / TriggerActions 扁平列表

3. 创建 `Execution/NodeProgressor.cs`
   - 逐条目评估 TriggerLine
   - 每个条目：先检查条件 → 通过则执行动作 → 标记 Done
   - 支持 Loop：最后条目后回到第一条
   - 返回当前执行的节点信息给调试

4. 创建 `Execution/ExecutionDebug.cs`
   - `ExecutionDebug` 类：IsActive / ActiveLineId / CurrentNodeId / ConditionMet / FailureReason / BattleTimeMs
   - `ToJson()` 方法 → 序列化给 Web UI 展示
   - 记录最近 20 条触发历史

**验证**: `dotnet build` 通过；节点可创建、可遍历、可输出调试信息

---

### Task 3: 接入运行时

**操作**:
1. 修改 `Runtime/ModeSwitch.cs`
   - `SetMode(Mode.ExecutionAxis)` → 初始化 `ExecutionAxis.Instance`
   - `SetMode(Mode.None)` → 清理执行轴状态
   - 切换模式后重置相关状态

2. 修改 `Runtime/AIRunner.cs`
   - 在 Update() 方法开头检查执行轴模式
   - 如果执行轴激活且有输出 → 消费输出（强制技能/切换目标/暂停）
   - 如果输出指示 ConsumeFrame → 跳过正常 ACR 循环
   - 检查 Rotation.TriggerConditions / TriggerActions 扁平列表

**验证**:
1. `dotnet build` 通过
2. 无轴模式（当前默认）行为不变
3. 启用执行轴模式后框架不崩溃
4. 执行轴可正确控制 ACR 行为（停手/强制技能/切换目标）

---

## 阶段验证

- [ ] `dotnet build` 通过
- [ ] 5 个 TriggerCond 可独立创建和检测
- [ ] 4 个 TriggerAction 可独立创建和执行
- [ ] TriggerLine 可按顺序推进条目
- [ ] TriggerLine 支持 Loop 循环
- [ ] 执行轴可控制 ACR 行为（停手/指定技能/切换目标）
- [ ] 执行轴可输出调试信息（当前节点 + 触发/未触发原因）
- [ ] 无轴模式行为不变（向后兼容）
- [ ] 模式切换不崩溃

## 威胁模型

| 威胁 | 类别 | 处置 |
|------|------|------|
| 执行轴空触发线导致异常 | D | TriggerLine 为空时跳过 |
| 强制技能不可用（CD/资源） | D | 执行轴输出后由 SlotExecutor 判断，不可用则跳过 |
| 无限循环 TriggerLine 卡死 | D | 每帧最多处理 1 个条目；Loop 次数可配置 |
| 切换模式时状态残留 | D | SetMode 中清理旧模式状态 |

## 进度

| Task | 状态 |
|------|------|
| Task 1: 触发器具体实现 | 已完成 |
| Task 2: 执行轴节点结构与运行约定 | 已完成 |
| Task 3: 接入运行时 | 已完成 |

---

*Created: 2026-05-03*
