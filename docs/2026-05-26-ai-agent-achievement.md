# HiAuRo 生态系统 — AI Agent 驱动构建成果描述

**日期**: 2026-05-26
**用途**: 申请 / 评估材料

---

## 项目概述

HiAuRo 是一个 AI Agent 驱动的 FFXIV 战斗辅助框架，由主框架 HiAuRo 和 MCPforDalamud 组成，通过 OpenCode 多 Agent 协作开发，539 次提交、56 个发布版本持续交付。

---

## 1. 核心痛点

FFXIV 高难副本中，玩家需同时应对 Boss 机制判定、最优技能循环、队友状态协调，信息过载导致决策失误。现有插件各自为政，缺乏统一框架将"感知→决策→执行→验证"链路自动化。插件开发调试同样低效——需反复进游戏测试，缺乏 AI 辅助的自动化验证手段。

---

## 2. 核心逻辑流（多 Agent 协作 + 长链推理）

### 2.1 三层 Agent 架构

| 层级 | 模块 | 职责 |
|------|------|------|
| **感知层** | MCPforDalamud | 通过 MCP 协议暴露 **45 个工具**（数据读取 22 个、角色操控 10 个、事件缓存 3 个、插件桥接 4 个、移动 4 个、插件管理/聊天各 1 个） |
| **决策层** | HiAuRo Runtime | AI Runner 引擎综合 FactAxis（Boss 时间线）、战斗录制、ACR 职业循环，构建决策树 |
| **执行层** | Execution Axis | 将决策编译为触发器，通过法术队列毫秒级精准释放技能 |

### 2.2 事件驱动缓存 — 零轮询设计

插件每帧检测 **21 种游戏事件**（HP/MP/GP 变化、战斗开始/结束、技能执行、施法、伤害、Buff、地图切换、骑乘、聊天、副本状态、FATE 等），写入 2000 条环形缓冲区。AI 通过 `query_events` 按需查询，支持类型筛选、时间范围、返回数量控制，**无需轮询，避免性能开销**。采集策略可通过 `configure_event_collection` 动态调整（属性选择、范围过滤、节流控制）。

### 2.3 长链推理闭环

```
Boss 读条 → 查询事实轴匹配机制 → 决策走位 + 减伤
    → 执行动作 → query_events 验证结果
    → 误判时自动回溯修正 → 重新推理
```

整个"感知 → 推理 → 决策 → 执行 → 验证"链路在事件缓存中可审计，支持事后复盘。

### 2.4 跨插件 Agent 协作

MCPforDalamud 的 IPC 桥接系统让 AI 可调用其他 Dalamud 插件的 IPC 端点：

1. `register_ipc_endpoint` — 注册已知插件端点
2. `list_ipc_endpoints` — 查询可用端点
3. `call_plugin_ipc` — 调用端点

示例：AI 调用 BossMod 获取机制时间轴 → 结合 ACR 生成应对策略 → 通过 MCP 操控角色执行，形成**跨插件多 Agent 协作**。

### 2.5 插件自我管理

AI 可通过 `manage_plugin` 加载/卸载/重载 Dalamud 插件，甚至热更新正在开发的 DLL，实现 AI 辅助的自动化测试工作流（开发 → 加载 → 测试 → 验证 → 修正 → 重载循环）。

### 2.6 AI Agent 驱动开发本身

项目通过 **OpenCode 多 Agent 架构**协作开发：

- **DeepSeek V4（主控 Agent）** — 理解需求、澄清模糊点、整理为结构化任务
- **GLM 5.1（Coder Agent）** — 执行复杂编码、分析、测试
- 工作流：用户需求 → 主控整理 → 调度 Coder → 编码测试 → 汇总结果 → Git Commit

---

## 3. 技术栈

- .NET 10, C#
- MCP (Model Context Protocol)
- Dalamud API Level 15
- OpenCode 多 Agent 平台
- GLM 5.1 / DeepSeek V4
- FFXIVClientStructs, Lumina
