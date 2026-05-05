# HiAuRo BRD ACR 示例

## 构建

```bash
dotnet build BRD.csproj
```

输出: `bin\Debug\HiAuRo.BRD.dll`

构建后自动复制到 `%APPDATA%\XIVLauncherCN\devPlugins\HiAuRo\ACR\HiAuRo\`。

游戏内 `/hi reload` 热加载。

## 引用

| DLL | 来源 | 用途 |
|-----|------|------|
| HiAuRo.dll | `devPlugins\HiAuRo\` | IRotationEntry / Slot / Spell / Data 等 |
| OmenTools.dll | `installedPlugins\OmenTools\` | DService / GameState / LocalPlayerState 等 |
| Dalamud SDK | 编译环境自带 | `Dalamud.Plugin`, ImGui, Lumina 等 |

## 自定义路径

```bash
dotnet build -p:HiAuRoDir=D:\my\path\ -p:OmenToolsDir=D:\other\
```

## 多职业支持

一个 DLL 可以包含多个 `IRotationEntry` 实现（每个对应一个职业）。ACRLoader 扫描时自动发现所有实现类。
