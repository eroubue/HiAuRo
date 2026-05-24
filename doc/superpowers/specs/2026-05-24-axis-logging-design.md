# 轴系统 Verbose 日志增强设计

## 概述

为轴系统（ExecutionAxis/AssistAxis）增加详细的 Verbose 级别执行日志，便于在全面测试时快速定位问题点。

## 改动范围

### 1. `Hi.cs` — 添加 Verbose 方法

```csharp
public static void Verbose(string msg) =>
    DService.Instance().Log.Verbose($"[HiAuRo] {msg}");
```

`DService.Instance().Log.Verbose()` 底层直接由 Dalamud 的 Serilog 支持，在配置中可独立控制是否输出。

### 2. `ExecutionNode.cs` — 每个节点类型加入 Verbose 跟踪

| 节点 | 日志点位 | 信息 |
|------|---------|------|
| TriggerNode.Execute | 入口 + 出口 | `[ExecNode] {DisplayName}(#{Id}) 开始执行` / `执行完毕` |
| TreeSequence | 循环每个子节点 | `[ExecNode] Sequence({Tag}) → 子节点 {i}/{n}: {childName}` / `短路退出` |
| TreeParallel | 启动/完成 | `[ExecNode] Parallel({Tag}) 启动 {n} 子节点 [AnyReturn={bool}]` / `全部完成` |
| TreeSelect | 每个尝试 | `[ExecNode] Select({Tag}) → 尝试子节点 {i}: {childName} → {(true/false)}` |
| TreeLoop | 外层循环 | `[ExecNode] Loop({Tag}) 第 {i}/{Times} 轮` |
| TreeCondNode | 条件求值 | `[ExecNode] Cond({Tag}) → 条件 [{CondLogicType}] 求值: {每个条件 Handle() 结果}` |
| TreeCondNode | 等待模式 | `[ExecNode] Cond({Tag}) 进入等待模式 (WaitCond 注册)` / `WaitCond 唤醒: {true/false}` |
| TreeScriptNode | OnlyCheck | `[ExecNode] Script({Tag}) OnlyCheck: {Check(null)=true/false}` |
| TreeScriptNode | 等待模式 | `[ExecNode] Script({Tag}) 进入等待模式` / `EvaluateConds: {true/false}` / `EvaluateForEvent: {true/false}` |
| TreeActionNode | 每个动作 | `[ExecNode] Action({Tag}) → 执行 {action.GetType().Name}` |
| TreeDelayNode | 开始/完成 | `[ExecNode] Delay({Tag}) 延迟 {Delay}s 开始` / `延迟完成` |
| TreePrintDebugInfoNode | 执行 | `[ExecNode] PrintDebug({Tag}) → {Info}` |

### 3. `ExecutionAxis.cs` — 生命周期 + WaitCond 跟踪

| 点位 | 信息 |
|------|------|
| Start() | `[ExecAxis] 触发树启动: {TimelineName}` |
| Stop() | `[ExecAxis] 触发树停止` (所有等待条件取消时逐条) |
| WaitCond(node) | `[ExecAxis] WaitCond 注册: {node.DisplayName}(#{node.Id})` |
| CheckWaitingConds() | `[ExecAxis] CheckWaitingConds: 检查 {n} 个挂起条件` / 逐条 `{name} → {(met/skip)}` |
| UseCondParams() | `[ExecAxis] UseCondParams: {condParams.GetType().Name}` / 匹配结果 |
| tcs.TrySetResult | `[ExecAxis] WaitCond 唤醒: {node.DisplayName}` |
| tcs.TrySetResult(false) on Stop | `[ExecAxis] WaitCond 取消(停止): {node.DisplayName}` |
| Update() | `[ExecAxis] Update: forceSpell={..}, paused={..}` |
| LoadFromJson | 已有 Information 日志，不动 |

### 4. `AssistAxis.cs` — 与 ExecutionAxis 对称的跟踪

同 ExecutionAxis 的所有点位，前缀改为 `[AssistAxis]`。

## 不变的内容

- 不修改任何业务逻辑
- 不改动现有 Info/Debug/Warning/Error 日志
- 不引入新的类或接口
- 不添加新的配置文件或设置项

## 验证方法

编译通过后，在 Dalamud 日志窗口中观察 `[HiAuRo]` `[ExecNode]` `[ExecAxis]` `[AssistAxis]` 前缀的 Verbose 日志输出。
