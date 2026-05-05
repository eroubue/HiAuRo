# BRD 诗人示例 ACR

## 用途
HiAuRo 框架的 ACR 开发示范 —— 2 个 SlotResolver + 1 个 Opener + QT 悬浮窗

## 文件

| 文件 | 作用 |
|------|------|
| `BRDRotationEntry.cs` | ACR 入口 + UI 注册 |
| `BRD_GCD_强力射击.cs` | GCD SlotResolver (强力射击) |
| `BRD_oGCD_失血箭.cs` | oGCD SlotResolver (失血箭) |
| `BRDOpener.cs` | 起手序列 |
| `BRDBattleData.cs` | 战斗数据管理 |
| `BRD.csproj` | 项目文件（引用 HiAuRo.dll + OmenTools.dll） |

## 构建与部署

```bash
dotnet build example/BRD/BRD.csproj
```

输出 `bin/Debug/HiAuRo.BRD.dll`，自动复制到 ACR 目录。游戏中 `/hi reload` 热加载。

## 开发模式
- **项目引用**（默认）：直接引用 `../../HiAuRo/HiAuRo.csproj`
- **DLL 引用**：注释掉项目引用，改为引用 `devPlugins/HiAuRo/HiAuRo.dll`

## 多职业支持
一个 DLL 可包含多个 `IRotationEntry` 实现（每个对应不同职业）。ACRLoader 扫描时自动发现。

## 悬浮窗
`BRDRotationUI.RegisterControls(IUiBuilder)` 注册声明式 UI 控件：
- 主控制 (暂停/保存按钮)
- Setting Tab → 基础设置组 → 智能模式开关
- → HiAuRo 翻译为 JSON → Web 前端渲染

## 扩展到其他职业
1. 创建新的 `XXRotationEntry : IRotationEntry`
2. 实现 `Build(settingFolder)` → 返回 `Rotation`（含 SlotResolvers/Opener/Handler 等）
3. 如有需求，实现 `IRotationUI` 注册悬浮窗控件
4. `TargetJobs` 返回对应职业枚举
