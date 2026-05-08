# AEAssist 源码学习笔记

> 用于 HiAuRo MVP 设计参考。AEAssist 是当前最成熟的 FFXIV Dalamud 战斗插件宿主框架，HiAuRo 对标它的定位。

---

## 一、整体架构概览

AEAssist 的核心文件结构：

```
AEAssist/
├── Plugin.cs                  # 插件入口，所有模块的组装点
├── Core.cs                    # 全局核心状态（static 类）
├── CombatRoutine/             # ACR 框架（最核心的子系统）
│   ├── Rotation.cs            # Rotation 容器
│   ├── RotationManager.cs     # ACR 运行时管理器
│   ├── IRotationEntry.cs      # ACR 作者入口接口
│   ├── SettingMgr.cs          # 全局设置管理器
│   ├── Spell.cs               # 技能定义
│   ├── SpellsDefine.cs / AurasDefine.cs  # 技能/BUFF 常量
│   ├── Jobs.cs / JobsCategory.cs         # 职业定义
│   ├── Module/                # ACR 运行时模块
│   │   ├── AI.cs              # AI 主循环（ACR 核心引擎）
│   │   ├── SlotMode.cs / SlotResolverData.cs / ISlotResolver.cs
│   │   ├── Opener/IOpener.cs / OpenerMgr.cs  # 起手爆发
│   │   ├── BattleData.cs      # 战斗数据缓存
│   │   ├── Hotkey/            # 热键系统
│   │   └── Target/            # 目标管理
│   ├── Trigger/               # 触发器系统（执行轴的核心）
│   │   ├── TriggerLine.cs / TriggerMgr.cs
│   │   ├── ITriggerAction.cs / ITriggerCond.cs
│   │   ├── TriggerAction/*    # 20+ 种触发动作
│   │   ├── TriggerCond/*      # 30+ 种触发条件
│   │   └── Node/*             # 触发器 AST 节点
│   └── View/                  # ACR 视图/UI
│       ├── JobView/           # 职业悬浮窗
│       │   ├── MainWindow.cs  # 主控制面板
│       │   ├── QtWindow.cs    # QT 开关面板
│       │   ├── HotkeyWindow.cs # 热键面板
│       │   └── JobViewWindow.cs # 职业视图
│       └── Setting/           # 全局设置 UI
├── Helper/                    # 44 个工具类
│   ├── GCDHelper.cs           # GCD 检测
│   ├── TargetHelper.cs        # 目标/敌人数获取
│   ├── PartyHelper.cs         # 队伍数据整理
│   ├── SpellHelper.cs         # 技能判断
│   ├── MoveHelper.cs          # 移动检测
│   ├── HotkeyHelper.cs        # 热键管理
│   ├── TriggerLineHelper.cs   # 触发器辅助
│   └── ...
├── MemoryApi/                 # 44 个内存 API 封装
│   ├── MemApiSpell.cs         # GCD/Combo/冷却
│   ├── MemApiBuff.cs          # BUFF 检测
│   ├── MemApiTarget.cs        # 目标信息
│   ├── MemApiCondition.cs     # 条件判断
│   ├── MemApiParty.cs         # 队伍信息
│   └── ...
├── JobApi/                    # 21 个职业 API
│   ├── JobApi_BlackMage.cs    # 黑魔特有状态
│   ├── JobApi_Bard.cs         # 诗人特有状态
│   └── ...（每个战斗职业一个文件）
├── TriggerlineEditor/         # 时间轴/触发器编辑器
│   ├── TriggerlineEditor.cs   # 编辑器主窗口
│   ├── CactbotTimeline.cs     # Cactbot 格式导入
│   └── CloudTriggerline.cs    # 云端时间轴
├── GUI/                       # ImGui 封装
│   ├── Tree/                  # 可视化树编辑器（时间轴编辑器基础）
│   │   ├── TreeEditor.cs / TreeTimelineEditor.cs
│   │   └── Components/*       # 渲染/拖拽/属性面板
│   └── ImGuiHelper.cs / ObjectDrawer.cs / ...
├── Command/CommandMgr.cs      # 命令系统
├── Module/                    # 其他功能模块
│   ├── Avoid/                 # 躲避系统（GJK 算法）
│   ├── MainUI/ShortcutWindow.cs  # 快捷窗口
│   └── Network/               # 网络请求
├── DynamicComplie/            # 动态编译（ACR 脚本热加载）
├── CloudACR/                  # 云端 ACR 加载
└── ...
```

---

## 二、各子系统详解

### 2.1 插件宿主层

**Plugin.cs** 做了这些事：
1. 调用 `ECommonsMain.Init()` 初始化 ECommons
2. 设置 `Share.CurrentDirectory`（ACR 脚本目录）
3. 初始化 `AeLogger`（日志）
4. 初始化 `PluginConfig`（配置）
5. 调用 `ConstValue.InitVersion()`（版本信息）
6. 调用 `MultiHelper.Exec()`（多开检测）
7. 调用 `NetworkHelper.Init()`（网络）
8. 创建 Module 列表和 UIModule 列表
9. 注册 Framework.Update 回调
10. 创建 `MainUi` 主窗口
11. 初始化 `CommandMgr`（命令系统）

**Core.cs** 是全局静态入口：
- `Core.Me` → 当前玩家对象
- `Core.Resolve<T>()` → 获取 MemApi 实例
- 全局状态管理

### 2.2 ACR 框架（CombatRoutine）

这是 HiAuRo MVP 最需要对齐的部分。

**ACR 作者入口**：
```
IRotationEntry.Build(settingFolder) → Rotation
```

**Rotation 结构**：
```csharp
public class Rotation
{
    public List<SlotResolverData> SlotResolvers;   // 技能槽位列表
    public List<ITriggerAction> TriggerActions;     // 触发动作
    public List<ITriggerCondition> TriggerConditions; // 触发条件
    public IOpener Opener;                          // 起手爆发
    public IRotationEventHandler EventHandler;       // 事件处理
    public List<ISlotSequence> SlotSequences;        // 技能序列
}

public class SlotResolverData
{
    public ISlotResolver Resolver;  // Check() 返回 int（正=可用，-1=禁止，0=不关心）
    public SlotMode Mode;          // Gcd / OffGcd / Always
}

public enum SlotMode { Gcd, OffGcd, Always }
```

**ACR 作者还需要提供的**：
- `IRotationUI` → 职业悬浮窗 UI
- `OnDrawSetting()` → 职业设置页（可选）
- `Dispose()` → 资源释放

**AI 主引擎（AI.cs）**：
- 三种模式：`AILoop_Normal`（PVE）、`AILoop_PVP`（PVP）、`AILoop_Simulate`（模拟）
- 每个 Tick 遍历 SlotResolvers 列表，按顺序找第一个可用的技能
- GCD 窗口管理：前半窗口打 oGCD，后半窗口留 GCD
- 技能队列（SpellQueue）

### 2.3 触发器系统（Trigger）

这是 AEAssist 的"执行轴"实现。HiAuRo 最需要重点参考。

**TriggerLine（触发线）**：一条时间线，在一段战斗时间内按顺序检查触发器
- 可以循环（Loop）或单次
- 可以并行（Parallel）或顺序（Sequence）
- 有条件分支（Select）

**触发器 AST 节点类型**：
| 节点 | 作用 |
|------|------|
| `TreeSequence` | 顺序执行子节点 |
| `TreeParallel` | 并行执行子节点 |
| `TreeSelect` | 条件分支（if/else） |
| `TreeLoop` | 循环 |
| `TreeDelayNode` | 延时等待 |
| `TreeCondNode` | 条件检查 |
| `TreeActionNode` | 执行动作 |
| `TreeScriptNode` | 执行 C# 脚本 |
| `TreeClearTargetNode` | 清除目标 |
| `TreeClearWaitNode` | 清除等待 |

**触发条件（ITriggerCond）** — 30+ 种：
| 条件类型 | 文件 |
|---------|------|
| 经过时间 | `TriggerCondAfterBattleStart` |
| 倒计时 | `TriggerCondBeforeBattleTime` |
| 敌人读条 | `TriggerCondEnemyCastSpell` |
| 自己使用技能后 | `TriggerCondAfterSpell` |
| 收到技能效果 | `TriggerCondReceviceAbilityEffect` |
| Actor 死亡 | `TriggerCondActorDeath` |
| 目标图标 | `TriggerCondCheckTargetIcon` |
| 连线 | `TriggerCondActorControlTether` |
| 地图效果 | `TriggerCondMapEffect` |
| 天气变化 | `TriggerCondOnWeatherIdChanged` |
| 游戏日志 | `TriggerCondGameLog` |
| VFX 创建 | `VFXCreatCondParams` |
| Npc Yell | `NpcYellCondParams` |
| 变量判断 | `TriggerCondVariable` |
| ... | 等 30+ 种 |

**触发动作（ITriggerAction）** — 20+ 种：
| 动作类型 | 文件 |
|---------|------|
| 释放技能 | `TriggerActionCastSpell` |
| 技能队列 | `TriggerActionSpellQueue` |
| 高优先插入 | `TriggerActionHighPrioritySlot` |
| 锁定技能 | `TriggerActionLockSpell` |
| 切换目标 | `TriggerActionSelectenemy` |
| 切换停手 | `TriggerActionSwitchStop` |
| 切换拉怪 | `TriggerActionSwitchPull` |
| 传送 | `TriggerAction_SimpleTP` |
| 吃药 | `TriggerActionUsePotion` |
| 重放起手 | `TriggerActionReplayOpener` |
| 设置 Rotation | `TriggerActionSetRotation` |
| 发送命令 | `TriggerAction_SendCommand` |
| 发送按键 | `TriggerAction_SendKey` |
| 移动 | `TriggerAction_MoveTo` |
| ... | 等 20+ 种 |

### 2.4 数据层（Helper + MemoryApi）

**Helper 工具类**（44 个）：
| 类别 | 文件 | 功能 |
|------|------|------|
| 战斗 | `GCDHelper` | GCD 剩余、冷却判断 |
| 战斗 | `SpellHelper` | 技能可用性、距离 |
| 战斗 | `TargetHelper` | 目标/敌人数/范围判断 |
| 队伍 | `PartyHelper` | 队伍角色分类、距离分类 |
| 移动 | `MoveHelper` | 移动检测 |
| 热键 | `HotkeyHelper` | 热键状态 |
| 时间轴 | `TriggerLineHelper` | 触发器坐标转换 |
| UI | `LogHelper` | 日志输出 |
| 通用 | `MathHelper` / `TimeHelper` / `RandomHelper` | 通用工具 |

**MemoryApi**（44 个）：
给 `Core.Resolve<T>()` 使用的内存 API，每个 API 封装了底层的内存访问。HiAuRo 使用 OmenTools 后不需要这套，因为 OmenTools 的 `DService` + `OmenService` 已经提供了等价的封装。

**JobApi**（21 个职业，每职业一个文件）：
> **对应关系**：AE 的 `JobApi` = HiAuRo 的 `XXHelp.cs`。两者设计目的一致——为每个职业提供运行时快捷读取的职业特有状态（歌曲、DoT、Buff、资源等）。HiAuRo 统一使用 "XXHelp" 命名（如 `BRDHelp.cs`），对应 AE 的 `JobApi_Bard.cs`。
每个文件提供该职业特有的状态读取：
```csharp
// 例如 JobApi_BlackMage.cs
public class JobApi_BlackMage
{
    public bool InAstralFire;        // 火状态
    public int AstralFireStacks;     // 火层数
    public int AstralSoulStacks;     // 耀星层数
    public bool IsParadoxActive;     // 悖论可用
    public int PolyglotStacks;       // 通晓层数
    public long EnochianTimer;       // 天语剩余时间
    public bool InUmbralIce;         // 冰状态
    public int UmbralHearts;         // 冰针
    public int UmbralIceStacks;      // 冰层数
}
```

这些 JobApi 不是在 Phase 3 数据层落的，而是在数据层之上，为 ACR 作者提供职业特有的快捷入口。

### 2.5 主控制面板（MainUI）

**MainUi.cs**：主窗口，包含：
- 职业选择/切换
- ACR 启停开关
- 当前运行状态显示
- 热键设置入口
- 时间轴编辑器入口
- 设置入口

**JobView/悬浮窗**：
- `MainWindow.cs` — 职业主悬浮窗（技能提示、QT 开关等）
- `QtWindow.cs` — QT（Quick Toggle）开关面板
- `HotkeyWindow.cs` — 热键显示

**设置页**：
- `GeneralBaseSettingUi` — 基础设置
- `GeneralHotkeyUi` — 热键设置
- `GeneralPotionSettingUi` — 药水设置
- `GeneralRotationSetting` — Rotation 设置
- `GeneralTtkSettingUi` — TTK 设置
- `GeneralBattleLogFilterView` — 战斗日志过滤
- `GeneralMacroSettingUi` — 宏设置

### 2.6 时间轴编辑器（TriggerlineEditor）

这是 HiAuRo Phase 7 事实轴编辑器的直接参考。

**TriggerlineEditor.cs**：时间轴编辑器主窗口
- **可视化时间轴**：横向时间线，节点在时间轴上排布
- **CactbotTimeline.cs**：支持导入 Cactbot 格式的时间线文件
- **CloudTriggerline.cs**：从云端加载社区共享的时间轴
- **FFLogs 集成**：从 FFLogs 战斗记录自动分析时间轴
- **TreeEditor**：基于 ImGui 的可视化节点编辑器（支持拖拽、复制粘贴、撤销重做）

**编辑器组件（GUI/Tree/）**：
- `TreeEditor` / `TreeTimelineEditor` — 核心编辑器
- `Components/DragDropHandler` — 拖拽支持
- `Components/TreeViewRenderer` — 树形视图渲染
- `Components/TreePropertyPanel` — 属性编辑面板
- `Commands/UndoRedoManager` — 撤销/重做

### 2.7 命令系统（Command）

**CommandMgr.cs**：通过 `/ae` 命令控制插件
- 开关 ACR
- 切换职业
- 热键绑定
- 调试命令

### 2.8 其他模块

**Avoid 系统**：副本 AOE 规避（GJK 碰撞检测算法），不是 MVP 必需。
**DynamicComplie**：动态编译 ACR 脚本（外部 DLL 热加载）。
**CloudACR**：从云端下载 ACR 脚本。
**Module 系统**：所有功能模块通过注册机制挂到 Framework.Update。

---

## 三、AEAssist 与 HiAuRo MVP 的对照

### AEAssist 有，HiAuRo MVP 也要有：

| AEAssist 组件 | HiAuRo 对应 | MVP 是否必需 |
|--------------|-------------|-------------|
| Plugin.cs（宿主入口） | Plugin.cs | ✓ Phase 1 |
| PluginConfig（配置） | PluginConfig.cs | ✓ Phase 2 |
| IRotationEntry（ACR 作者接口） | IRotationEntry | ✓ Phase 5 |
| Rotation + SlotResolverData | Rotation + SlotResolverData | ✓ Phase 5 |
| SlotMode (Gcd/OffGcd/Always) | SlotMode | ✓ Phase 5 |
| ISlotResolver | ISlotResolver | ✓ Phase 5 |
| IOpener（起手爆发） | IOpener | ✓ Phase 5 |
| ISlotSequence（技能序列） | ISlotSequence | ✓ Phase 5 |
| TriggerAction / TriggerCond | TriggerAction / TriggerCond | ✓ Phase 5 |
| **Core.Me（玩家对象）** | **Data.Self** | ✓ Phase 3 |
| **PartyHelper** | **Data.Party** | ✓ Phase 3 |
| **TargetHelper** | **Data.Target** | ✓ Phase 3 |
| **GCDHelper** | **Data.Combat** 或 ACR 内部 | ✓ Phase 3-4 |
| **SpellHelper** | **ACR 内部工具** | ✓ Phase 5 |
| **MoveHelper** | **Data.Self** 或 **LocalPlayerState** | ✓ Phase 3 |
| **JobApi（职业 API）** | **XXHelp.cs** | ✓ Phase 3（或 Phase 5） |
| **HotkeyHelper/QT 系统** | **HotkeyHelper** | ✓ Phase 5 |
| MainUI（主面板） | MainPanel.cs | ✓ Phase 5 |
| JobView（职业悬浮窗） | JobViewWindow | ✓ Phase 5 |
| SettingMgr（全局设置） | SettingMgr | ✓ Phase 5 |
| CommandMgr（命令） | CommandMgr | ✓ Phase 5 |
| Framework.Update（帧循环） | FrameworkManager.Reg()（OmenTools） | ✓ Phase 4 |
| TriggerLine（触发线） | ExecutionAxis | Phase 6（MVP 后） |
| TriggerlineEditor（编辑器） | FactAxis Editor | Phase 7-9（MVP 后） |

### AEAssist 有，HiAuRo MVP 不需要的：

| 组件 | 原因 |
|------|------|
| MemoryApi（44 个） | OmenTools `DService` + `OmenService` 替代 |
| DynamicComplie | 先用内置 ACR，后续再加 |
| CloudACR | 后续功能 |
| Avoid 系统 | 不做自动跑位 |
| CactbotTimeline 导入 | Phase 7 再考虑 |
| FFLogs 集成 | 后续功能 |
| Network（多人协同） | 不在 MVP 范围 |

---

## 四、对 HiAuRo 文档的补全结论

对比 AEAssist 源码和旧规划文档后，HiAuRo 当前 `doc/` 缺失以下内容：

### 必须补的：

1. **Phase 4 运行时核心缺少细节**
   - 旧规划有 `04-02: 建立模式切换与互斥约束`、`04-03: 建立基础调度与运行循环骨架`
   - 当前只有三个文件名，没有具体描述

2. **Phase 5 ACR 抽象严重不完整**
   - 缺少 **IOpener**（起手爆发序列）
   - 缺少 **ISlotSequence**（技能序列/组合按键）
   - 缺少 **Hotkey 系统**（热键/QT 开关）
   - 缺少 **CommandMgr**（`/hi` 命令行）
   - 缺少 **SettingMgr**（全局 + 职业设置管理）
   - 缺少 **JobViewWindow**（职业悬浮窗）
   - 缺少 **Spell/Auras 常量定义**（技能/BUFF ID 表）
   - 缺少 **ITriggerAction / ITriggerCond** 的完整定义

3. **Phase 3 数据层缺少 JobApi**
   - 旧规划有 `XXHelp.cs` 概念（22 个职业文件）
   - 当前文档完全没有提及

4. **旧规划的 55 个 D-Decisions 全部丢失**
   - Phase 1: 8 个 (D-01~D-08)
   - Phase 2: 13 个 (D-01~D-13)
   - Phase 3: 34 个 (D-01~D-34)
   - 这些决策定义了每个阶段的明确边界和取舍

---

*Last updated: 2026-05-03*
来源：AEAssist 源码（`资料/AEAssist/`）、Oblivion 源码（`资料/AE-ACR-Save/Oblivion/`）、旧规划文档（`.planning/`）
