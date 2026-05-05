# 执行轴 (Execution Axis — Phase 6)

## 核心概念

执行轴是一个完整的 AE 风格触发树 AST，通过 `async Task` 驱动，对齐 AE TriggerlineData。

## 异步运行模型

```
战斗开始 → ExecutionAxis.Start() → async void RunTreeAsync()
  → await Root.Execute(ctx)   ← 一次性 Task 调用，与 AE 完全一致
  → Root Task 完成 → 树终结
战斗结束 → ExecutionAxis.Stop() → CTS.Cancel()
```

## AST 节点类型

| 节点 | 求值方式 |
|------|---------|
| TreeSequence | `await child.Execute()` 依次，IgnoreNodeResult 不短路 |
| TreeParallel | `Task.WhenAll` / `Task.WhenAny`（竞赛模式） |
| TreeSelect | 依次尝试，失败时仍返回 true |
| TreeLoop | 外层循环 Times 次内层全子节点 |
| TreeCondNode | CheckOnce 立即检查；等待模式 `await WaitCond()` |
| TreeActionNode | 同步执行所有 ITriggerAction |
| TreeDelayNode | `await Coroutine.DelayAsync(seconds)` |
| TreeScriptNode | Roslyn 动态编译 → ITriggerScript.Check() |

## WaitCond 机制

- CondNode 等待时注册 `TaskCompletionSource<bool>` 到 `_waitingConds`
- 每帧 `CheckWaitingConds()` 遍历 → 条件满足 → TCS.SetResult → await 返回 → 树继续

## 辅助轴

AssistAxis 与执行轴共享同一套 AST 引擎：
- 始终运行（独立于 ModeSwitch）
- 从 `AssistTimelines/{副本ID}.txt` 加载
- 与执行轴互不影响

## JSON 加载

```csharp
// AE 格式 JSON → TriggerNode AST
ExecutionJsonLoader.FromJson(json) → Root

// ACR 自定义类型注册
ExecutionJsonLoader.RegisterFromRotation(rotation)
ExecutionJsonLoader.RegisterConditionType(fullName, type)
```

## 脚本编译

Roslyn 动态编译 JSON 中的 C# 脚本 → `ITriggerScript` → `Check(condParams)`

## 关键文件

| 文件 | 作用 |
|------|------|
| ExecutionNode.cs | AST 节点定义 + 异步求值 |
| ExecutionAxis.cs | 主控制器（WaitCond + 启停 + 输出） |
| ExecutionJson.cs | AE 格式 JSON 解析 + 类型注册 |
| ScriptCompiler.cs | C# 脚本动态编译 |
| AssistAxis.cs | 辅助轴 |
