# Phase 7: Fact Axis（事实轴）— 占位

> Phase 7 没有独立的 dev-task 文档。实现直接依据 `doc/ROADMAP.md` Phase 7 规格。

## 实际交付

| 文件 | 行数 | 说明 |
|------|------|------|
| `FactAxis/FactNode.cs` | 237 | 数据模型 — FactTimelineData / Phase / Event / PhaseSwitch / nested Branch |
| `FactAxis/FactTimeline.cs` | 425 | 时间线引擎 — 双时钟 + Sync 校准 + 分支切换 + JSON 加载 |
| `FactAxis/sample_timeline.json` | 91 | Suzaku 副本示例 |

## 完成标准

1. ✅ JSON 定义 Boss 技能时间线（阶段 + 事件 + 切换点）
2. ✅ 根据 Sync 事件校准事件实际开始/结束时间
3. ✅ 支持条件分支切换后续事件列表
4. ✅ 输出当前事实状态供上层消费（Phase 8）

详见 `doc/DEVLOG.md` 2026-05-05 条目和 `doc/ROADMAP.md` Phase 7。
