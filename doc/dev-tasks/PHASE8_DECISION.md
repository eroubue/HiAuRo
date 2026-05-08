# Phase 8: Decision Layer（决策层）— 占位

> Phase 8 没有独立的 dev-task 文档。实现直接依据 `doc/ROADMAP.md` Phase 8 规格。

## 实际交付

| 文件 | 行数 | 说明 |
|------|------|------|
| `Decision/DecisionTypes.cs` | 118 | 技能数据模型 + DecisionSkillRegistry + DecisionOutput |
| `Decision/DecisionEngine.cs` | 206 | 贪心分配引擎 + 内置技能数据（BRD/MNK/WHM） |

## 完成标准

1. ✅ 可读取事实轴需求 + 队伍组成
2. ✅ 减伤分配优先级按冷却升序，允许超出需求
3. ✅ 输出强制技能列表给 ACR 执行

## 已注册内置技能

| 职业 | 团队减伤 | 单人减伤 | 团队治疗 |
|------|----------|----------|----------|
| BRD | 策动 / 行吟 | — | 光阴神的礼赞凯歌 |
| MNK | 牵制 | 内丹 | — |
| WHM | 节制 | — | 医济 / 全大赦 |

详见 `doc/DEVLOG.md` 2026-05-05 条目和 `doc/ROADMAP.md` Phase 8。
