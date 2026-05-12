# 智能层实现计划

> **For agentic workers:** Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement.

**Goal:** 实现智能层（DemandBuffer + IntelligenceEngine），将脚本节点的移动需求绑定到事实轴，积压-释放执行。

**Architecture:** 新增 `Runtime/Intelligence/` 目录，含 4 个新文件。`TreeScriptNode` 加 `FactNodeId`，脚本通过 `DemandBuffer.Add()` 提交需求。`IntelligenceEngine` 每帧检查事实轴 → 释放匹配需求。IPC 暂空置。

---

### Task 1: 创建数据模型 + DemandBuffer

**Files:**
- Create: `HiAuRo/Runtime/Intelligence/DemandType.cs`
- Create: `HiAuRo/Runtime/Intelligence/MovementDemand.cs`
- Create: `HiAuRo/Runtime/Intelligence/DemandBuffer.cs`

- [ ] **Step 1: 创建 DemandType.cs**

```csharp
namespace HiAuRo.Runtime.Intelligence;

/// <summary>
/// 移动需求类型
/// </summary>
public enum DemandType
{
    MoveTo,
    TP,
    Hold,
}
```

- [ ] **Step 2: 创建 MovementDemand.cs**

```csharp
using System.Numerics;

namespace HiAuRo.Runtime.Intelligence;

/// <summary>
/// 移动/TP 需求，由脚本或 IPC 产生
/// </summary>
public sealed class MovementDemand
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public string FactNodeId { get; init; } = "";
    public DemandType Type { get; init; }
    public Vector3? TargetPos { get; init; }
    public float? TargetHeading { get; init; }
    public string TargetRole { get; init; } = "All";
    public int AddedOrder { get; set; }
    public string Source { get; init; } = "";
}
```

- [ ] **Step 3: 创建 DemandBuffer.cs**

```csharp
using System.Collections.Concurrent;

namespace HiAuRo.Runtime.Intelligence;

/// <summary>
/// 线程安全的移动需求缓冲区——脚本和 IPC 接收方写入，智能层读取
/// </summary>
public static class DemandBuffer
{
    private static readonly ConcurrentQueue<MovementDemand> _pending = new();
    private static int _orderCounter;

    /// <summary>添加需求（自动分配 AddedOrder）</summary>
    public static void Add(MovementDemand demand)
    {
        demand.AddedOrder = Interlocked.Increment(ref _orderCounter);
        _pending.Enqueue(demand);
    }

    /// <summary>按 factNodeId 分组取出所有积压需求</summary>
    public static ILookup<string, MovementDemand> GetGrouped()
    {
        return _pending.ToArray().ToLookup(d => d.FactNodeId);
    }

    /// <summary>移除已释放的需求</summary>
    public static void Remove(IEnumerable<string> demandIds)
    {
        var ids = new HashSet<string>(demandIds);
        var remaining = _pending.Where(d => !ids.Contains(d.Id)).ToArray();
        while (_pending.TryDequeue(out _)) { }
        foreach (var d in remaining)
            _pending.Enqueue(d);
    }

    /// <summary>清空</summary>
    public static void Clear()
    {
        while (_pending.TryDequeue(out _)) { }
    }
}
```

- [ ] **Step 4: 验证编译**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

- [ ] **Step 5: Commit**

```bash
git add HiAuRo/Runtime/Intelligence/
git commit -m "feat: add MovementDemand model and DemandBuffer"
```

---

### Task 2: 创建 IntelligenceEngine

**Files:**
- Create: `HiAuRo/Runtime/Intelligence/IntelligenceEngine.cs`

- [ ] **Step 1: 创建 IntelligenceEngine.cs**

```csharp
using HiAuRo.FactAxis;

namespace HiAuRo.Runtime.Intelligence;

/// <summary>
/// 智能层引擎——根据事实轴释放积压的移动需求
/// </summary>
public sealed class IntelligenceEngine
{
    public static IntelligenceEngine Instance { get; } = new();

    private string? _lastEventId;

    /// <summary>获取当前玩家的职责（供 TargetRole 匹配）</summary>
    private string CurrentRole
    {
        get
        {
            try
            {
                // 根据队伍位置判断: 自己是几号位
                return ""; // 暂空置
            }
            catch { return ""; }
        }
    }

    /// <summary>每帧由 AIRunner 调用</summary>
    public void Update(FactTimeline timeline)
    {
        var currentEvent = timeline.State.CurrentEvent;
        var eventId = currentEvent?.Id;
        if (eventId == null || eventId == _lastEventId) return;
        _lastEventId = eventId;

        var grouped = DemandBuffer.GetGrouped();
        if (!grouped.Contains(eventId)) return;

        var demands = grouped[eventId]
            .Where(d => string.IsNullOrEmpty(d.TargetRole) || d.TargetRole == "All" || d.TargetRole == CurrentRole)
            .OrderBy(d => d.AddedOrder);

        var released = new List<string>();
        foreach (var d in demands)
        {
            if (CanExecute(d))
            {
                Release(d);
                released.Add(d.Id);
            }
        }

        if (released.Count > 0)
            DemandBuffer.Remove(released);
    }

    /// <summary>判断当前是否可以执行（暂空置，未来加读条/机制检测）</summary>
    private static bool CanExecute(MovementDemand demand)
    {
        if (demand.Type == DemandType.TP) return true; // TP 不受读条影响

        // TODO: 检查玩家是否在读条中
        // TODO: 检查是否有重叠机制

        return true;
    }

    /// <summary>释放需求——通过 IPC 发给外部移动插件</summary>
    private static void Release(MovementDemand demand)
    {
        // 暂空置：通过 Dalamud IPC 调用外部移动插件
        DService.Instance().Log.Debug($"[Intelligence] 释放需求: {demand.Id} type={demand.Type} node={demand.FactNodeId} role={demand.TargetRole}");
        // 未来: IpcDemandService.Execute(demand);
    }
}
```

- [ ] **Step 2: 验证编译**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

- [ ] **Step 3: Commit**

```bash
git add HiAuRo/Runtime/Intelligence/IntelligenceEngine.cs
git commit -m "feat: add IntelligenceEngine with FactAxis-aware demand release"
```

---

### Task 3: TreeScriptNode 加 FactNodeId + ExecutionJson 解析

**Files:**
- Modify: `HiAuRo/Execution/ExecutionNode.cs` (TreeScriptNode)
- Modify: `HiAuRo/Execution/ExecutionJson.cs` (TriggerNodeData + ToNode)

- [ ] **Step 1: TreeScriptNode 加 FactNodeId 属性**

在 `TreeScriptNode` 类中，`Script` 属性之后添加：

```csharp
    /// <summary>绑定的事实轴节点 ID，脚本产生的 MovementDemand 自动继承</summary>
    public string FactNodeId { get; set; } = "";
```

- [ ] **Step 2: TriggerNodeData 加 factNodeId 字段**

在 `TriggerNodeData` 类中，`Script` 属性之后添加：

```csharp
    [JsonPropertyName("factNodeId")]
    public string? FactNodeId { get; set; }
```

- [ ] **Step 3: ToNode 传递 FactNodeId**

在 `ToNode()` 方法的 `case "TreeScriptNode":` 分支中，`Script = Script ?? ""` 之后添加：

```csharp
        FactNodeId = FactNodeId ?? ""
```

- [ ] **Step 4: 验证编译**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

- [ ] **Step 5: Commit**

```bash
git add HiAuRo/Execution/ExecutionNode.cs HiAuRo/Execution/ExecutionJson.cs
git commit -m "feat: add FactNodeId to TreeScriptNode and JSON parsing"
```

---

### Task 4: AIRunner 集成 IntelligenceEngine

**Files:**
- Modify: `HiAuRo/Runtime/AIRunner.cs`

- [ ] **Step 1: 在 UpdateDecisions 后调用 IntelligenceEngine**

在 `UpdateDecisions()` 调用之后（`UpdateFactAxis` 方法内），添加：

```csharp
    // Phase 8.5: 智能层——释放事实轴对应的移动需求
    IntelligenceEngine.Instance.Update(FactTimeline.Instance);
```

- [ ] **Step 2: 添加 using**

文件头部添加：
```csharp
using HiAuRo.Runtime.Intelligence;
```

- [ ] **Step 3: 验证编译**

```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

- [ ] **Step 4: Commit**

```bash
git add HiAuRo/Runtime/AIRunner.cs
git commit -m "feat: integrate IntelligenceEngine into AIRunner update loop"
```

---

### Task 5: 编辑器 factNodeId 输入

**Files:**
- Modify: `HiAuRo/UI/web/editor.js`

- [ ] **Step 1: 在脚本节点属性面板中添加 factNodeId 输入**

在 `editor.js` 中找到脚本节点的属性编辑部分（搜索 "Script" 相关的属性输入框），添加一个文本输入框用于编辑 `factNodeId`。精确插入点需要在读取文件后确定。

基本逻辑：
```javascript
// 在脚本属性区域添加
const factNodeIdRow = document.createElement('div');
factNodeIdRow.innerHTML = `
  <label>事实轴节点 ID</label>
  <input type="text" value="${node.factNodeId || ''}" 
    onchange="updateNodeProperty('factNodeId', this.value)" />
`;
propsPanel.appendChild(factNodeIdRow);
```

- [ ] **Step 2: 验证**

检查 `editor.html` 能正常加载，脚本节点属性面板多一个 factNodeId 输入框。

- [ ] **Step 3: Commit**

```bash
git add HiAuRo/UI/web/editor.js
git commit -m "feat: add factNodeId input to script node editor"
```

---

### Verification

全部完成后：
```bash
cmd.exe /c "dotnet build HiAuRo/HiAuRo.csproj -nologo"
```

Expected: 0 errors.

数据流验证：
```
脚本节点 JSON { factNodeId: "p2_raidwide", script: "DemandBuffer.Add(new MovementDemand {...})" }
  → ExecutionJson 解析 → TreeScriptNode.FactNodeId = "p2_raidwide"
  → 脚本执行 → DemandBuffer.Add(demand)  // demand.FactNodeId = "p2_raidwide"
  → 智能层每帧检查 → FactTimeline.State.CurrentEvent.Id == "p2_raidwide"
  → CanExecute() 通过 → Release() → 移除
```
