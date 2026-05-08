# Phase 3: Data Layer（游戏数据层）— 开发任务

## 目标

整理游戏运行时数据，提供统一入口 `HiAuRo.Data`；为首要目标职业 BRD 提供职业快捷入口。

**依赖**: Phase 2
**需求**: DATA-01, DATA-02, DATA-03, DATA-04, DATA-05

## 实现原则

- 以 API 直取和必要整理为主，不把数据层做成笨重的大型包装仓库
- Self / Target / Combat 直接转发 OmenTools 的静态属性
- Party / Objects 做一次扫描、多视图复用
- 用 `GameState.IsLoggedIn` 做就绪判断（OmenTools 已提供）
- 不建立重缓存生命周期或快照树

## 文件清单

| 操作 | 文件 | 说明 |
|------|------|------|
| 新建 | `Data/Data.cs` | HiAuRo.Data 根入口（partial class） |
| 新建 | `Data/Data.Self.cs` | 转发 LocalPlayerState.* |
| 新建 | `Data/Data.Target.cs` | 转发 TargetManager.* |
| 新建 | `Data/Data.Combat.cs` | 转发 GameState.* + Condition.* |
| 新建 | `Data/Data.Party.cs` | 队伍扫描 + 角色/距离分类 |
| 新建 | `Data/Data.Objects.cs` | 对象扫描 + 语义分类 |
| 新建 | `Data/Jobs/BRDHelp.cs` | 诗人职业快捷入口 |
| 修改 | `Plugin.cs` | 确认 DService 初始化已完成（Phase 1 已接入） |

## 任务

### Task 1: Data 根入口 + Self/Target/Combat 分区

**操作**:
1. 新建 `Data/Data.cs` → `public static partial class Data`
2. 新建 `Data/Data.Self.cs` → `public static partial class Data { public static class Self { ... } }`
   - 直接转发 `LocalPlayerState.Object`、`.Name`、`.ClassJob`、`.CurrentLevel`、`.IsMoving`
   - Buff 检测转发 `LocalPlayerState.HasStatus()`
   - 距离计算转发 `LocalPlayerState.DistanceToObject2D/3D()`
   - 就绪前返回空/默认值
3. 新建 `Data/Data.Target.cs` → `public static partial class Data { public static class Target { ... } }`
   - 直接转发 `TargetManager.Target`、`.FocusTarget`、`.MouseOverTarget`、`.SoftTarget`、`.PreviousTarget`
4. 新建 `Data/Data.Combat.cs` → `public static partial class Data { public static class Combat { ... } }`
   - 转发 `GameState.IsLoggedIn`、`.IsInInstanceArea`、`.IsInPVPArea`、`.TerritoryType`、`.Map`、`.ServerTime`、`.DeltaTime`
   - 转发 `DService.Condition` 相关：InCombat、IsCasting 等
   - Combat 保持克制，不接收敌人数统计、TTK 等"已经解释战斗"的字段

**验证**: `dotnet build` 通过；Self/Target/Combat 三个分区可以从 Data 根入口访问

**完成**: Self/Target/Combat 是转发，没有仓库层或快照树

---

### Task 2: Party/Objects 分类视图

**操作**:
1. 新建 `Data/Data.Party.cs` → `public static partial class Data { public static class Party { ... } }`
   - 刷新方法：一次扫描 `DService.PartyList`，遍历 `IPartyMember`
   - 每个 `IPartyMember.GameObject` 只解析一次
   - 输出视图：All / Alive / Dead / Tanks / Healers / Dps / Nearby5y / Nearby10y / Nearby15y / CastableParty / CastableTanks / CastableHealers / CastableDps / CastableMainTanks / CastableMelees / CastableRangeds / CastableAlliesWithin20 / CastableAlliesWithin25 / CastableAlliesWithin30
   - CastableParty：可施法队友（排除自己、排除距离外）
   - CastableMainTanks：开着盾姿的 T（可用于减伤分配）
   - CastableMelees / CastableRangeds：近战/远程队友分类
   - Nearby 扩展桶：20y / 25y / 30y，用于更大范围 AOE 和治疗判定
   - 就绪前返回空列表
2. 新建 `Data/Data.Objects.cs` → `public static partial class Data { public static class Objects { ... } }`
   - 刷新方法：一次扫描 `DService.ObjectTable`
   - 输出视图：All / Allies / Enemies / Party / Pets / Summons / Environment / Others
   - **Enemies 分类必须联合**：ObjectKind(BattleNpc) + BattleNpcSubKind(Enemy) + IsTargetable + OwnerId + BuddyList 关系的排除
   - 不要单用 BattleNpcSubKind.Enemy 判断（单人 duty 友方 NPC 可能也是 Enemy 类型）
3. 刷新按需触发，不建立后台持续刷新

**验证**: `dotnet build` 通过；Party/Objects 分区各类视图可访问

**完成**: Party/Objects 属于一次扫描多视图，不存在后台持续刷新或长期缓存

---

### Task 3: BRDHelp.cs 职业快捷入口

**操作**:
1. 新建 `Data/Jobs/BRDHelp.cs` → `public static class BRDHelp`
2. 组合 Data.Self + Data.Target + Data.Party + Data.Combat + `DService.JobGauges`
3. 暴露诗人常用状态：
   - 歌曲状态（当前歌曲、剩余时间）
   - DoT 状态（风蚀/毒咬是否在目标上、剩余时间）
   - 诗人特定 Buff（直线射击预备、九天连箭预备等）
   - 职业资源（灵魂之声量谱）
4. 风格：短平快，职业作者一眼能懂，拿数据快、写逻辑顺
5. 中文注释：在容易误解的职业特有状态处加注释

**验证**: `dotnet build` 通过；BRDHelp 各属性可访问

**完成**: 诗人职业数据有了短路径入口；没有长出新的职业框架层

---

## 阶段验证

- [ ] `HiAuRo.Data` 可访问 Self / Target / Party / Objects / Combat
- [ ] Self / Target / Combat 是转发，不做额外缓存
- [ ] Party / Objects 一次扫描多视图，不重复遍历
- [ ] 未登录/切图时空值语义稳定
- [ ] Enemies 分类不是单一字段判断
- [ ] BRDHelp.cs 可读取诗人特有职业状态
- [ ] 没有仓库层、快照树、长期缓存

## 威胁模型

| 威胁 | 类别 | 处置 |
|------|------|------|
| 失效对象引用（切图/登录后） | D | 统一检查 `GameState.IsLoggedIn`；不跨帧持有对象引用 |
| PartyMember.GameObject 重复解析 | D | 单次扫描内只解析一次，后续复用 |
| BattleNpcSubKind.Enemy 误判友军 | T | 联合 ObjectKind + OwnerId + BuddyList + IsTargetable 多字段判断 |
| DebugEnabled 泄露隐私 | I | Data 层不主动输出调试日志到常驻通道 |

---

## 进度

| Task | 状态 |
|------|------|
| Task 1: Data 根入口 + Self/Target/Combat | 已完成 |
| Task 2: Party/Objects 分类视图 | 已完成 |
| Task 3: BRDHelp.cs | 已完成 |

---

*Created: 2026-05-03*
