# Phase 5: ACR Abstraction + BRD 打样 — 索引

> Phase 5 已拆分为 4 个子阶段，按依赖顺序执行。

## 子阶段

| 阶段 | 文件 | 内容 | 依赖 |
|------|------|------|------|
| 5.1 | [PHASE5.1_ACR_CORE.md](PHASE5.1_ACR_CORE.md) | ACR 核心接口 + 常量 + Spell/Slot | Phase 4 |
| 5.2 | [PHASE5.2_ACR_EXTENDED.md](PHASE5.2_ACR_EXTENDED.md) | 起手/序列/触发器 + 战斗 Helper | 5.1 |
| 5.3 | [PHASE5.3_UI.md](PHASE5.3_UI.md) | CEF Web UI 层（CEF 渲染 + Kestrel/WebSocket + HTML/CSS/JS + 热键/命令/设置） | 5.1 |
| 5.4 | [PHASE5.4_ENGINE.md](PHASE5.4_ENGINE.md) | AI 引擎 + BRD 打样（SlotExecutor + AIRunner + 全链路验证） | 5.1 + 5.2 + 5.3 |

## 执行顺序

```
5.1 (核心接口+常量+Spell/Slot) ──→ 5.2 (起手/序列/触发器/Helper)
                               │
                               └──→ 5.3 (CEF Web UI 层)
                                         │
                                         ↓
                                    5.4 (AI引擎+BRD打样)  ← MVP 终点
```

5.1 是所有子阶段的公共基础。5.2 和 5.3 互不依赖，可以并行开发。5.4 需要前三者全部完成。

## 架构亮点

5.3 的 CEF + WebSocket 基础设施不仅服务于 MVP 控制面板，更重要的是**被 Phase 9 事实轴可视化编辑器直接复用**。拖拽式时间线编辑器用 HTML/CSS/JS 实现远比 ImGui 容易。

## 进度总览

| 子阶段 | 状态 |
|--------|------|
| 5.1 ACR 核心 + 常量 + Spell/Slot | 已完成 |
| 5.2 起手/序列/触发器/Helper | 已完成 |
| 5.3 CEF Web UI 层 | 已完成 |
| 5.4 AI 引擎 + BRD 打样 | 已完成 |

---

*Created: 2026-05-03*
