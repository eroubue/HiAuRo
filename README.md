# HiAuRo

面向 FFXIV 的 Dalamud 战斗辅助**框架/平台**（.NET 10，Dalamud.CN.NET.Sdk 15.0.0）。<br>
不是职业循环实现 — 提供运行时、数据层、ACR 接口和执行控制，供 ACR 作者开发职业逻辑。

## 快速开始

```bash
# 1. 克隆（含子模块）
git clone --recurse-submodules <repo-url>
cd HiAuRo

# 2. 设置 DALAMUD_HOME 环境变量（指向 Dalamud dev 目录）
#    Windows 原生：
#      $env:DALAMUD_HOME = "$env:APPDATA\XIVLauncherCN\addon\Hooks\dev"
#    WSL（见下方 WSL 章节）

# 3. 编译
dotnet build HiAuRo/HiAuRo.csproj -nologo

# 输出: HiAuRo/bin/Debug/HiAuRo-0.1.0.zip
```

## 技术栈

| 项目 | 说明 |
|------|------|
| .NET 10.0 / C# 13 | 运行时与语言 |
| [Dalamud.CN.NET.Sdk](https://www.nuget.org/packages/Dalamud.CN.NET.Sdk) 15.0.0 | 国服 Dalamud 插件 SDK |
| [OmenTools](https://github.com/AtmoOmen/OmenTools) | DService 服务入口、ImGuiOm UI 组件 |
| CEF (Browsingway) | 游戏内嵌浏览器渲染（独立进程，D3D11 共享纹理） |
| Kestrel + WebSocket | Web UI 服务器 (`localhost:5678`) |
| FlatSharp | FlatBuffers 序列化（IPC 通信） |

## WSL 环境注意事项

WSL 下编译需要额外配置（已通过 `Directory.Build.props` 自动处理大部分问题）：

### 1. DALAMUD_HOME 环境变量

WSL 通过 `/mnt/c/` 挂载访问 Windows 文件系统。必须手动设置 `DALAMUD_HOME`：

```bash
# 添加到 ~/.bashrc 持久生效
export DALAMUD_HOME=/mnt/c/Users/<你的用户名>/AppData/Roaming/XIVLauncherCN/addon/Hooks/dev
```

> **注意：** `DALAMUD_HOME` 必须指向 `addon/Hooks/dev/` 子目录（`Dalamud.dll` 所在位置），不是 XIVLauncherCN 根目录。

### 2. .NET 运行时版本

项目需要 **.NET 9.0 运行时**（FlatSharp.Compiler 工具的依赖）**和** **.NET 10.0 SDK**：

```bash
# 安装 .NET 10 SDK（如果未安装）
sudo apt install -y dotnet-sdk-10.0

# 安装 .NET 9 运行时（FlatSharp.Compiler 需要）
# 如果 apt 源找不到 dotnet-runtime-9.0 包，用脚本安装：
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
sudo /tmp/dotnet-install.sh --runtime dotnet --version 9.0.15 --install-dir /usr/lib/dotnet

# 验证
dotnet --list-runtimes
# 应该看到: Microsoft.NETCore.App 9.0.x 和 10.0.x
```

### 3. `host/fxr` 缺失问题

如果遇到 `Error: [/usr/lib/dotnet/host/fxr] does not exist`，说明只安装了 `dotnet-host-10.0` 但缺少 `dotnet-hostfxr-10.0`：

```bash
sudo apt install -y dotnet-hostfxr-10.0
```

### 4. Submodule 初始化

OmenTools 是 git submodule，克隆后必须初始化：

```bash
git submodule update --init
```

### 5. `Directory.Build.props` 自动配置

仓库根目录的 `Directory.Build.props` 提供以下通用配置：

| 配置项 | 说明 |
|--------|------|
| `EnableWindowsTargeting=true` | 允许 Linux 下编译 `net10.0-windows` 目标 |
| `DALAMUD_HOME` 构建前验证 | 未设置时给出中文错误提示，指出正确路径格式 |

### 6. 常见构建错误速查

| 错误 | 原因 | 解决 |
|------|------|------|
| `NETSDK1100: EnableWindowsTargeting` | WSL 下编译 Windows 目标 | 已通过 `Directory.Build.props` 修复 |
| `Dalamud not found at ...` | `DALAMUD_HOME` 路径不对 | 确认指向 `addon/Hooks/dev/` |
| `FlatSharp.Compiler` / `net9.0` not found | 缺少 .NET 9 运行时 | 安装 `dotnet-runtime-9.0` |
| `host/fxr does not exist` | 缺少 hostfxr 包 | `sudo apt install dotnet-hostfxr-10.0` |
| `OmenTools.csproj not found` | submodule 未初始化 | `git submodule update --init` |

## 项目结构

```
.
├── HiAuRo/                # 插件主项目
│   ├── ACR/               # ACR 抽象层（Rotation/SlotResolver/Target/Spell）
│   ├── Command/           # /hi 命令系统
│   ├── Data/              # 游戏数据层（Self/Target/Party/Objects）+ HelperContext
│   ├── Execution/         # 执行轴（TriggerLine/NodeProgressor）
│   ├── Infrastructure/    # 配置、日志
│   ├── Runtime/           # 运行时核心（Tick/AIRunner/ACRLifecycle）+ HelperUpdater
│   ├── Setting/           # 设置管理
│   └── UI/                # Web UI + WebSocket + ImGui 主窗口
├── HiAuRo.Helper/         # 职业数据辅助库（独立公开 repo）
├── Browsingway/           # CEF 游戏内浏览器渲染（external）
├── OmenTools/             # Dalamud 服务封装（git submodule）
├── example/BRD/           # 诗人 ACR 打样实例
├── doc/                   # 设计文档
└── Directory.Build.props  # 全局 MSBuild 配置
```

## HiAuRo.Sdk NuGet 包

ACR 开发者无需配置开发环境，一个包搞定全部依赖。

```bash
dotnet nuget add source "https://nuget.pkg.github.com/denghaoxuan991876906/index.json" -n github
dotnet add package HiAuRo.Sdk
```

包含：HiAuRo.dll + HiAuRo.Helper.dll + OmenTools.dll + 11 个 Dalamud 运行时 DLL。

## 开发

### 增量构建关键点

- 主项目无 `.sln`，直接 build `.csproj`
- `Browsingway.Common` 必须先构建（FlatBuffers schema 生成）
- OmenTools 是外部 submodule，不直接修改
- Browsingway 和 OmenTools 都是 `Dalamud.CN.NET.Sdk` 项目，也需要 `DALAMUD_HOME`

### 调试

- Web UI 开发：浏览器直接打开 `http://localhost:5678`，无需启动游戏
- 游戏内调试：XIVLauncherCN → DevPlugins → 加载 `HiAuRo/bin/Debug/` 目录

## License

[待定]
