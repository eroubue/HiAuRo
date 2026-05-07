# HiAuRo

面向 FFXIV 的 Dalamud 战斗辅助**框架/平台**（.NET 10，Dalamud.CN.NET.Sdk 15.0.0）。

不是职业循环实现 — 提供运行时、数据层、ACR 接口和执行控制，供 ACR 作者开发职业逻辑。

[![Build](https://github.com/denghaoxuan991876906/HiAuRo/actions/workflows/ci.yml/badge.svg)](https://github.com/denghaoxuan991876906/HiAuRo/actions)

## 快速开始

```bash
git clone --recurse-submodules https://github.com/denghaoxuan991876906/HiAuRo
cd HiAuRo

# 设置 DALAMUD_HOME
export DALAMUD_HOME=/mnt/c/Users/<用户名>/AppData/Roaming/XIVLauncherCN/addon/Hooks/dev

# 初始化 Helper 子模块
cd HiAuRo.Helper && git submodule update --init && cd ..

dotnet build HiAuRo/HiAuRo.csproj
```

## 项目结构

```
HiAuRo/                     ← 主插件
├── ACR/                    ← ACR 接口 + Helper + 类型
├── Command/                ← /hi 命令系统
├── Data/                   ← 游戏数据层 + HelperContext
├── Execution/              ← 执行轴 + 触发器
├── Runtime/                ← 运行时核心 + HelperUpdater（自动更新）
├── Setting/                ← 设置管理
├── UI/                     ← Web UI + ImGui 面板
├── Authoring/              ← 可视化编辑器后端
├── FactAxis/               ← 事实轴
└── Decision/               ← 智能决策层

HiAuRo.Helper/              ← 职业数据辅助库（独立公开 repo）
Browsingway/                ← CEF 浏览器渲染
OmenTools/                  ← Dalamud 服务封装（submodule）
example/BRD/                ← 诗人 ACR 打样
doc/                        ← 设计文档
```

## 技术栈

| 项目 | 说明 |
|------|------|
| .NET 10.0 / C# 13 | 运行时与语言 |
| [Dalamud.CN.NET.Sdk](https://www.nuget.org/packages/Dalamud.CN.NET.Sdk) 15.0.0 | 国服 Dalamud 插件 SDK |
| [OmenTools](https://github.com/AtmoOmen/OmenTools) | DService 服务入口 |
| CEF (Browsingway) | 游戏内嵌浏览器渲染 |
| Kestrel + WebSocket | Web UI 服务器 |
| [HiAuRo.Helper](https://github.com/denghaoxuan991876906/HiAuRo.Helper) | 全职业数据辅助库 |

## ACR 开发者

无需搭建本项目，直接引用 NuGet 包：

```bash
dotnet nuget add source "https://nuget.pkg.github.com/denghaoxuan991876906/index.json" -n github
dotnet add package HiAuRo.Sdk
```

包含：HiAuRo.dll + HiAuRo.Helper.dll + OmenTools.dll + 11 个 Dalamud 运行时 DLL。

详见 [HiAuRo.Helper](https://github.com/denghaoxuan991876906/HiAuRo.Helper) 仓库。

## WSL 环境注意事项

### DALAMUD_HOME

```bash
export DALAMUD_HOME=/mnt/c/Users/<用户名>/AppData/Roaming/XIVLauncherCN/addon/Hooks/dev
```

### .NET 运行时

需要 .NET 9.0 运行时（FlatSharp 工具）+ .NET 10.0 SDK：

```bash
sudo apt install -y dotnet-sdk-10.0 dotnet-hostfxr-10.0
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --runtime dotnet --version 9.0.15 --install-dir /usr/lib/dotnet
```

### 常见错误

| 错误 | 解决 |
|------|------|
| `NETSDK1100: EnableWindowsTargeting` | 已通过 `Directory.Build.props` 修复 |
| `FlatSharp.Compiler / net9.0` | 安装 .NET 9 运行时 |
| `host/fxr does not exist` | `sudo apt install dotnet-hostfxr-10.0` |
| `OmenTools.csproj not found` | `git submodule update --init` |

## 开发

- 主项目无 `.sln`，直接 `dotnet build HiAuRo/HiAuRo.csproj`
- Web UI 开发：浏览器打开 `http://localhost:5678`
- 游戏内调试：XIVLauncherCN → DevPlugins → 加载 `HiAuRo/bin/Debug/`
- 执行轴自动加载：放 JSON 到 `ConfigDir/ExecutionTimelines/{副本ID}.json`
- 命令：`/hi on|off|toggle|status|fact|assist load|assist unload`
