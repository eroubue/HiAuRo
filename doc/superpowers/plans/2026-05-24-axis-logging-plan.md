# 轴系统 Verbose 日志增强 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use subagent-driven-development or executing-plans to implement step-by-step. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 为 ExecutionAxis/AssistAxis/ExecutionNode 添加全套 Verbose 级别执行日志，便于测试时定位问题

**Architecture:** 激活已有的 `Hi` 静态封装类（`Hi.Verbose()`），在轴树执行的所有关键节点插入 Verbose 日志调用，不改动任何业务逻辑

**Tech Stack:** C# 13, .NET 10, Serilog (via Dalamud DService)

---

### Task 1: Hi.cs — 添加 Verbose 方法

**Files:**
- Modify: `HiAuRo/Infrastructure/Hi.cs:10-13`

- [ ] **Step 1: 添加 Hi.Verbose() 方法**

在 `Debug` 方法后面插入：

```csharp
/// <summary>输出详细日志（Verbose）</summary>
public static void Verbose(string msg) =>
    DService.Instance().Log.Verbose($"[HiAuRo] {msg}");
```

- [ ] **Step 2: 编译验证**

```bash
dotnet build HiAuRo/HiAuRo.csproj -nologo
```

Expected: Build succeeded, 0 warnings

- [ ] **Step 3: 提交**

```bash
git add HiAuRo/Infrastructure/Hi.cs
git commit -m "feat: 添加 Hi.Verbose() 便捷日志方法"
```

---

### Task 2: ExecutionAxis.cs — 添加 Verbose 跟踪

**Files:**
- Modify: `HiAuRo/Execution/ExecutionAxis.cs`

在以下点位插入 `Hi.Verbose(...)` 调用（所有已有日志不动）：

- [ ] **Step 1: Start() — 已在第 86 行 `_ = RunTreeAsync(...)` 之前加**

在 `IsRunning = true` 赋值后，`_cts` 初始化前，添加：

```csharp
Hi.Verbose($"[ExecAxis] Start: {TimelineName}");
```

- [ ] **Step 2: Stop() — 等待条件取消时逐条记录**

在 foreach 循环内（第 98-100 行），添加：

```csharp
Hi.Verbose($"[ExecAxis] Stop: 取消 WaitCond {node.DisplayName}(#{node.Id})");
```

- [ ] **Step 3: WaitCond() — 注册记录**

在 `tcs` 创建后、return 前，添加：

```csharp
Hi.Verbose($"[ExecAxis] WaitCond 注册: {node.DisplayName}(#{node.Id})");
```

- [ ] **Step 4: CheckWaitingConds() — 检查详细**

方法入口（第 136 行）添加检查总数：
```csharp
Hi.Verbose($"[ExecAxis] CheckWaitingConds: 检查 {_waitingConds.Count} 个挂起条件");
```

在 `toWake.Add(node)` 处（第 148 行）添加唤醒记录：
```csharp
Hi.Verbose($"[ExecAxis] CheckWaitingConds: 条件满足 → {node.DisplayName}(#{node.Id})");
```

tcs.TrySetResult(true) 前（第 156 行）添加：
```csharp
Hi.Verbose($"[ExecAxis] WaitCond 唤醒: {node.DisplayName}(#{node.Id})");
```

- [ ] **Step 5: UseCondParams() — 事件驱动匹配**

方法入口（第 162 行）添加：
```csharp
Hi.Verbose($"[ExecAxis] UseCondParams: {condParams.GetType().Name} (挂起 {_waitingConds.Count} 个)");
```

`toWake.Add(node)` 处添加：
```csharp
Hi.Verbose($"[ExecAxis] UseCondParams 匹配 → {node.DisplayName}(#{node.Id})");
```

- [ ] **Step 6: Update() — 决策记录**

`Stop()` 调用前（第 248 行）添加：
```csharp
Hi.Verbose($"[ExecAxis] Update: 副本切换 {_previousTerritoryId} → {territory}，重新加载");
```

`CheckWaitingConds()` 调用前（第 252 行）添加：
```csharp
Hi.Verbose($"[ExecAxis] Update: batteTimeMs={battleTimeMs}");
```

`_forceSpell` 判断分支（第 256 行）加：
```csharp
Hi.Verbose($"[ExecAxis] Update: 强制技能 {_forceSpell.Name}");
```

`_paused` 判断分支（第 266 行）加：
```csharp
Hi.Verbose($"[ExecAxis] Update: 暂停 ACR");
```

- [ ] **Step 7: 编译验证**

```bash
dotnet build HiAuRo/HiAuRo.csproj -nologo
```

Expected: Build succeeded

- [ ] **Step 8: 提交**

```bash
git add HiAuRo/Execution/ExecutionAxis.cs
git commit -m "feat: ExecutionAxis 添加 Verbose 日志跟踪"
```

---

### Task 3: AssistAxis.cs — 添加 Verbose 跟踪

**Files:**
- Modify: `HiAuRo/Execution/AssistAxis.cs`

与 ExecutionAxis 对称，所有前缀改为 `[AssistAxis]`。

- [ ] **Step 1: Start() — 启动记录**

```csharp
Hi.Verbose($"[AssistAxis] Start: {TimelineName}");
```

- [ ] **Step 2: Stop() — 取消等待条件**

```csharp
Hi.Verbose($"[AssistAxis] Stop: 取消 WaitCond {node.DisplayName}(#{node.Id})");
```

- [ ] **Step 3: WaitCond() — 注册记录**

```csharp
Hi.Verbose($"[AssistAxis] WaitCond 注册: {node.DisplayName}(#{node.Id})");
```

- [ ] **Step 4: CheckWaitingConds() — 检查/唤醒**

方法入口：检查总数

`TryRemove` 成功处：唤醒记录

- [ ] **Step 5: Update() — 决策记录**

副本切换、CheckWaitingConds、forceSpell、paused 各处

- [ ] **Step 6: 编译验证**

```bash
dotnet build HiAuRo/HiAuRo.csproj -nologo
```

- [ ] **Step 7: 提交**

```bash
git add HiAuRo/Execution/AssistAxis.cs
git commit -m "feat: AssistAxis 添加 Verbose 日志跟踪"
```

---

### Task 4: ExecutionNode.cs — 各节点 Verbose 跟踪

**Files:**
- Modify: `HiAuRo/Execution/ExecutionNode.cs`

- [ ] **Step 1: TriggerNode.Execute — 基类入口/出口加日志**

在 `OnExecute(ctx)` 调用前后加 Verbose：

```csharp
protected override async Task<bool> OnExecute(EvalContext ctx)
{
    // 已存在: if (!Enable) return true;
    // 已存在: if (ctx.IsDisposed) return false;
    Hi.Verbose($"[ExecNode] {DisplayName}(#{Id}) 开始执行");
    var result = await OnExecute(ctx);
    Hi.Verbose($"[ExecNode] {DisplayName}(#{Id}) 执行完毕 → {result}");
    return result;
}
```

等等——`TriggerNode.Execute` 不是 `OnExecute`，它是基类的 `Execute` 方法。让我重新看代码结构：

```csharp
public async Task<bool> Execute(EvalContext ctx)
{
    if (!Enable) return true;
    if (ctx.IsDisposed) return false;
    return await OnExecute(ctx);
}
```

这是 base `Execute`，不是 virtual。这里加日志会在每个节点执行时都打印入口和出口，非常合适。

```csharp
public async Task<bool> Execute(EvalContext ctx)
{
    if (!Enable) return true;
    if (ctx.IsDisposed) return false;
    Hi.Verbose($"[ExecNode] ▶ {DisplayName}(#{Id}) 进入");
    var result = await OnExecute(ctx);
    Hi.Verbose($"[ExecNode] ◀ {DisplayName}(#{Id}) 退出 → {(result ? "成功" : "失败")}");
    return result;
}
```

- [ ] **Step 2: TreeSequence.OnExecute — 子节点遍历**

```csharp
protected override async Task<bool> OnExecute(EvalContext ctx)
{
    for (int i = 0; i < Childs.Count; i++)
    {
        var child = Childs[i];
        if (!child.Enable) continue;
        if (ctx.IsDisposed) return false;
        Hi.Verbose($"[ExecNode] Sequence({Tag}) → 子节点 [{i}/{Childs.Count}] {child.DisplayName}(#{child.Id})");
        var result = await child.Execute(ctx);
        if (!result && !IgnoreNodeResult)
        {
            Hi.Verbose($"[ExecNode] Sequence({Tag}) 短路退出 (IgnoreNodeResult=false)");
            return false;
        }
    }
    return true;
}
```

- [ ] **Step 3: TreeParallel.OnExecute — 并行执行**

```csharp
protected override async Task<bool> OnExecute(EvalContext ctx)
{
    var tasks = Childs.Where(c => c.Enable).Select(c => c.Execute(ctx)).ToList();
    Hi.Verbose($"[ExecNode] Parallel({Tag}) 启动 {tasks.Count} 子节点 [AnyReturn={AnyReturn}]");
    if (tasks.Count == 0) return true;
    if (AnyReturn)
    {
        var winner = await Task.WhenAny(tasks);
        Hi.Verbose($"[ExecNode] Parallel({Tag}) 竞赛胜出");
    }
    else
    {
        await Task.WhenAll(tasks);
        Hi.Verbose($"[ExecNode] Parallel({Tag}) 全部完成");
    }
    return true;
}
```

- [ ] **Step 4: TreeSelect.OnExecute — 依次尝试**

```csharp
protected override async Task<bool> OnExecute(EvalContext ctx)
{
    foreach (var child in Childs)
    {
        if (!child.Enable) continue;
        Hi.Verbose($"[ExecNode] Select({Tag}) → 尝试 {child.DisplayName}(#{child.Id})");
        if (await child.Execute(ctx))
        {
            Hi.Verbose($"[ExecNode] Select({Tag}) → {child.DisplayName} 成功");
            return true;
        }
        Hi.Verbose($"[ExecNode] Select({Tag}) → {child.DisplayName} 失败");
    }
    return true;
}
```

- [ ] **Step 5: TreeLoop.OnExecute — 循环轮次**

```csharp
protected override async Task<bool> OnExecute(EvalContext ctx)
{
    for (int i = 0; i < Times; i++)
    {
        Hi.Verbose($"[ExecNode] Loop({Tag}) 第 {i+1}/{Times} 轮");
        foreach (var child in Childs)
        {
            if (!child.Enable) continue;
            if (ctx.IsDisposed) return false;
            await child.Execute(ctx);
        }
    }
    return true;
}
```

- [ ] **Step 6: TreeCondNode.OnExecute — 条件求值 + 等待**

CheckOnce 分支：
```csharp
if (CheckOnce)
{
    var met = EvaluateConds();
    Hi.Verbose($"[ExecNode] Cond({Tag}) CheckOnce: {(met ? "满足" : "不满足")} (反转={ReverseResult})");
    return met;
}
```

等待模式分支：
```csharp
Hi.Verbose($"[ExecNode] Cond({Tag}) 进入等待模式 (挂起 {ExecutionAxis.Instance._waitingConds.Count+1} 个条件)");
var result = await ExecutionAxis.Instance.WaitCond(this);
Hi.Verbose($"[ExecNode] Cond({Tag}) WaitCond 返回: {(result ? "满足" : "取消")}");
return result;
```

Wait... `_waitingConds` is private in ExecutionAxis. So I can't access `.Count`. Let me just keep it simple:
```csharp
Hi.Verbose($"[ExecNode] Cond({Tag}) 进入等待模式");
var result = await ExecutionAxis.Instance.WaitCond(this);
Hi.Verbose($"[ExecNode] Cond({Tag}) WaitCond 返回: {(result ? "满足" : "取消")}");
return result;
```

- [ ] **Step 7: TreeCondNode.EvaluateConds — 条件详情**

```csharp
public bool EvaluateConds()
{
    if (TriggerConds.Count == 0) return true;
    
    var results = new List<string>();
    bool met = CondLogicType == CondLogicType.And
        ? TriggerConds.All(c => {
            var r = EvaluateSingleCond(c);
            results.Add($"{c.GetType().Name}={r}");
            return r;
        })
        : TriggerConds.Any(c => {
            var r = EvaluateSingleCond(c);
            results.Add($"{c.GetType().Name}={r}");
            return r;
        });

    if (ReverseResult) met = !met;
    Hi.Verbose($"[ExecNode] Cond({Tag}) [{CondLogicType}] {{string.Join(",", results)}} → {(met ? "满足" : "不满足")}");
    return met;
}

// Helper
private static bool EvaluateSingleCond(ITriggerCond cond)
{
    try { return cond.Handle(); }
    catch { return false; }
}
```

Wait, but this is getting complex. The user wants "足够的log来确定问题点位" but I shouldn't over-engineer. Let me keep it simpler - just log which conditions evaluated and their results:

Actually, let me keep it much simpler. Just log the results without adding a helper:

```csharp
public bool EvaluateConds()
{
    if (TriggerConds.Count == 0) return true;

    bool met = CondLogicType == CondLogicType.And
        ? TriggerConds.All(c => { try { return c.Handle(); } catch { return false; } })
        : TriggerConds.Any(c => { try { return c.Handle(); } catch { return false; } });

    if (ReverseResult) met = !met;
    Hi.Verbose($"[ExecNode] Cond({Tag}) [{CondLogicType}] {TriggerConds.Count}个条件 → {(met ? "满足" : "不满足")}");
    return met;
}
```

That's simpler and still useful. Same for EvaluateForEvent.

- [ ] **Step 8: TreeScriptNode.OnExecute — 脚本执行**

OnlyCheck 分支：
```csharp
if (OnlyCheck)
{
    try {
        var checkResult = _compiled.Check(null);
        Hi.Verbose($"[ExecNode] Script({Tag}) OnlyCheck: {checkResult}");
        return checkResult;
    }
    ...
}
```

等待模式：
```csharp
Hi.Verbose($"[ExecNode] Script({Tag}) 进入等待模式");
var result = await ExecutionAxis.Instance.WaitCond(this);
Hi.Verbose($"[ExecNode] Script({Tag}) WaitCond 返回: {(result ? "满足" : "取消")}");
return result;
```

- [ ] **Step 9: TreeScriptNode.EvaluateConds/EvaluateForEvent**

```csharp
public bool EvaluateConds()
{
    if (_compiled == null) return true;
    try {
        var r = _compiled.Check(null);
        Hi.Verbose($"[ExecNode] Script({Tag}) EvaluateConds: {r}");
        return r;
    }
    catch { return false; }
}
```

- [ ] **Step 10: TreeActionNode.OnExecute — 动作执行**

```csharp
protected override Task<bool> OnExecute(EvalContext ctx)
{
    foreach (var action in TriggerActions)
    {
        try {
            Hi.Verbose($"[ExecNode] Action({Tag}) → {action.GetType().Name}");
            action.Handle();
        }
        catch (Exception ex) { DService.Instance().Log.Error($"[TriggerNode] 动作异常: {ex.Message}"); }
    }
    return Task.FromResult(true);
}
```

- [ ] **Step 11: TreeDelayNode.OnExecute — 延迟**

```csharp
protected override async Task<bool> OnExecute(EvalContext ctx)
{
    if (ctx.IsDisposed) return false;
    Hi.Verbose($"[ExecNode] Delay({Tag}) 延迟 {Delay}s 开始");
    await HiAuRo.Runtime.Coroutine.Instance.DelayAsync(Delay * 1000);
    Hi.Verbose($"[ExecNode] Delay({Tag}) 延迟完成");
    return !ctx.IsDisposed;
}
```

- [ ] **Step 12: 编译验证**

```bash
dotnet build HiAuRo/HiAuRo.csproj -nologo
```

Expected: Build succeeded

- [ ] **Step 13: 提交**

```bash
git add HiAuRo/Execution/ExecutionNode.cs
git commit -m "feat: ExecutionNode 各节点添加 Verbose 执行日志"
```
