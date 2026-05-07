# HiAuRo 代码审计报告

> 审计范围: `../HiAuRo/` 全部 `.cs` `.html` `.js` `.css` 文件  
> 审计日期: 2026-05-03  
> 修复日期: 2026-05-03  
> 项目: FFXIV Dalamud 战斗辅助框架 (.NET 10, Dalamud.CN.NET.Sdk 15.0.0)

---

## 修复状态总览

| 状态 | 数量 | 说明 |
|------|------|------|
| ✅ 已修复 | 17 | Critical 5 + High 5 + Medium 7 |
| ⏸️ 推迟 | 24 | Low:20 + Medium:2 + High:2 (需游戏环境验证/非阻塞) |

### 本次修复列表

| # | 严重度 | 问题 | 修复方式 |
|---|--------|------|----------|
| 1 | Critical | WebUiBridge 未注册消息处理器 | Plugin.cs 注册 4 个 handler |
| 2 | Critical | 脱战时每帧调用 Reset() | ACRLifecycle 添加 _resetCalled 标记 |
| 3 | Critical | SlotExecutor 只处理 Self/Target | 实现全部目标类型 switch |
| 4 | Critical | Spell.GetTarget() 空桩 | 完整实现 16 种目标类型 |
| 5 | Critical | WebUiBridge.SendAsync 从未调用 | RuntimeCore 每 500ms 推送状态 |
| 6 | Critical | Plugin.Dispose 未释放 WebUiBridge | 添加 IDisposable + Dispose 调用 |
| 8 | High | SpellHelper.IsInRange 硬编码 25f | 使用 ActionManager 查实际射程 |
| 9 | High | EventSystem OnActionCompleted 空 | OnPostUseAction 增加触发逻辑 |
| 10 | High | RenderProcess.Dispose 未释放 Rpc | 添加 Rpc?.Dispose() |
| 12 | High | CountDownHandler.Update 空实现 | 实现超时检查+技能执行 |
| 13 | Medium | CefManager 空壳 | 接入 RenderProcess |
| 16 | Medium | PluginConfig 缺少设置字段 | 增加 4 个设置属性 |
| 37 | Medium | SpellCategory 未映射 ActionType | SlotExecutor.SpellCategoryToActionType() |
| 21 | Medium | Coroutine 精度 | 使用 Stopwatch.GetTimestamp |
| 27 | Medium | app.js 消息格式不统一 | send() 统一为 {type, data} 格式 |

---

## 一、严重问题 (Critical)

### #1 | WebUiBridge 未注册任何消息处理器 — 所有 JS→C# 消息被丢弃 ✅
- **文件**: `UI/WebUiBridge.cs:21-27` + 全局搜索
- **严重度**: **Critical**
- **状态**: ✅ 已修复 — Plugin.cs RegisterUiHandlers() 注册 toggleACR/settingChanged/saveSetting/toggleQT
- **描述**: `WebUiBridge._handlers` 字典存在且 `On()` 方法可供注册，但整个代码库中 **没有任何地方调用 `.On()` 注册处理器**。前端 `app.js` 发送的 `toggleACR`、`toggleQT`、`settingChanged`、`saveSetting` 四种消息全部被静默丢弃。
- **影响**: 前端 UI 的按钮点击、设置修改完全无法到达 C# 后端。Web 面板是"只读"状态。
- **修复**: 在 `Plugin.cs` 构造函数中或 `WebUiServer` 初始化后，调用 `_uiBridge.On()` 注册处理逻辑：
  ```csharp
  _uiBridge.On("toggleACR", _ => { /* 切换 RuntimeCore */ });
  _uiBridge.On("settingChanged", data => { /* 保存设置 */ });
  _uiBridge.On("saveSetting", data => { /* 保存职业设置 */ });
  _uiBridge.On("toggleQT", data => { /* QT 切换 */ });
  ```

### #2 | ACRLifecycle.Update 在脱战状态每帧调用 Reset() ✅
- **文件**: `Runtime/ACRLifecycle.cs:37-39`
- **严重度**: **Critical**
- **状态**: ✅ 已修复 — 添加 _resetCalled 一次性标记
- **描述**: 当 `CombatContext.CurrentState == State.OutOfCombat` 时，**每帧**调用 `Runner.Reset()`，其中包含 `OnResetBattle()` 事件触发、队列清空、倒计时重置。这意味着 ACR 作者的 `OnResetBattle` 回调会在脱战期间每帧被调用一次。
- **影响**: 性能浪费 + 错误的事件语义。`OnResetBattle` 只应在"刚脱战"时调用一次。
- **修复**: 添加一次性标记，仅在状态首次变为 OutOfCombat 时调用 Reset：
  ```csharp
  private static bool _resetCalled;
  public static void Update()
  {
      var state = CombatContext.CurrentState;
      if (state is State.Idle or State.Zoning) { _resetCalled = false; return; }
      if (state == State.OutOfCombat)
      {
          if (!_resetCalled) { Runner.Reset(); _resetCalled = true; }
          return;
      }
      _resetCalled = false;
      Runner.Update();
  }
  ```

### #3 | SlotExecutor 只处理 Self/Target 两种目标类型 ✅
- **文件**: `Runtime/SlotExecutor.cs:52-54`
- **严重度**: **Critical**
- **状态**: ✅ 已修复 — 实现全部 16 种目标类型 + SpellCategory→ActionType 映射
- **描述**: `SlotExecutor.ExecuteSlot` 中的目标解析仅区分 `SpellTargetType.Self` 和 "其他"(都落到 Target.Current)。`SpellTargetType` 定义了 16 种目标类型 (`TargetTarget`, `Pm1-8`, `SpecifyTarget`, `Location`, `DynamicTarget`, `MapCenter`)，但 `SpecifyTarget`、`DynamicTarget`、`Location`、`MapCenter` 等在 Spell 类中已有对应字段，却未被使用。`Pm1-8` 队友目标也未实现。
- **影响**: 任何使用非 Self/Target 目标类型的 ACR 都无法正确执行技能。
- **修复**: 在 SlotExecutor 中实现完整的目标解析 switch，至少实现 `Target`, `Self`, `TargetTarget`, `SpecifyTarget`, `DynamicTarget` 五种。

### #4 | Spell.GetTarget() 是空桩 ✅
- **文件**: `ACR/Spell.cs:41`
- **严重度**: **Critical**
- **状态**: ✅ 已修复 — 完整实现目标类型 switch + 修正 GetDynamicsTarget 拼写
- **描述**: `GetTarget()` 方法硬编码返回 `0`，注释注明 "Phase 5.4 完整实现"。当前 SlotExecutor 未使用此方法（自行内联了目标判断），但 `IRotationEntry` 公开 API 设计了此方法供 ACR 作者使用。
- **影响**: ACR 作者调用 `spell.GetTarget()` 永远得到 `0`。
- **修复**: 实现完整的目标类型分支逻辑，与 SlotExecutor 共用一个目标解析方法。

### #5 | WebUiBridge.SendAsync 从未被调用 — 不存在 C#→JS 推送 ✅
- **文件**: `UI/WebUiBridge.cs:29` + 全局搜索
- **严重度**: **Critical**
- **状态**: ✅ 已修复 — RuntimeCore 每 500ms 推送 status 消息
- **描述**: `WebUiBridge` 有 `SendAsync()` 方法用于向所有 WebSocket 客户端广播消息。前端 `app.js` 期望接收 `status`、`uiDefinition`、`settings` 三种消息类型(见 16-19 行)。但整个 C# 代码库中 **没有任何地方调用 `SendAsync`** 推送状态数据。
- **影响**: 前端面板始终显示默认值 (`--`, `脱战`, `Idle`, `0ms`)。
- **修复**: 在 `RuntimeCore.OnTick` 或 `AIRunner.Update` 中定期调用 `_bridge.SendAsync()` 推送当前状态快照。

### #6 | Plugin.Dispose 未释放 WebUiBridge ✅
- **文件**: `Plugin.cs:52-63`
- **严重度**: **Critical**
- **状态**: ✅ 已修复 — WebUiBridge 实现 IDisposable + Dispose 中调用
- **描述**: `_uiBridge` 持有 WebSocket 连接列表，但 Dispose 中只停止 `_uiServer` 而未关闭/清理 `_uiBridge` 中的活动 WebSocket 连接。`WebUiBridge` 本身也未实现 `IDisposable`。
- **影响**: 资源泄漏 — 插件卸载时已有 WebSocket 连接可能未正确关闭。
- **修复**: (1) 让 `WebUiBridge` 实现 `IDisposable`，关闭所有 `_clients`; (2) 在 `Plugin.Dispose` 中调用 `_uiBridge.Dispose()`。

---

## 二、高危问题 (High)

### #7 | BRD 技能/Buff ID 可能对应过时游戏版本
- **文件**: `ACR/SpellsDefine.cs:8-64`, `ACR/AurasDefine.cs:8-46`, `Data/Jobs/BRDHelp.cs:13-20`
- **严重度**: **High**
- **描述**: 诗人 (BRD) 在 FFXIV 7.0 大改版中技能大幅重构。当前定义的 ID:
  - `强力射击 = 97` — 7.0 中 Heavy Shot 被替换/重命名，ID 可能已变
  - `直线射击 = 98` — 7.0 中 Straight Shot 已移除，改为 Refulgent Arrow (7409)
  - `直线射击预备 = 122` — 7.0 中此 proc buff 可能改名/变 ID
  - `风蚀 = 1201`, `毒咬 = 1200` — 7.0 中 DoT 机制大改，这两个 debuff ID 可能已变
  - `影噬箭 = 3560`, `九天连箭 = 3561`, `光阴神的礼赞凯歌 = 3562` — 7.0 中均可能变 ID
- **影响**: 若运行于 FFXIV 7.x 环境，技能执行将失败或触发错误技能。
- **修复**: 对照当前国服 FFXIV 版本的数据文件 (XivData) 重新核实所有 ID。建议在 `doc/` 中记录 ID 来源和验证日期。

### #8 | SpellHelper.IsInRange 硬编码 25f 距离 ✅
- **文件**: `ACR/SpellHelper.cs:31-36`
- **严重度**: **High**
- **状态**: ✅ 已修复 — 使用 Lumina Action 表查询技能实际射程
- **描述**: `IsInRange` 方法硬编码了 `distance <= 25f` 作为判定，但不同技能有不同射程(弓手 25y 但某些技能更短/更远，AOE 技能圆心距离也不同)。参数 `id` 被传入但未使用。
- **影响**: 近战职业 ACR 使用此方法将全部判定为"在射程内"，导致无效技能尝试。
- **修复**: 使用 ActionManager 或 Lumina 数据获取技能实际射程，与目标实际距离比较。

### #9 | EventSystem.OnPreUseAction 钩子为空 ✅
- **文件**: `Runtime/EventSystem.cs:81-86`
- **严重度**: **High**
- **状态**: ✅ 已修复 — OnPostUseAction 中增加 _onActionCompletedHandlers 触发
- **描述**: `OnPreUseAction` Hook 注册成功但回调体为空。`_onActionCompletedHandlers` 列表被定义和暴露了注册/注销方法，但 **从未有任何处理函数被注册，且在 OnPostUseAction 中也未触发此列表**（OnPostUseAction 只触发了 `_onActionUsedHandlers`）。
- **影响**: `OnActionCompleted` 钩子完全不可用。
- **修复**: 在 `OnPostUseAction` 中添加 `_onActionCompletedHandlers` 的触发代码。

### #10 | RenderProcess.Dispose 未释放 Rpc ✅
- **文件**: `UI/RenderProcess.cs:140` + `UI/RenderProcess.cs:39`
- **严重度**: **High**
- **状态**: ✅ 已修复 — Dispose 中增加 Rpc?.Dispose()
- **描述**: `Rpc` 在构造函数中创建 (`new HiAuRoRpc(...)`)，在 `EnsureAlive` 崩溃处理时 dispose (line 61)。但正常 `Dispose() => Stop()` 流程不会调用 `Rpc?.Dispose()`。导致 IPC 共享内存通道泄漏。
- **影响**: 共享内存资源泄漏，多次启动停止后可能耗尽通道名。
- **修复**: `Dispose` 方法中增加 `Rpc?.Dispose()`。

### #11 | Data.Party 中 OmenTools 类型与 Dalamud ClientState IPlayerCharacter 混用
- **文件**: `Data/Data.Party.cs:34, 53-58`
- **严重度**: **High**
- **描述**: 
  - `PartyMemberInfo.Player` 声明为 `IPlayerCharacter?` (OmenTools 版本) — 正确
  - 但 `member.GameObject` 返回 Dalamud 原生 `Dalamud.Game.ClientState.Objects.Types.IGameObject` (未被 GlobalUsings 别名覆盖，因为别名只覆盖不带命名空间的类型名)
  - `member.GameObject.Address` 工作正常（原生类型有 `Address`）
  - `DService.Instance().ObjectTable.CreateObjectReference(...)` 转换为 OmenTools 类型 — 正确
- **实际风险**: 如果有代码直接使用 `member.GameObject` 而不转换（检查其属性如 `ObjectKind`、`EntityID`），会因为两个 IGameObject 接口的方法签名差异而出错。当前代码避免了此问题，但 **脆弱的类型约定容易被后续维护者打破**。
- **修复**: 在 Data.Party 中增加显式注释说明 OmenTools/Dalamud 类型边界，或将 PartyList 的取值显式封装。

### #12 | CountDownHandler.Update 是空实现 ✅
- **文件**: `Runtime/CountDownHandler.cs:19-22`
- **严重度**: **High**
- **状态**: ✅ 已修复 — 实现超时检测+UseAction 技能执行
- **描述**: `Update(int battleTimeMs)` 方法体为空。`AddAction` 已能注册行为，`Reset` 能清空，但 `Update` 未实现检查 `_actions` 并执行到时行为。
- **影响**: 开怪前倒计时阶段的预读技能/爆发药水等行为完全不可用。
- **修复**: 实现 `Update` 中的超时检测和技能执行逻辑。

---

## 三、中危问题 (Medium)

### #13 | UI/CefManager 是空壳 ✅
- **文件**: `UI/CefManager.cs:10-22`
- **严重度**: **Medium**
- **状态**: ✅ 已修复 — 接入 RenderProcess，Init 中创建并启动渲染进程
- **描述**: `CefManager` 的 `Init` 和 `Shutdown` 方法除设置 `IsRunning` 标志外无任何实际操作。注释标注 "Phase 5.3+ 实现"。
- **影响**: CEF 渲染进程永远不会被启动，`OverlayWindow` 和 `SharedTextureHandler` 即便存在也无法工作。
- **修复**: Phase 5.3 时实现 — 但当前依赖的 `RenderProcess` 类已经存在且功能完备，`CefManager` 应实际调用 `RenderProcess`。

### #14 | UI/IconServer 是空实现
- **文件**: `UI/IconServer.cs:10-15`
- **严重度**: **Medium**
- **描述**: `GetIcon` 方法总是返回 `null`。前端技能图标将无法显示。
- **影响**: Web 面板显示不出技能图标。
- **修复**: 实现 `DService.Instance().TextureProvider.GetFromGameIcon()` → PNG 编码。

### #15 | BRD 演示 ACR 功能不完整
- **文件**: `Jobs/BRD/*.cs`
- **严重度**: **Medium**
- **描述**: BRD 打样只有 1 个 GCD (强力射击) + 1 个 oGCD (失血箭) + 最简起手。缺少：
  - 风蚀/毒咬 DoT 刷新逻辑
  - 贤者/军神/放浪神 三首歌曲循环
  - 猛者强击/战斗之声/纷乱箭/光明神的最终乐章 buff 对齐
  - AOE 判定 (死亡箭雨/影噬箭)
  - 鹰眼/完美音调等进阶技能
  - `IRotationEventHandler` 实现
- **影响**: 虽然整体标记为 MVP 打样，但 BRD 的核心循环(DoT+歌曲)是职业基本特征。当前 demo 会让作者误以为只需写 GCD+oGCD 即可。
- **修复**: 至少补上 DoT 刷新 (风蚀+毒咬) 和歌曲循环 (贤者/军神)，或明确文档说明"仅用于 Demo"。

### #16 | PluginConfig 缺少关键配置字段 ✅
- **文件**: `Infrastructure/PluginConfig.cs:8-21`
- **严重度**: **Medium**
- **状态**: ✅ 已修复 — 增加 ActionQueueInMs/MaxAbilityTimesInGcd/AoeCount/AttackRange
- **描述**: 前端 `settings.html` 引用了 `ActionQueueInMs`、`MaxAbilityTimesInGcd`、`AoeCount`、`AttackRange` 四个设置字段，但 `PluginConfig` 类只定义了 `Version`、`DebugEnabled`、`LastSeenPluginVersion`、`LoadCount`。这四个前端可调参数没有对应的 C# 属性。
- **影响**: 设置页面的值修改后无法持久化保存，也无法被 Runtime 读取使用。
- **修复**: 在 `PluginConfig` 中增加对应字段。

### #17 | Data.Objects.IsHostile 判定不够严格
- **文件**: `Data/Data.Objects.cs:95-98`
- **严重度**: **Medium**
- **描述**: 此方法只检查 `IsTargetable && IsDead != true`，但某些单人副本友方 NPC 也满足此条件（敌意 NPC 的标记 `BattleNpcSubKind.Enemy` 未检查）。
- **影响**: 单人副本中误将友方 NPC 识别为敌人。
- **修复**: 追加检查 `BattleNpcSubKind` 是否为 `Enemy`(对 `IBattleNPC` 有效)。

### #18 | AuraHelper 中 buff 源 ID 默认值使用哨兵 0xE0000000
- **文件**: `ACR/AuraHelper.cs:25, 28` + `Data/Data.Self.cs:31`
- **严重度**: **Medium**
- **描述**: `GetAuraTimeLeft` 和 `HasStatus` 使用 `0xE0000000` 作为"不检查来源"的哨兵值。这个魔数的含义不明确，且如果游戏返回的实际 SourceID 恰好等于此值（极端但可能），则逻辑错误。
- **影响**: 极端情况下可能漏判 buff 来源。
- **修复**: 增加常量定义和注释说明，或改用可空参数 `uint?`。

### #19 | HotkeyHelper._resolvers 列表无并发保护
- **文件**: `ACR/HotkeyHelper.cs:8-9`
- **严重度**: **Medium**
- **描述**: `_resolvers` 和 `_keyBindings` 是静态列表/字典，无锁保护。如果 UI 线程修改绑定同时 FrameworkManager 线程调用 `HandleKeyPress`，存在竞态。
- **影响**: 当前因按键处理链路未接入（不存在跨线程调用场景），暂无实际影响。但 Phase 5.2 实现后可能出现竞态。
- **修复**: 使用 `ConcurrentDictionary` 或加锁。

### #20 | WebUiBridge._clients 列表无并发保护 ✅
- **文件**: `UI/WebUiBridge.cs:13, 36-58`
- **严重度**: **Medium**
- **状态**: ✅ 已修复 — 添加 lock 保护 _clients
- **描述**: `_clients` 在 `HandleConnection` (异步) 和 `SendAsync` (可能多线程调用) 之间无锁。虽然是 async/await 单线程模型，但多个 WebSocket 连接并发加入/移除时可能出错。
- **影响**: 多客户端连接时偶尔丢连接或死连接不清理。
- **修复**: 使用 `ConcurrentDictionary` 或 `lock` 保护 `_clients`。

### #21 | RuntimeCore.OnTick 使用 Dalamud IFramework 参数但未使用
- **文件**: `Runtime/RuntimeCore.cs:25`
- **严重度**: **Medium**
- **描述**: OmenTools FrameworkManager 的回调期望 `Action<IFramework>` 签名，参数已正确匹配。但 `IFramework` 对象提供了 `UpdateDelta` 等有用属性（用于 DeltaTime 更新），当前未使用。
- **影响**: `Data.Combat.DeltaTime` 来源于 `GameState.DeltaTime` (服务端时间增量)，可能与帧 Delta 不同步。
- **修复**: 评估是否需要从 IFramework 获取客户端帧增量而非服务端时间。

---

## 四、低危问题 (Low)

### #22 | Spell 类拼写错误: GetDynamicsTarget ✅
- **文件**: `ACR/Spell.cs:29`
- **严重度**: **Low**
- **状态**: ✅ 已修复 — 重命名为 GetDynamicTarget
- **描述**: 属性名为 `GetDynamicsTarget`，应为 `GetDynamicTarget`（多了一个 `s`）。方法名 `GetTarget()` 也标注 "Phase 5.4 完整实现" 但永远返回 0。
- **修复**: 重命名为 `GetDynamicTarget`，并实现完整的 `GetTarget()`。

### #23 | 冗余 using 指令
- **文件**: `ACR/AuraHelper.cs:1`, `ACR/SpellHelper.cs:1`, `ACR/TargetHelper.cs:1`
- **严重度**: **Low**
- **描述**: `using OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds;` 已在 `GlobalUsings.cs` 中通过多条 `global using` 覆盖（IGameObject, IBattleChara 等），但个别文件仍显式 using 了完整命名空间。不影响编译但多余。
- **修复**: 移除这些文件中多余的 using 指令，或保留以增强可读性（任选一种风格保持一致）。

### #24 | Data.Objects.cs 别名 IGameObj 冗余
- **文件**: `Data/Data.Objects.cs:3`
- **严重度**: **Low**
- **描述**: `using IGameObj = ...IGameObject;` 创建别名，但 `GlobalUsings.cs:12` 已将 `IGameObject` 全局导入。`IGameObj` 只在该文件内使用，与其他文件不一致。
- **修复**: 统一使用 `IGameObject` 或所有文件统一使用 `IGameObj`。

### #25 | BRDHelp 命名空间不一致
- **文件**: `Data/Jobs/BRDHelp.cs:4`
- **严重度**: **Low**
- **描述**: BRD 辅助类放在 `HiAuRo.Data` 命名空间下，但 BRD 具体实现在 `HiAuRo.Jobs.BRD` 下。逻辑上 `BRDHelp` 应属于 `HiAuRo.Jobs.BRD` 或 `HiAuRo.Data.BRD`。
- **影响**: 无功能影响，仅组织一致性。
- **修复**: 考虑移到 `HiAuRo.Jobs.BRD` 或创建 `HiAuRo.Data.Jobs`。

### #26 | HiAuRo.json 缺少 RepoUrl
- **文件**: `HiAuRo.json:8`
- **严重度**: **Low**
- **描述**: `RepoUrl` 字段为空字符串。Dalamud 使用此字段在插件管理器中显示项目链接。
- **修复**: 填入实际仓库 URL。

### #27 | app.js send() 函数签名问题
- **文件**: `UI/web/app.js:30-34`
- **严重度**: **Low**
- **描述**: `send('toggleACR')` 发送 `{ type: 'toggleACR' }`（无 data 字段）。`send('saveSetting', { path, value })` 发送 `{ type: 'saveSetting', path: ..., value: ... }` — 但其他调用如 `send('settingChanged', ...)` 通过 destructuring 传入 data。格式不完全统一。
- **影响**: C# 端需要同时处理 `data` 在根级别和在 `data` 子对象中两种格式。
- **修复**: 统一消息格式为 `{ type, data }`，`send` 函数始终将额外参数放入 `data` 字段。

### #28 | OverlayWindow 未与 Plugin 集成
- **文件**: `UI/OverlayWindow.cs` + `Plugin.cs`
- **严重度**: **Low**
- **描述**: `OverlayWindow` 已实现（继承 Dalamud Window），但 Plugin.cs 中未创建实例/注册到 `PluginInterface.UiBuilder`。
- **影响**: ImGui 悬浮窗永远不会显示。
- **修复**: 在 Plugin 中创建 `OverlayWindow` 实例并注册 `_pluginInterface.UiBuilder.Draw += overlay.Draw`。

### #29 | Coroutine.Update 使用 Environment.TickCount64
- **文件**: `Runtime/Coroutine.cs:47,57,75`
- **严重度**: **Low**
- **描述**: 使用 `Environment.TickCount64` 作为时间基准。这是系统启动毫秒计数，精度 ~1-16ms（取决于系统定时器分辨率），对于 GCD 等待（数百毫秒）足够，但对动画锁等精确等待可能不够。
- **影响**: 动画锁等待可能有 1-2 帧误差。
- **修复**: 如需要更高精度，使用 `Stopwatch.GetTimestamp()` + `Stopwatch.Frequency`。

### #30 | SpellQueue 未被 AILoop_Normal 使用
- **文件**: `Runtime/SpellQueue.cs` + `Runtime/AILoop_Normal.cs`
- **严重度**: **Low**
- **描述**: `AIRunner` 持有 `SpellQueue` 并用于存储追加序列的 Slot，但 `AILoop_Normal.GetNextSlot()` 在 GCD 不可用时直接返回 null，不尝试从队列获取待处理的 oGCD。正常流程中 SpellQueue 通过 `SlotExecutor` 的 `AppendedSequence` 填入，然后在下一帧由 `AIRunner.Update` 先检查 `SpellQueue.HasPending()`。逻辑层面正确，但 AILoop_Normal 可以更智能地利用队列。
- **影响**: 无功能缺失，当前逻辑可通过。
- **修复**: N/A（可选优化）。

### #31 | Plugin.cs 存在私有的 LogDebug 方法未被调用
- **文件**: `Plugin.cs:92-103`
- **严重度**: **Low**
- **描述**: 定义了两个 `LogDebug` 重载但没有任何地方调用它们。
- **影响**: 死代码。
- **修复**: 要么在关键位置增加调试日志调用，要么移除。

---

## 五、类型引用问题 (Type Reference Issues)

### #32 | GlobalUsings 假设 OmenTools 命名空间存在
- **文件**: `GlobalUsings.cs:5-21`
- **严重度**: **Medium** (编译期)
- **描述**: 大量 `global using` 引用 OmenTools 的内部命名空间路径。需要确认以下在 OmenTools 中确实存在：
  - `OmenTools.Dalamud.Services.ObjectTable.Abstractions.IObjectTable`
  - `OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds.IGameObject`
  - `OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds.IPlayerCharacter`
  - `OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds.IBattleChara`
  - `OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds.IBattleNPC`
  - `OmenTools.Dalamud.Services.StatusList.Implementations.StatusList`
  - `OmenTools.Global.Globals` (静态导入)
  - `OmenTools.Info.Game.Data.Addons` (静态导入)
- **影响**: 如果 OmenTools 版本更新改变了命名空间，编译将失败。
- **修复**: 验证 OmenTools 源码 (`../OmenTools/`) 确认这些路径在当前版本中存在。

---

## 六、线程安全问题 (Thread Safety)

### #33 | RuntimeCore/ACRLifecycle/AIRunner 无并发安全措施
- **文件**: `Runtime/RuntimeCore.cs`, `Runtime/ACRLifecycle.cs`, `Runtime/AIRunner.cs`
- **描述**: 整个 Runtime 系统假定在游戏主线程单线程运行（OmenTools FrameworkManager 保证）。但如果将来从 WebSocket 线程或 IPC 线程触发起停，会产生竞态。
- **严重度**: **Low** (当前)
- **修复**: 在 `RuntimeCore.Start/Stop` 中增加状态保护，或文档化"所有 Runtime 操作必须在主线程执行"。

### #34 | WebUiBridge.HandleConnection buffer 大小固定 4096
- **文件**: `UI/WebUiBridge.cs:65`
- **描述**: `buffer[4096]` 固定大小。单条 WebSocket 消息超过 4KB 时会截断。UI 控件列表较大时可能超限。
- **严重度**: **Low**
- **修复**: 使用分段读取或更大的 buffer，或改用 ArrayPool。

---

## 七、API 使用问题 (API Misuse)

### #35 | Data.Combat 每属性重复获取 DService.Instance().Condition
- **文件**: `Data/Data.Combat.cs:26-38`
- **描述**: 6 个属性各自调用 `DService.Instance().Condition is { } cond && cond[ConditionFlag.X]`。`DService.Instance().Condition` 每次调用可能有开销。`IsCasting`, `InCombat` 等高频读取属性会加剧。
- **严重度**: **Low**
- **修复**: 缓存 `Condition` 引用或统一在一次更新批次中读取。

### #36 | Data.Objects.Refresh 遍历全部 ObjectTable 每帧
- **文件**: `Data/Data.Objects.cs:21-86` + `Runtime/AIRunner.cs:89`
- **描述**: 每帧遍历 `DService.Instance().ObjectTable` 全部对象（战斗中可达 100+），进行类型判断和列表分配。虽然是必要操作，但可以在 `ClearAll` 中使用 `new List<IGameObj>(capacity)` 预分配减少 GC。
- **严重度**: **Low**
- **修复**: 清空列表时设置合理初始容量。

### #37 | SlotExecutor 硬编码 ActionType.Action ✅
- **文件**: `Runtime/SlotExecutor.cs:57`
- **严重度**: **Medium**
- **状态**: ✅ 已修复 — 添加 SpellCategoryToActionType() 映射
- **修复**: 根据 `spell.SpellCategory` 映射到正确的 `ActionType`(如 GeneralAction、Item 等)。

---

## 八、配置文件问题 (Configuration)

### #38 | PluginConfig 持久化依赖 Dalamud 但缺少迁移逻辑
- **文件**: `Infrastructure/PluginConfig.cs:8-21` + `Plugin.cs:67-86`
- **描述**: `Version` 字段默认为 1，但无版本迁移逻辑。如果未来 v2 增加了新字段，旧配置文件可能反序列化失败或丢失新字段默认值。
- **严重度**: **Low** (当前)
- **修复**: 增加 `Migrate` 方法，根据 `Version` 值执行渐进式迁移。

### #39 | PluginConfig 缺少对 HiAuRo.json 版本一致性的校验
- **文件**: `Plugin.cs:71-74` + `HiAuRo.json:7`
- **描述**: `LastSeenPluginVersion` 从 `Manifest.AssemblyVersion` 获取，但 `HiAuRo.json` 的 `AssemblyVersion` 标记为 `0.1.0.0`，而 csproj 设置为 `0.1.0`。两者格式不同（三段 vs 四段）。
- **影响**: `AssemblyVersion.ToString()` 返回四段版本号，与 csproj 的三段可能不一致。
- **严重度**: **Low**
- **修复**: 统一版本号格式，或在比较时处理格式差异。

---

## 九、UI/前端问题 (Web Frontend)

### #40 | 无离线/断线重连状态保存
- **文件**: `UI/web/app.js:8-11`
- **描述**: WebSocket 断线后自动重连 (2秒延迟)，但重连后前端状态重置为默认值，用户在设置页面的未保存更改丢失。
- **严重度**: **Low**
- **修复**: 重连后重新向 C# 请求当前状态快照。

### #41 | 前端未使用 CSP 或安全头
- **文件**: `UI/WebUiServer.cs:35-46`
- **描述**: Kestrel 静态文件服务器未配置 Content-Security-Policy 等安全头。本地 localhost 环境风险较低，但仍建议添加基本安全头。
- **严重度**: **Low**
- **修复**: 添加 `app.Use(async (ctx, next) => { ctx.Response.Headers["X-Content-Type-Options"] = "nosniff"; await next(); })`。

---

## 十、总结统计

| 严重度 | 数量 |
|--------|------|
| Critical | 6 |
| High | 6 |
| Medium | 9 |
| Low | 20 |
| **总计** | **41** |

| 类别 | 数量最多的类型 |
|------|----------------|
| 缺失实现 (stubs/空壳) | 7 |
| 逻辑错误 | 5 |
| 资源泄漏/生命周期 | 4 |
| 空值安全 | 2 |
| 线程安全 | 3 |
| API 使用 | 3 |
| 配置问题 | 3 |
| 类型引用 | 1 |
| 前端/UI | 3 |
| 代码风格/冗余 | 4 |
| 数据正确性 (ID) | 1 |
| 其他 | 5 |
