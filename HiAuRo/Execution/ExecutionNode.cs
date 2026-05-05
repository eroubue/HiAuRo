using HiAuRo.ACR;

namespace HiAuRo.Execution;

/// <summary>
/// 触发树节点基类 — 对齐 AE TreeNodeBase.Run()
/// 异步一次性求值，与 AE 的 Task 模型一致
/// </summary>
public abstract class TriggerNode
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = "";
    public string Remark { get; set; } = "";
    public string Tag { get; set; } = "";
    public bool Enable { get; set; } = true;

    /// <summary>异步求值。Task 完成 = 节点终结（对齐 AE）</summary>
    public async Task<bool> Execute(EvalContext ctx)
    {
        if (!Enable) return true;
        if (ctx.IsDisposed) return false;
        return await OnExecute(ctx);
    }

    protected abstract Task<bool> OnExecute(EvalContext ctx);
}

/// <summary>组合节点</summary>
public abstract class TriggerCompositeNode : TriggerNode
{
    public List<TriggerNode> Childs { get; set; } = [];
}

/// <summary>叶子节点</summary>
public abstract class TriggerLeafNode : TriggerNode { }

#region 组合节点 —— 与 AE 完全对齐

/// <summary>
/// 序列节点 — 依次 await 子节点
/// IgnoreNodeResult: 忽略子节点失败，总是继续
/// </summary>
public sealed class TreeSequence : TriggerCompositeNode
{
    public bool IgnoreNodeResult { get; set; }

    protected override async Task<bool> OnExecute(EvalContext ctx)
    {
        foreach (var child in Childs)
        {
            if (!child.Enable) continue;
            if (ctx.IsDisposed) return false;

            var result = await child.Execute(ctx);

            if (!result && !IgnoreNodeResult)
                return false; // 短路失败
        }
        return true;
    }
}

/// <summary>
/// 并行节点
/// AnyReturn: 竞赛模式，最先完成的胜出
/// </summary>
public sealed class TreeParallel : TriggerCompositeNode
{
    public bool AnyReturn { get; set; }

    protected override async Task<bool> OnExecute(EvalContext ctx)
    {
        if (AnyReturn)
        {
            var tasks = Childs.Where(c => c.Enable).Select(c => c.Execute(ctx)).ToList();
            if (tasks.Count == 0) return true;
            var winner = await Task.WhenAny(tasks);
            return true; // 对齐 AE: 竞赛模式总是返回 true
        }
        else
        {
            var tasks = Childs.Where(c => c.Enable).Select(c => c.Execute(ctx)).ToList();
            if (tasks.Count == 0) return true;
            await Task.WhenAll(tasks);
            return true; // 对齐 AE: 忽略子节点结果
        }
    }
}

/// <summary>
/// 选择节点 — 依次尝试，第一个成功的返回
/// 对齐 AE: 所有分支都失败时仍返回 true
/// </summary>
public sealed class TreeSelect : TriggerCompositeNode
{
    protected override async Task<bool> OnExecute(EvalContext ctx)
    {
        foreach (var child in Childs)
        {
            if (!child.Enable) continue;
            if (await child.Execute(ctx))
                return true;
        }
        return true; // 对齐 AE
    }
}

/// <summary>
/// 循环节点 — 重复 Times 次
/// </summary>
public sealed class TreeLoop : TriggerCompositeNode
{
    public int Times { get; set; } = 1;

    protected override async Task<bool> OnExecute(EvalContext ctx)
    {
        for (int i = 0; i < Times; i++)
        {
            foreach (var child in Childs)
            {
                if (!child.Enable) continue;
                if (ctx.IsDisposed) return false;
                await child.Execute(ctx);
            }
        }
        return true;
    }
}

/// <summary>
/// 脚本节点 — C# 动态编译执行，对齐 AE TreeScriptNode
/// </summary>
public sealed class TreeScriptNode : TriggerLeafNode
{
    public bool OnlyCheck { get; set; }
    public string Script { get; set; } = "";

    private ITriggerScript? _compiled;

    protected override Task<bool> OnExecute(EvalContext ctx)
    {
        if (string.IsNullOrWhiteSpace(Script)) return Task.FromResult(true);

        try
        {
            _compiled ??= ScriptCompiler.Compile(Script);
            if (_compiled == null) return Task.FromResult(true);

            if (OnlyCheck)
            {
                // 仅检查：脚本返回的 bool 即条件结果
                return Task.FromResult(_compiled.Check(null));
            }
            else
            {
                // 执行：不计返回值，总是成功
                _compiled.Check(null);
                return Task.FromResult(true);
            }
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[TreeScriptNode] 脚本执行异常: {ex.Message}");
            return Task.FromResult(true);
        }
    }
}

#endregion

#region 叶子节点

public enum CondLogicType { And = 0, Or = 1 }

/// <summary>
/// 条件节点
/// CheckOnce: 立即检查
/// 等待模式: 注册 WaitCond（对齐 AE TriggerlineData.WaitCond）
/// </summary>
public sealed class TreeCondNode : TriggerLeafNode
{
    public List<ITriggerCond> TriggerConds { get; set; } = [];
    public CondLogicType CondLogicType { get; set; } = CondLogicType.And;
    public bool CheckOnce { get; set; }
    public bool ReverseResult { get; set; }

    protected override async Task<bool> OnExecute(EvalContext ctx)
    {
        if (CheckOnce)
        {
            return EvaluateConds();
        }
        else
        {
            // 等待模式: 挂起直到条件满足（对齐 AE WaitCond）
            return await ExecutionAxis.Instance.WaitCond(this);
        }
    }

    /// <summary>执行条件检查（供 ExecutionAxis 事件驱动调用）</summary>
    public bool EvaluateConds()
    {
        if (TriggerConds.Count == 0) return true;

        bool met = CondLogicType == CondLogicType.And
            ? TriggerConds.All(c => { try { return c.Handle(); } catch { return false; } })
            : TriggerConds.Any(c => { try { return c.Handle(); } catch { return false; } });

        if (ReverseResult) met = !met;
        return met;
    }
}

/// <summary>动作节点 — 总是成功</summary>
public sealed class TreeActionNode : TriggerLeafNode
{
    public List<ITriggerAction> TriggerActions { get; set; } = [];

    protected override Task<bool> OnExecute(EvalContext ctx)
    {
        foreach (var action in TriggerActions)
        {
            try { action.Handle(); }
            catch (Exception ex) { DService.Instance().Log.Error($"[TriggerNode] 动作异常: {ex.Message}"); }
        }
        return Task.FromResult(true);
    }
}

/// <summary>延迟节点 — await Coroutine.DelayAsync</summary>
public sealed class TreeDelayNode : TriggerLeafNode
{
    public double Delay { get; set; }

    protected override async Task<bool> OnExecute(EvalContext ctx)
    {
        if (ctx.IsDisposed) return false;
        await HiAuRo.Runtime.Coroutine.Instance.DelayAsync(Delay * 1000);
        return !ctx.IsDisposed;
    }
}

/// <summary>打印调试信息</summary>
public sealed class TreePrintDebugInfoNode : TriggerLeafNode
{
    public string Info { get; set; } = "";
    protected override Task<bool> OnExecute(EvalContext ctx)
    {
        DService.Instance().Log.Information($"[TriggerNode] {Info}");
        return Task.FromResult(true);
    }
}

/// <summary>清除等待条件</summary>
public sealed class TreeClearWaitNode : TriggerLeafNode
{
    public bool OnlyPreNode { get; set; } = true;
    protected override Task<bool> OnExecute(EvalContext ctx) => Task.FromResult(true);
}

#endregion

#region 上下文

public sealed class EvalContext
{
    public bool IsDisposed { get; set; }
    public int BattleTimeMs { get; set; }
    public Dictionary<string, int> Variables { get; } = [];

    public int GetVariable(string name) =>
        Variables.TryGetValue(name, out var v) ? v : 0;

    public void SetVariable(string name, int value) =>
        Variables[name] = value;

    public void Reset()
    {
        IsDisposed = false;
        BattleTimeMs = 0;
    }
}

#endregion
