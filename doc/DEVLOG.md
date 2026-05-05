# HiAuRo 开发日志

> 记录关键决策、踩坑教训、架构演变，便于后续开发。

---

## 2026-05-03 全天开发记录

### 项目启动 → MVP 完成（Phase 1~5）

#### Phase 1: 插件骨架 ✅

- 建 `HiAuRo.csproj`（`Dalamud.CN.NET.Sdk/15.0.0`）+ `Plugin.cs` + `HiAuRo.json`
- OmenTools 本地副本用 CN SDK，刚开始用非 CN 版报错，统一后解决
- **教训**: 本地 OmenTools 是什么 SDK，主项目就用什么，不要混

#### Phase 2: 配置 + 日志 ✅

- `PluginConfig` 走 `IPluginConfiguration`
- 日志前缀约定：`[Lifecycle]` / `[Config]` / `[Debug]`

#### Phase 3: 数据层 ✅

- `HiAuRo.Data` — Self/Target/Combat 转发 OmenTools，Party/Objects 一次扫描多视图
- BRDHelp 诗人职业快捷入口
- **踩坑**: OmenTools 的 `IGameObject` / `IPlayerCharacter` 等通过 `global using` 重定义了，不是 Dalamud 原版。主项目复制 `GlobalUsings.cs` 统一类型
- `ClassJob` 类型是 `RowRef<ClassJob>`，得 `.RowId` 取 uint
- `IObjectTable.SearchByID` 不是 `SearchById`
- `IStatus.StatusID` 不是 `StatusId`
- `BattleNpcSubKind` 没有 `Enemy`，改用 `IsTargetable` 联合多字段判断
- `Data` 类名与 `Data/` 文件夹冲突 → 改为 `namespace HiAuRo.Data`

#### Phase 4: 运行时核心 ✅

- `RuntimeCore` → `FrameworkManager.Instance().Reg(OnTick)`
- `CombatContext` 状态机 Idle/InCombat/OutOfCombat/Zoning
- `EventSystem` 基于 `UseActionManager` Hook
- `Coroutine` 轻量协程（不用 Task/async）
- `ACRLifecycle` Init/Update/Dispose 空壳
- **踩坑**: `UseActionManager` 的 Hook 签名在不同 CefSharp 版本不同，必须对齐

#### Phase 5.1: ACR 核心接口 ✅

- `IRotationEntry` / `Slot` / `SlotAction` / `Spell` / `Rotation` 等 22 个文件
- `Spell.Idle` 哨兵技能（ID=0，空转等待）
- `WaitType`: None / WaitInMs / WaitForSndHalfWindow

#### Phase 5.2: 起手/序列/触发器/Helper ✅

- `IOpener` 继承 `ISlotSequence`
- `OpenerMgr` 起手状态机
- 五个 Helper：`GCDHelper` / `SpellHelper` / `TargetHelper` / `AuraHelper` / `CooldownHelper`
- **踩坑**: FFXIVClientStructs 的 `ActionType.Action` 不是 `Spell`；`GetRecastGroupDetail` 返回指针，角色未加载时为 null → 每处加 null 检查

#### Phase 5.3: Web UI 层 ✅

**Kestrel → HttpListener 的教训（最惨）**

- 最初用 Kestrel（ASP.NET Core），加了 `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
- 在 Dalamud 的 `ManagedLoadContext` 中无法加载 `Microsoft.Extensions.Hosting`
- 尝试了 `AssemblyLoadContext.Resolving` → **Dalamud 的 ManagedLoadContext 不触发 Resolving 事件**
- 尝试了 `AssemblyLoadContext.Default.LoadFromAssemblyPath` 预加载 → 也不走
- **最终方案**: 放弃 Kestrel，改用 `HttpListener`（.NET 内置，零依赖）

**CEF 渲染管线（已移除）**

- 参考 Browsingway 实现了完整的 CEF 渲染管线：
  - 独立 `HiAuRo.Renderer.exe`（CefSharp 147）
  - D3D11 共享纹理
  - SharedMemory + JSON IPC
  - WndProc 键盘鼠标转发
- **最终决定移除 CEF**，悬浮窗交给 Browsingway：
  - CEF 二进制打包后 zip 膨胀
  - 渲染进程不稳定，游戏卡顿
  - Browsingway 已有成熟方案，复用即可
- **教训**: 不要重复造轮子。Browsingway 已经解决了 CEF 渲染的所有问题，HiAuRo 只需提供 jobview.html 给 Browsingway 显示

**WebSocket → 连接时一次推送**

- 最初每秒 500ms 推送状态
- 改为连接时推一次初始状态，前端自维护
- **教训**: 事件驱动优于轮询

#### Phase 5.4: AI 引擎 + BRD 打样 ✅

- `IAILoop` / `AILoop_Normal` / `SlotExecutor` / `SpellQueue` / `AIRunner`
- BRD 示例：强力射击（GCD）+ 失血箭（oGCD）+ 起手

**AILoop_Normal 三次重构（最关键的教训）**

第一次：GCD 和 oGCD 分两个独立循环，各扫各的列表
- 问题：GCD 技能优先执行，oGCD 只能在 GCD 之后 → 不符合 AE 设计

第二次：改为一个列表混合扫描，按 Mode 分别匹配窗口
- 问题：在 Check() 之前就按 Mode 过滤了 → 不对

**第三次（正确版）**：与 AE 完全对齐
```
遍历所有 Resolver（index 越小优先级越高）
  → 每个都调 Check()           ← 不管窗口，全部调用
  → Check() < 0? 跳过            ← Resolver 不想执行
  → SlotMode 判定执行时机:
      Gcd    → GCD 就绪才 Build
      OffGcd → oGCD 窗口 + 能力技未满才 Build
      Always → 任何时都执行
  → 第一个满足的 → Build() → 返回
```

- **教训**: Check() 是"你想执行吗"，Mode 是"现在能执行吗"。两者独立。
- Check() 返回值 `>= 0` 只表示"我想执行"，**不表示优先级**。优先级由列表 index 决定。

#### ACR 动态加载 ✅

- `ACRLoader` 扫描 `ACR/作者名/*.dll`
- 每作者独立 `AssemblyLoadContext`，支持 `/hi reload` 热卸载
- `Resolving` 事件桥接 HiAuRo/OmenTools/Dalamud → 宿主 ALC
- **内置 BRD 移除**，移至 `example/BRD/` 作为 ACR 作者模板
- `SettingMgr` 增加 ACR 设置：`GetAcrSetting(author, job)` / `SaveAcrSetting`
- `SettingMgr` 增加 ACR 设置：`GetAcrSetting(author, job)` / `SaveAcrSetting`
- 职业切换自动匹配 ACR

#### UI 架构 ✅

- **ImGui 主界面**（设置/状态/Debug）→ WindowSystem 托管
- **Web 悬浮窗**（jobview.html）→ HttpListener :5678 + WebSocket
- **Browsingway** → 游戏内 CEF 渲染 `localhost:5678/jobview.html`
- `OpenConfig` 点击切换开关（Toggle）

#### 修复补丁

- `GCDHelper.GetRecastGroupDetail` 空指针：角色未加载时返回 null → 加 null 检查
- `MainWindow` 未加载数据时显示"等待角色加载"
- `WebUiServer` URL 无后缀自动补 `.html`
- `ACRLifecycle.CheckJobSwitch` 从 `OutOfCombat` 移到每帧调用
- `RuntimeCore` 移除每 500ms 推送，改为 WebSocket 连接时一次推送

---

### 2026-05-04 Phase 6: 执行轴 ✅

#### Plan 06-01: 补齐触发器具体实现

- 实现了远超 Phase 6 首批计划的触发器数量：
  - **TriggerCond 18 种**：敌人读条、经过时间、技能后、Actor死亡、倒计时、倒计时开始、单位可选中、单位移除、地图特效、天气变化、技能冷却、收到技能效果、检查目标图标、游戏日志、等待目标、上次技能、连线
  - **TriggerAction 10 种**：切换目标、释放技能、切换停手、吃药、技能队列、高优Slot、发送命令、发送按键、设置Rotation、锁定技能
- 覆盖了 AEAssist 核心子集，接近完整对齐

#### Plan 06-02~04: 执行轴核心结构

- `ExecutionAxis` — TriggerLine 集合管理 + ExecutionOutput 控制信号
- `ExecutionNode` — 10 种 AST 节点类型（对齐 AE）+ ExecutionEntry 简化条目
- `NodeProgressor` — 逐条推进：条件→动作→Delay→Loop→Done标记
- `ExecutionDebug` — 当前活跃触发线/条目索引/条件状态/失败原因 + 20条历史记录

#### 运行时集成

- `AIRunner.Update()` 通过 `ModeSwitch.CurrentMode == ExecutionAxis` 接入
- `ExecutionOutput`：ConsumeFrame / ForceSpell / ForceTarget / PauseAcr / ResumeAcr
- Rotation 级全局触发器在 ACR 正常循环前执行
- `CanUseHighPrioritySlotCheck` 回调保护强制技能安全

---

### 当前架构总览

```
HiAuRo.dll
├── Data/        游戏数据 (Self/Target/Combat/Party/Objects)
├── Runtime/     运行时 (Core/CombatContext/AIRunner/AILoop/Coroutine)
├── ACR/         ACR 接口 + Helper + 合同类型
├── UI/          ImGui 主界面 + HttpListener Web 服务
├── Command/     /hi 命令系统
├── Setting/     设置持久化 + ACR 设置
└── Infrastructure/ 配置

悬浮窗: Browsingway → localhost:5678/jobview.html
开发示例: example/BRD/
```

---

### 关键踩坑清单

| # | 问题 | 原因 | 解决 |
|---|------|------|------|
| 1 | Kestrel 无法启动 | Dalamud ManagedLoadContext 不解析 ASP.NET 程序集 | 换 HttpListener |
| 2 | CEF 渲染进程卡顿 | CEF 二进制未完全适配 | 移除，交给 Browsingway |
| 3 | `Data` 类名冲突 | 文件夹名 `Data/` 与类名 `Data` 冲突 | 改 namespace |
| 4 | OmenTools 类型不一致 | OmenTools 重定义了 IGameObject 等 | 统一用 GlobalUsings |
| 5 | `detail->IsActive` 空指针 | 角色未登录时 GetRecastGroupDetail 返回 null | 加 null 检查 |
| 6 | AILoop Check/Build 混用 | Check 前就按 Mode 过滤了 | Check 全调，Mode 只决定 Build |
| 7 | 职业切换不触发 | CheckJobSwitch 只在 OutOfCombat 调用 | 移到每帧调用 |
| 8 | WebUiBridge 从未连线 | 消息处理器未注册，SendAsync 未调用 | 连接时推状态 + 注册 handler |
| 9 | flatpack 自建包失败 | CefSharp 147 vs Browsingway 143 API 差异 | 放弃自建，用 Browsingway |
| 10 | Spell.GetTarget() 空桩 | Sdk 分离时改了接口 | 还原完整实现 |

---

### 后续计划

- **Phase 7**: 事实轴（Boss 时间线 JSON）← 下一步
- **Phase 8**: 智能决策层
- **Phase 9**: 创作工具（可视化编辑器）
- **P0~P3 对齐**: SpellTargetLimit / ITargetResolver / TriggerCond 扩展 / JobApi 覆盖

---

## 2026-05-05 执行轴重构 + 辅助轴 + 脚本编译

### 执行轴从扁平线重构为 AE 触发树 AST

**原问题**：之前的执行轴是扁平 `TriggerLine → ExecutionEntry` 模型，与 AE 的树形 `TreeSequence/TreeParallel/TreeSelect/TreeLoop` AST 完全不一致。JSON 格式也不兼容 AE。

**重构**：
- `ExecutionNode.cs`：10 种 AST 节点 → `async Task<bool>` 求值，与 AE 的 `TreeNodeBase.Run()` 对齐
- `ExecutionAxis.cs`：从逐帧轮询改为 async void 一次性调用 + TCS WaitCond 机制
- `ExecutionJson.cs`：支持 AE 格式 JSON（`$type` 多态）反序列化 + 自定义类型注册
- `Coroutine.cs`：新增 `DelayAsync(ms)` 和 `WaitUntilAsync(condition)` 支持 async/await

**对齐 AE 的关键机制**：
- WaitCond：`TaskCompletionSource<bool>` 注册表 → 每帧 `CheckWaitingConds()` 唤醒
- 顺序模式：`IgnoreNodeResult=true` → 子节点失败不短路
- Select 节点：所有分支失败时仍返回 true（AE 特有行为）
- Parallel 竞赛模式：`Task.WhenAny`

### 辅助轴（AssistAxis）

- 完全复用执行轴 AST 引擎
- 始终运行，独立于 ModeSwitch（执行轴/事实轴切换不影响）
- 从 `AssistTimelines/{副本ID}.txt` 加载（执行轴用 .json）
- AIRunner 集成：战斗启停 + 输出处理

### 脚本动态编译（TreeScriptNode）

- 新增 `ITriggerScript` 接口 + `ScriptCompiler`
- 使用 Roslyn `Microsoft.CodeAnalysis.CSharp` 动态编译
- 按代码哈希缓存，同脚本只编译一次
- 自动包装 using 引用（HiAuRo/OmenTools/Dalamud）

### 文件变更

| 操作 | 文件 |
|------|------|
| 新增 | `Execution/ExecutionJson.cs`, `Execution/ScriptCompiler.cs`, `Execution/AssistAxis.cs` |
| 新增 | `ACR/Interfaces/ITriggerScript.cs` |
| 重写 | `Execution/ExecutionNode.cs`（同步→async）, `Execution/ExecutionAxis.cs`（扁平线→AST） |
| 删除 | `Execution/NodeProgressor.cs`, `Execution/ExecutionDebug.cs` |
| 修改 | `Runtime/Coroutine.cs`（+DelayAsync/+WaitUntilAsync） |
| 修改 | `Runtime/AIRunner.cs`（+AssistAxis 集成） |
| 修改 | `HiAuRo.csproj`（+Roslyn NuGet） |

---

## 2026-05-05 事实轴重构

**原问题**：第一版事实轴（Stopwatch + SyncEngine + BranchEngine）的 Sync 概念不对——把 Sync 当成了阶段级别的校准，实际 Sync 是事件级别的（校准单个事件的开始/结束时刻）。

**重构**：
- `FactNode.cs`：新数据模型 —— Phase → Event → PhaseSwitch → Branch（嵌套），每事件有 startSync/endSync
- `FactTimeline.cs`：合并 SyncEngine + BranchEngine 逻辑到主控制器
  - 阶段内纯时间推进 + 事件等待 Sync
  - 切换点触发后评估分支替换事件列表
  - StageTime + TotalTime 双时钟
- 删除 `SyncEngine.cs` / `BranchEngine.cs`

**核心逻辑**：
```
阶段内: 时钟走到事件时间 → 有 Sync? → 等 Sync 事件 → 校准开始/结束
阶段末: 所有事件完成 → 等切换 Sync → 评估分支 → 替换事件列表
```

---

*Last updated: 2026-05-05*
