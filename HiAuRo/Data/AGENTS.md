# 数据层

## 入口
`HiAuRo.Data.Data` — 静态类，统一游戏数据入口。
`Data.IsReady` — GameState.IsLoggedIn && IsTerritoryLoaded 时返回 true。

## 分布类设计
`Data.cs` 使用 `partial class` 分布到多个文件：

| 文件 | 职责 |
|------|------|
| `Data.cs` | IsReady 检查 |
| `Data.Me.cs` | 自身角色数据 (Job/Level/HP/MP/Position/Status) |
| `Data.Target.cs` | 当前目标数据 |
| `Data.Party.cs` | 队伍成员数据 |
| `Data.Objects.cs` | 周围对象数据 |
| `Data.Combat.cs` | 战斗状态 (InCombat/BattleTime/...) |

## OmenTools 数据来源
- **无包装层** — Data 是轻薄转发门面，不构建仓库层
- 所有 Dalamud 服务通过 `DService.*` 访问（OmenTools 提供）
- `IObjectTable.LocalPlayer`（非废弃的 `Svc.ClientState.LocalPlayer`）
- `IPlayerState` 用于职业档案信息

## 辅助数据
- `BRDHelp.cs` — 诗人特定数据辅助（歌曲状态/Empyreal Arrow/Bloodletter 等）

## 重要告诫
1. 不迭代 `IPartyMember.GameObject` 多次 — 每帧只解析一次
2. 敌人分类不能仅靠 `BattleNpcSubKind.Enemy` — 还需检查 `ObjectKind/OwnerId/BuddyList/IsTargetable`
3. 数据是一层薄薄的转发，不是仓储模式
