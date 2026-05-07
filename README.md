# HiAuRo

FFXIV Dalamud 战斗辅助框架（.NET 10，Dalamud.CN.NET.Sdk 15.0.0）。

不是职业循环 — 提供运行时、数据层、ACR 接口和执行控制。

[![Build](https://github.com/denghaoxuan991876906/HiAuRo/actions/workflows/ci.yml/badge.svg)](https://github.com/denghaoxuan991876906/HiAuRo/actions)

## ACR 开发者 — 一键开始

零配置，一个 NuGet 包搞定全部依赖：

```bash
dotnet nuget add source "https://nuget.pkg.github.com/denghaoxuan991876906/index.json" -n github
dotnet add package HiAuRo.Sdk
```

```csharp
using HiAuRo.ACR;
using HiAuRo.Helper;

public class MyBrdAcr : IRotationEntry
{
    public IEnumerable<Jobs> TargetJobs => [Jobs.BRD];
    public Rotation? Build(string settingFolder) { /* ... */ return new Rotation(); }
}
```

## 编译

```bash
git clone --recurse-submodules https://github.com/denghaoxuan991876906/HiAuRo
cd HiAuRo
export DALAMUD_HOME=/path/to/XIVLauncherCN/addon/Hooks/dev
dotnet build HiAuRo/HiAuRo.csproj
```

## 项目结构

```
HiAuRo/              ← 主插件
├── ACR/             ← 接口 + Helper + 类型
├── Command/         ← /hi 命令
├── Data/            ← 游戏数据层
├── Execution/       ← 执行轴 + 触发器
├── Runtime/         ← 运行时 + HelperUpdater
├── UI/              ← Web UI + ImGui
├── Authoring/       ← 编辑器后端
├── FactAxis/        ← 事实轴
└── Decision/        ← 决策层

HiAuRo.Helper/       ← 职业数据辅助库（独立 repo）
Browsingway/         ← CEF 渲染
OmenTools/           ← Dalamud 服务封装（submodule）
example/BRD/         ← ACR 打样
```

## 命令

| 命令 | 说明 |
|------|------|
| `/hi on/off/toggle` | 启停 ACR |
| `/hi status` | 查看状态 |
| `/hi fact` | 切换事实轴 |
| `/hi assist load/unload` | 辅助轴加载/卸载 |
| `/hi reload` | 重新扫描 ACR |

## 相关仓库

| 仓库 | 说明 |
|------|------|
| [HiAuRo.Helper](https://github.com/denghaoxuan991876906/HiAuRo.Helper) | 全职业数据辅助库 |
| [HiAuRo.Sdk](https://github.com/denghaoxuan991876906/HiAuRo/pkgs/nuget/HiAuRo.Sdk) | ACR 开发 NuGet 包 |
