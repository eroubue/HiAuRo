# ITriggerCondParams 事件日志系统

**日期**: 2026-05-12  
**状态**: 已确认

## 目标

测试所有 ITriggerCondParams 实现类（41个）是否正常运行，给事件分发点添加日志，输出到 HiAuRo 专属日志文件，并在主窗口新增"日志"Tab 实时显示。

## 关键决策

| 决策点 | 选用方案 |
|--------|---------|
| 日志文件位置 | Plugin Config 目录 (`ConfigDirectory/hiauro_events.log`) |
| 记录时机 | 每次事件触发时（GameEventHook 中 `OnEventFired.Invoke` 处） |
| 实时显示 | MainWindow ImGui 新增"日志"Tab |

## 组件设计

### 1. LogManager（新文件 `Infrastructure/LogManager.cs`）

单例，负责日志缓冲与文件写入。

```
class LogManager
  - _entries: ConcurrentQueue<LogEntry>   (环形缓冲区，内存保留最近 5000 条)
  - _syncRoot: object                     (文件写入锁)
  - _writer: StreamWriter                 (追加写)
  - _configDir: string
  - Instance: static LogManager
  - Init(string configDir): void          (创建/打开日志文件)
  - Log(ITriggerCondParams params): void  (格式化 + 入队 + 写文件)
  - GetEntries(): IReadOnlyList<LogEntry> (返回快照供 UI 线程读取)
  - Clear(): void                         (清空内存缓冲)
  - Dispose(): void                       (flush + close)
```

**LogEntry 记录**:
```csharp
public record LogEntry(DateTime Timestamp, string Type, string Content);
```

**内容生成**: 对每个 ITriggerCondParams 实例，使用反射读取其所有 public 字段，格式化为 `Field1=Value1, Field2=Value2, ...`。

**线程安全**: `ConcurrentQueue` 用于读写分离，UI 线程通过 `GetEntries()` 获取快照。

### 2. 集成点

在 `GameEventHook.cs` 中所有 `OnEventFired?.Invoke(...)` 调用前，增加一行：

```csharp
LogManager.Instance.Log(params);
```

共约 25 处 Invoke 调用点，在 `OnEventFired?.Invoke` 之后添加 `LogManager.Instance.Log`。

### 3. MainWindow "日志" Tab

在 MainWindow 的 TabBar 中新增"日志"Tab，DrawLog() 方法：

- **表格列**: 时间（HH:mm:ss.fff）、类型（可点击筛选）、内容
- **自动滚底**: 始终显示最新条目
- **筛选器**: 点击类型名可只显示该类型，再次点击取消筛选
- **清除按钮**: 清空内存缓冲
- **最大行数**: 5000 条，超过循环覆盖

## 文件变更清单

| 操作 | 文件 | 说明 |
|------|------|------|
| 新增 | `Infrastructure/LogManager.cs` | 日志管理器 |
| 修改 | `Execution/Events/GameEventHook.cs` | 每个 OnEventFired.Invoke 后加 LogManager.Instance.Log |
| 修改 | `UI/MainWindow.cs` | 新增"日志"Tab + DrawLog() 方法 |
| 修改 | `Plugin.cs` | Init 中 LogManager.Init(), Dispose 中 LogManager.Dispose() |
