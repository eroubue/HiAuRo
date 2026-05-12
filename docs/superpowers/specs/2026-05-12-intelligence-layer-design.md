# 智能层 — 需求到事实轴的绑定与广播

**日期**: 2026-05-12  
**状态**: 已确认

## 目标

实现智能层（IntelligenceEngine），将执行轴/辅助轴脚本节点产生的移动/TP 需求绑定到事实轴节点，通过 IPC 广播到 8 个客户端，各客户端独立根据事实轴+智能层决定执行时机，最终通过 IPC 发给外部移动插件执行。

## 关键决策

| 决策点 | 选用方案 |
|--------|---------|
| 需求绑定方式 | 脚本节点 JSON 加 `factNodeId` 字段，一对一绑定 |
| 运行时行为 | 积压-释放：需求先入队，事实轴到位后智能层判断放行 |
| 同节点多需求顺序 | 按添加顺序 |
| 网络广播 | 通过 Dalamud IPC → BroadcastPlugin → 网络 → 各客户端 IPC 接收 |
| 移动执行 | 暂空置，通过 IPC 发给外部移动插件 |

## 组件设计

### 1. MovementDemand（新文件 `Runtime/Intelligence/MovementDemand.cs`）

```csharp
public sealed class MovementDemand
{
    public string Id { get; init; }
    public string FactNodeId { get; init; }
    public DemandType Type { get; init; }    // MoveTo / TP / Hold
    public Vector3? TargetPos { get; init; }
    public float? TargetHeading { get; init; }
    public string TargetRole { get; init; } = "All"; // 目标职责（MT/OT/H1/H2/D1-D4/All），由脚本指定
    public int AddedOrder { get; set; }
    public string Source { get; init; } = "";
}
```

### 2. DemandBuffer（新文件 `Runtime/Intelligence/DemandBuffer.cs`）

线程安全的 `ConcurrentQueue<MovementDemand>`，提供 `Add()`、`GetGrouped()`（按 factNodeId 分组）、`Remove()`. 脚本节点和 IPC 接收方调用 `Add()`，智能层调用 `GetGrouped()` 和 `Remove()`。

### 3. IntelligenceEngine（新文件 `Runtime/Intelligence/IntelligenceEngine.cs`）

单例，每帧由 AIRunner 调用 `Update(FactTimeline)`：
- 读取 `FactState.CurrentEvent.Id`
- 从 `DemandBuffer.GetGrouped()` 取匹配的需求
- 按 `TargetRole` 过滤（客户端只执行自己职责的需求）
- 按 `AddedOrder` 排序
- `CanExecute()` 判断（暂空置，始终返回 true；将来加读条/机制检测）
- 通过 IPC 发给外部移动插件执行
- 从缓冲区移除已释放的需求

### 4. IpcDemandService（新文件 `Runtime/Intelligence/IpcDemandService.cs`）

封装 IPC 通信：
- `SendToBroadcast(MovementDemand[])` → 通过 Dalamud IPC 调用 BroadcastPlugin 广播到其他客户端
- `RegisterReceiver(Action<MovementDemand[]>)` → 注册 IPC 接收回调，收到后添加到 DemandBuffer

### 5. 脚本节点增强

`TreeScriptNode` 加 `FactNodeId` 属性。`ExecutionJson.cs` 解析时提取 `factNodeId` 字段。脚本执行上下文中注入 `DemandBuffer`，自动继承节点的 `FactNodeId`。

### 6. 编辑器联动

`editor.js` 中脚本节点属性面板加 `factNodeId` 输入框 + 加载事实轴后下拉选择。

### 7. Plugin 集成

- 初始化时 `IpcDemandService.RegisterReceiver()`
- `AIRunner.Update()` 中调用 `IntelligenceEngine.Instance.Update()`

## 文件变更

| 操作 | 文件 | 说明 |
|------|------|------|
| 新增 | `Runtime/Intelligence/MovementDemand.cs` | 需求数据模型 |
| 新增 | `Runtime/Intelligence/DemandBuffer.cs` | 线程安全缓冲区 |
| 新增 | `Runtime/Intelligence/IntelligenceEngine.cs` | 智能层引擎 |
| 新增 | `Runtime/Intelligence/IpcDemandService.cs` | IPC 发送/接收 |
| 修改 | `Execution/ExecutionNode.cs` | TreeScriptNode.FactNodeId |
| 修改 | `Execution/ExecutionJson.cs` | JSON 解析 factNodeId |
| 修改 | `Runtime/AIRunner.cs` | 每帧调用 IntelligenceEngine |
| 修改 | `Plugin.cs` | 注册 IPC 接收 |
| 修改 | `UI/web/editor.js` | 编辑器 factNodeId 输入 |
