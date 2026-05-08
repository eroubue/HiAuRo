# 开发任务文档（历史记录）

> ⚠️ 此目录下所有文档为 MVP 开发阶段的历史记录，已全部完成。
> Phase 7 和 Phase 8 没有独立的 dev-task 文档——实现直接依据 ROADMAP.md 规格。

## Phase 完成状态

| 文件 | Phase | 状态 | 说明 |
|------|-------|------|------|
| `PHASE1_HOST.md` | 1 | ✅ 已完成 | 宿主层 — Plugin.cs + HiAuRo.csproj |
| `PHASE2_INFRA.md` | 2 | ✅ 已完成 | 基础设施层 — PluginConfig + 分类日志 |
| `PHASE3_DATA.md` | 3 | ✅ 已完成 | 数据层 — Data.Self/Target/Party/Objects/Combat/Me |
| `PHASE4_RUNTIME.md` | 4 | ✅ 已完成 | 运行时核心 — RuntimeCore + CombatContext + EventSystem |
| `PHASE5_ACR.md` | 5 | ✅ 已完成 | ACR 抽象 — 总览 |
| `PHASE5.1_ACR_CORE.md` | 5.1 | ✅ 已完成 | ACR 核心接口 — IRotationEntry + Slot + Spell 等 22 文件 |
| `PHASE5.2_ACR_EXTENDED.md` | 5.2 | ✅ 已完成 | ACR 扩展 — IOpener + ISlotSequence + 5 Helper |
| `PHASE5.3_UI.md` | 5.3 | ✅ 已完成 | Web UI — HttpListener + WebSocket + 前端 |
| `PHASE5.4_ENGINE.md` | 5.4 | ✅ 已完成 | AI 引擎 — AIRunner + AILoop + SlotExecutor + BRD 打样 |
| `PHASE6_EXECUTION.md` | 6 | ✅ 已完成 | 执行轴 — async Task AST 引擎 + 18 Cond + 10 Action |
| `PHASE7_FACTAXIS.md` | 7 | ✅ 已完成 | 事实轴 — 无独立 dev-task, 直接依据 ROADMAP.md |
| `PHASE8_DECISION.md` | 8 | ✅ 已完成 | 决策层 — 无独立 dev-task, 直接依据 ROADMAP.md |
| `PHASE9_AUTHORING.md` | 9 | ✅ 已完成 | 创作层 — 纯前端编辑器（File System Access API），无需后端 |

## 当前项目状态

Phase 1-9 全部完成。编辑器为纯前端应用（File System Access API），AuthoringServer 仅提供 trigger catalog 注册。

HiAuRo.Helper 独立仓库：21 职业全 Helper 覆盖。

详见 `doc/ROADMAP.md`、`doc/PROJECT.md`、`doc/DEVLOG.md`。

---

*Last updated: 2026-05-08*
