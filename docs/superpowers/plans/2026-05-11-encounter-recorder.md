# 副本事件记录器 实现计划

> **For agentic workers:** 使用 subagent-driven-development 逐任务执行。步骤使用 checkbox (`- [ ]`) 跟踪。

**Goal:** 为 HiAuRo 增加副本战斗事件自动录制子系统，C# 录制引擎自动保存 EncounterRecord JSON，事实轴编辑器可从录制文件中选择事件导入到时间线。

**Architecture:** C# RecordingEngine 订阅 GameEventHook.OnEventFired + CombatContext.StateChanged，自动在进战时开始录制、脱战时保存文件。录制数据以 EncounterRecord JSON 格式存储在 ConfigDirectory/Recordings/。前端事实轴编辑器（纯本地）新增导入功能：加载 JSON → 按分类过滤 → 勾选事件批量转换为 FactEvent 插入时间线。

**Tech Stack:** .NET 10 (C# Dalamud Plugin), OmenTools, vanilla JavaScript, HTML/CSS

---

### 文件结构

| 操作 | 路径 | 职责 |
|------|------|------|
| **Create** | `HiAuRo/Recording/EncounterRecord.cs` | C# 数据模型 + CombatClock |
| **Create** | `HiAuRo/Recording/EncounterRecorder.cs` | 主控制器 + 所有序列化器 |
| **Modify** | `HiAuRo/Plugin.cs` | Init/Shutdown 集成 |
| **Modify** | `HiAuRo/UI/MainWindow.cs` | 新增"录制"Tab |
| **Modify** | `HiAuRo/UI/web/fact-editor.html` | 新增按钮 + 录制侧边栏 |
| **Modify** | `HiAuRo/UI/web/fact-editor.js` | 录制加载/过滤/导入逻辑 |
| **Modify** | `HiAuRo/UI/web/fact-editor.css` | 录制面板样式 |

---

### Task 1: EncounterRecord.cs — 数据模型

**Files:**
- Create: `HiAuRo/Recording/EncounterRecord.cs`

- [ ] **Step 1: 创建数据模型文件**

```csharp
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace HiAuRo.Recording;

public sealed class EncounterRecord
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("territoryId")]
    public uint TerritoryId { get; set; }

    [JsonPropertyName("territoryName")]
    public string TerritoryName { get; set; } = "";

    [JsonPropertyName("recordedAt")]
    public string RecordedAt { get; set; } = "";

    [JsonPropertyName("totalTimeMs")]
    public long TotalTimeMs { get; set; }

    [JsonPropertyName("partySize")]
    public int PartySize { get; set; }

    [JsonPropertyName("jobComposition")]
    public List<string> JobComposition { get; set; } = [];

    [JsonPropertyName("bosses")]
    public List<BossInfo> Bosses { get; set; } = [];

    [JsonPropertyName("events")]
    public List<EncounterEvent> Events { get; set; } = [];
}

public sealed class BossInfo
{
    [JsonPropertyName("npcId")]
    public uint NpcId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("hpMax")]
    public uint HpMax { get; set; }
}

public sealed class EncounterEvent
{
    [JsonPropertyName("timeMs")]
    public long TimeMs { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("data")]
    public Dictionary<string, object?> Data { get; set; } = [];
}

/// <summary>
/// 战斗时钟 — 进战归零，脱战暂停
/// </summary>
public sealed class CombatClock
{
    private readonly Stopwatch _sw = new();
    private long _baseMs;

    public void Reset()
    {
        _baseMs = 0;
        _sw.Restart();
    }

    public void Pause()
    {
        _baseMs += _sw.ElapsedMilliseconds;
        _sw.Stop();
    }

    public void Resume() => _sw.Start();

    public long Now => _baseMs + _sw.ElapsedMilliseconds;
}
```

- [ ] **Step 2: 验证编译**

```bash
dotnet build HiAuRo/HiAuRo.csproj -nologo
```

期望: 编译通过。

- [ ] **Step 3: Commit**

```bash
git add HiAuRo/Recording/EncounterRecord.cs
git commit -m "feat(recording): add EncounterRecord data model and CombatClock"
```

---

### Task 2: EncounterRecorder.cs — 主控制器 + 序列化器注册

**Files:**
- Create: `HiAuRo/Recording/EncounterRecorder.cs`

**前置条件:** Task 1 完成。

- [ ] **Step 1: 创建主控制器框架**

```csharp
using System.Diagnostics;
using System.Text.Json;
using HiAuRo.Execution.Events;
using HiAuRo.Runtime;
using OmenTools.OmenService;

namespace HiAuRo.Recording;

public sealed class EncounterRecorder
{
    public static EncounterRecorder Instance { get; } = new();

    private readonly CombatClock _clock = new();
    private EncounterRecord? _current;
    private bool _initialized;
    private string _saveDir = "";
    private long _currentCombatStartMs;
    private readonly HashSet<uint> _bossNpcIds = [];

    private EncounterRecorder() { }

    #region 生命周期

    public void Init()
    {
        if (_initialized) return;
        _initialized = true;

        _saveDir = Path.Combine(
            DService.Instance().PI.ConfigDirectory.FullName, "Recordings");
        Directory.CreateDirectory(_saveDir);

        GameEventHook.Instance.OnEventFired += OnGameEvent;
        CombatContext.StateChanged += OnCombatStateChanged;
        RegisterSerializers();

        _bossNpcIds.Clear();
    }

    public void Shutdown()
    {
        if (!_initialized) return;
        _initialized = false;

        GameEventHook.Instance.OnEventFired -= OnGameEvent;
        CombatContext.StateChanged -= OnCombatStateChanged;

        if (_current != null && _current.Events.Count > 0)
            SaveRecord();

        _serializers.Clear();
        _bossNpcIds.Clear();
    }

    #endregion

    #region 战斗状态回调

    private void OnCombatStateChanged(CombatContext.State oldState, CombatContext.State newState)
    {
        if (newState == CombatContext.State.InCombat)
        {
            StartRecording();
        }
        else if (newState == CombatContext.State.OutOfCombat && _current != null)
        {
            _clock.Pause();
            SaveRecord();
        }
    }

    private void StartRecording()
    {
        _clock.Reset();
        _currentCombatStartMs = Environment.TickCount64;

        _current = new EncounterRecord
        {
            TerritoryId = OmenService.GameState.TerritoryType,
            TerritoryName = GetTerritoryName(),
            RecordedAt = DateTime.UtcNow.ToString("O"),
            Events = []
        };

        // 采集队伍信息
        var pt = DService.Instance().Party;
        if (pt != null)
        {
            _current.PartySize = pt.Count;
            var jobs = new List<string>();
            foreach (var member in pt)
            {
                if (member != null)
                {
                    var name = member.ClassJob?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(name))
                        jobs.Add(name);
                }
            }
            _current.JobComposition = jobs;
        }
    }

    private string GetTerritoryName()
    {
        try
        {
            var sheet = DService.Instance().DM.GameData.GetExcelSheet<
                Lumina.Excel.Sheets.TerritoryType>();
            if (sheet != null)
            {
                var row = sheet.GetRow(OmenService.GameState.TerritoryType);
                var placeName = row.PlaceName.Value;
                return placeName.Name.ToString();
            }
        }
        catch { }
        return $"Territory_{OmenService.GameState.TerritoryType}";
    }

    private void SaveRecord()
    {
        if (_current == null) return;

        _current.TotalTimeMs = _clock.Now;
        _current.Bosses = _bossNpcIds.Select(id =>
        {
            var obj = DService.Instance().ObjectTable.SearchByID(id);
            return new BossInfo
            {
                NpcId = obj is OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds.IBattleNPC bn ? bn.DataID : 0,
                Name = obj?.Name.ToString() ?? "",
                HpMax = (obj is OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds.IBattleNPC bn2) ? bn2.MaxHP : 0
            };
        }).ToList();

        var json = JsonSerializer.Serialize(_current,
            new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var safeName = SanitizeFileName(_current.TerritoryName) + "_" +
                       DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json";
        var path = Path.Combine(_saveDir, safeName);

        File.WriteAllText(path, json);
        DService.Instance().Log.Information($"[Recording] 已保存: {path} ({_current.Events.Count} 事件)");

        _current = null;
        _bossNpcIds.Clear();
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var arr = name.Where(c => !invalid.Contains(c)).ToArray();
        return new string(arr);
    }

    #endregion

    #region 事件序列化

    private readonly Dictionary<Type, Func<ITriggerCondParams, long, EncounterEvent>> _serializers = [];

    private void RegisterSerializers()
    {
        // ---- cast ----
        _serializers[typeof(ActorCastParams)] = (p, t) =>
        {
            var cp = (ActorCastParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(ActorCastParams),
                Category = "cast",
                Data = new()
                {
                    ["actionId"] = cp.ActionID,
                    ["castTime"] = cp.CastTime,
                    ["targetId"] = cp.TargetID,
                    ["sourceId"] = cp.SourceID,
                    ["posX"] = cp.PosX,
                    ["posY"] = cp.PosY,
                    ["posZ"] = cp.PosZ,
                }
            };
        };

        _serializers[typeof(AfterSpellParams)] = (p, t) =>
        {
            var ap = (AfterSpellParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(AfterSpellParams),
                Category = "cast",
                Data = new() { ["spellId"] = ap.SpellID }
            };
        };

        // ---- ability ----
        _serializers[typeof(ActionEffectParams)] = (p, t) =>
        {
            var ap = (ActionEffectParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(ActionEffectParams),
                Category = "ability",
                Data = new()
                {
                    ["actionId"] = ap.ActionID,
                    ["sourceId"] = ap.SourceID,
                    ["targetOid"] = ap.TargetOID,
                    ["animationId"] = ap.AnimationID,
                    ["effectType"] = ap.EffectType,
                }
            };
        };

        _serializers[typeof(NoTargetAbilityEffectParams)] = (p, t) =>
        {
            var np = (NoTargetAbilityEffectParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(NoTargetAbilityEffectParams),
                Category = "ability",
                Data = new()
                {
                    ["sourceId"] = np.SourceID,
                    ["actionId"] = np.ActionID,
                    ["posX"] = np.PosX,
                    ["posY"] = np.PosY,
                    ["posZ"] = np.PosZ,
                }
            };
        };

        // ---- buff ----
        _serializers[typeof(BuffGainParams)] = (p, t) =>
        {
            var bp = (BuffGainParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(BuffGainParams),
                Category = "buff",
                Data = new()
                {
                    ["sourceId"] = bp.SourceID,
                    ["statusId"] = bp.StatusID,
                    ["stackCount"] = bp.StackCount,
                }
            };
        };

        _serializers[typeof(BuffRemoveParams)] = (p, t) =>
        {
            var bp = (BuffRemoveParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(BuffRemoveParams),
                Category = "buff",
                Data = new()
                {
                    ["sourceId"] = bp.SourceID,
                    ["statusId"] = bp.StatusID,
                }
            };
        };

        // ---- tether ----
        _serializers[typeof(TetherCreateParams)] = (p, t) =>
        {
            var tp = (TetherCreateParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(TetherCreateParams),
                Category = "tether",
                Data = new()
                {
                    ["tetherId"] = tp.TetherID,
                    ["sourceId"] = tp.SourceID,
                    ["targetOid"] = tp.TargetOID,
                    ["param2"] = tp.Param2,
                    ["param3"] = tp.Param3,
                    ["param5"] = tp.Param5,
                }
            };
        };

        _serializers[typeof(TetherRemoveParams)] = (p, t) =>
        {
            var tp = (TetherRemoveParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(TetherRemoveParams),
                Category = "tether",
                Data = new()
                {
                    ["sourceId"] = tp.SourceID,
                    ["param2"] = tp.Param2,
                    ["param3"] = tp.Param3,
                    ["param5"] = tp.Param5,
                }
            };
        };

        // ---- spawn ----
        _serializers[typeof(UnitCreateParams)] = (p, t) =>
        {
            var up = (UnitCreateParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(UnitCreateParams),
                Category = "spawn",
                Data = new()
                {
                    ["entityId"] = up.EntityId,
                    ["dataId"] = up.DataId,
                    ["name"] = up.Name,
                }
            };
        };

        _serializers[typeof(UnitDeleteParams)] = (p, t) =>
        {
            var up = (UnitDeleteParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(UnitDeleteParams),
                Category = "spawn",
                Data = new()
                {
                    ["entityId"] = up.EntityId,
                    ["dataId"] = up.DataId,
                    ["name"] = up.Name,
                }
            };
        };

        // ---- death ----
        _serializers[typeof(ActorControlDeathParams)] = (p, t) =>
        {
            var dp = (ActorControlDeathParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(ActorControlDeathParams),
                Category = "death",
                Data = new()
                {
                    ["sourceId"] = dp.SourceID,
                    ["targetId"] = dp.TargetID,
                }
            };
        };

        // ---- target ----
        _serializers[typeof(ActorControlTargetIconParams)] = (p, t) =>
        {
            var tp = (ActorControlTargetIconParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(ActorControlTargetIconParams),
                Category = "target",
                Data = new()
                {
                    ["sourceId"] = tp.SourceID,
                    ["targetId"] = tp.TargetID,
                    ["iconId"] = tp.IconID,
                }
            };
        };

        _serializers[typeof(ActorControlTargetableParams)] = (p, t) =>
        {
            var tp = (ActorControlTargetableParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(ActorControlTargetableParams),
                Category = "target",
                Data = new()
                {
                    ["sourceId"] = tp.SourceID,
                    ["targetId"] = tp.TargetID,
                    ["isTargetable"] = tp.IsTargetable,
                }
            };
        };

        // ---- combat ----
        _serializers[typeof(CombatStateParams)] = (p, t) =>
        {
            var cp = (CombatStateParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(CombatStateParams),
                Category = "combat",
                Data = new() { ["isEntering"] = cp.IsEntering }
            };
        };

        _serializers[typeof(ActorControlCombatParams)] = (p, t) =>
        {
            var cp = (ActorControlCombatParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(ActorControlCombatParams),
                Category = "combat",
                Data = new() { ["isEntering"] = cp.IsEntering }
            };
        };

        // ---- environment ----
        _serializers[typeof(MapEffectParams)] = (p, t) =>
        {
            var mp = (MapEffectParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(MapEffectParams),
                Category = "environment",
                Data = new()
                {
                    ["positionIndex"] = mp.PositionIndex,
                    ["param1"] = mp.Param1,
                    ["param2"] = mp.Param2,
                }
            };
        };

        _serializers[typeof(EnvControlParams)] = (p, t) =>
        {
            var ep = (EnvControlParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(EnvControlParams),
                Category = "environment",
                Data = new()
                {
                    ["index"] = ep.Index,
                    ["flag"] = ep.Flag,
                }
            };
        };

        _serializers[typeof(WeatherChangedParams)] = (p, t) =>
        {
            var wp = (WeatherChangedParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(WeatherChangedParams),
                Category = "environment",
                Data = new() { ["newWeatherId"] = wp.NewWeatherId }
            };
        };

        // ---- npc ----
        _serializers[typeof(NpcYellParams)] = (p, t) =>
        {
            var np = (NpcYellParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(NpcYellParams),
                Category = "npc",
                Data = new()
                {
                    ["sourceId"] = np.SourceID,
                    ["sourceName"] = np.SourceName,
                    ["yellId"] = np.YellID,
                    ["yellMsg"] = np.YellMsg,
                }
            };
        };

        // ---- director ----
        _serializers[typeof(DirectorUpdateParams)] = (p, t) =>
        {
            var dp = (DirectorUpdateParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(DirectorUpdateParams),
                Category = "director",
                Data = new()
                {
                    ["category"] = dp.Category.ToString(),
                    ["param1"] = dp.Param1,
                    ["param2"] = dp.Param2,
                    ["param3"] = dp.Param3,
                    ["param4"] = dp.Param4,
                    ["a6"] = dp.A6,
                    ["a7"] = dp.A7,
                    ["a8"] = dp.A8,
                    ["a9"] = dp.A9,
                }
            };
        };

        _serializers[typeof(ActorControlTimelineParams)] = (p, t) =>
        {
            var tp = (ActorControlTimelineParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(ActorControlTimelineParams),
                Category = "director",
                Data = new()
                {
                    ["sourceId"] = tp.SourceID,
                    ["timelineId"] = tp.TimelineID,
                }
            };
        };

        // ---- actorControl ----
        _serializers[typeof(ActorControlParams)] = (p, t) =>
        {
            var ap = (ActorControlParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(ActorControlParams),
                Category = "actorControl",
                Data = new()
                {
                    ["sourceId"] = ap.SourceID,
                    ["command"] = ap.Command,
                    ["p1"] = ap.P1,
                    ["p2"] = ap.P2,
                    ["p3"] = ap.P3,
                    ["p4"] = ap.P4,
                    ["p5"] = ap.P5,
                    ["p6"] = ap.P6,
                    ["targetId"] = ap.TargetID,
                }
            };
        };

        // ---- chat ----
        _serializers[typeof(ChatMessageParams)] = (p, t) =>
        {
            var cp = (ChatMessageParams)p;
            return new EncounterEvent
            {
                TimeMs = t,
                Type = nameof(ChatMessageParams),
                Category = "chat",
                Data = new() { ["message"] = cp.Message }
            };
        };
    }

    private void OnGameEvent(ITriggerCondParams condParams)
    {
        if (_current == null) return;

        // 尝试识别 Boss NPC (来自 cast 事件的高HP敌人)
        if (condParams is ActorCastParams acp && acp.SourceID != 0)
        {
            try
            {
                var obj = DService.Instance().ObjectTable.SearchByID(acp.SourceID);
                if (obj is OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds.IBattleNPC bn
                    && bn.MaxHP > 1000)
                {
                    _bossNpcIds.Add(acp.SourceID);
                }
            }
            catch { }
        }

        var type = condParams.GetType();
        if (!_serializers.TryGetValue(type, out var serializer)) return;

        try
        {
            var evt = serializer(condParams, _clock.Now);
            _current.Events.Add(evt);
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[Recording] 序列化失败 {type.Name}: {ex}");
        }
    }

    #endregion
}
```

- [ ] **Step 2: 验证编译**

```bash
dotnet build HiAuRo/HiAuRo.csproj -nologo
```

期望: 编译通过。

- [ ] **Step 3: Commit**

```bash
git add HiAuRo/Recording/EncounterRecorder.cs
git commit -m "feat(recording): add EncounterRecorder controller with all ITriggerCondParams serializers"
```

---

### Task 3: Plugin.cs + MainWindow.cs 集成

**Files:**
- Modify: `HiAuRo/Plugin.cs`
- Modify: `HiAuRo/UI/MainWindow.cs`

**前置条件:** Task 1, Task 2 完成。

- [ ] **Step 1: Plugin.cs 构造函数 — 在 GameEventHook.Init() 之后添加 Recorder Init**

找到 `Plugin.cs` 中这一行（约第 58 行）:
```csharp
GameEventHook.Instance.Init();
```

在其后添加:
```csharp
EncounterRecorder.Instance.Init();
```

- [ ] **Step 2: Plugin.cs Dispose() — 添加 Recorder Shutdown**

找到 `Plugin.cs` 中 `SafeDispose()` 方法（约第 142 行），在 `GameEventHook.Instance.Shutdown();` 之前添加:
```csharp
EncounterRecorder.Instance.Shutdown();
```

找到 `Dispose()` 方法（约第 212 行），在 `GameEventHook.Instance.Shutdown();` 之前添加:
```csharp
EncounterRecorder.Instance.Shutdown();
```

- [ ] **Step 3: Plugin.cs 添加 using**

在文件顶部 using 区域添加:
```csharp
using HiAuRo.Recording;
```

- [ ] **Step 4: MainWindow.cs — 新增"录制"Tab**

在 `MainWindow.cs` 的 `Draw()` 方法中（约第 53 行，`ImGui.EndTabBar()` 之前），添加新 Tab:

```csharp
if (ImGui.BeginTabItem("录制"))
{
    DrawRecording();
    ImGui.EndTabItem();
}
```

然后在 `MainWindow.cs` 末尾（`}` 之前）添加 `DrawRecording()` 方法:

```csharp
private static void DrawRecording()
{
    ImGui.Spacing();
    ImGui.Text("副本录制状态");

    var recorder = EncounterRecorder.Instance;
    var isRecording = recorder.IsRecording;

    if (isRecording)
    {
        var seconds = recorder.ElapsedSeconds;
        ImGui.TextColored(new Vector4(1, 0.3f, 0.3f, 1),
            $"● 录制中 ({seconds / 60:D2}:{seconds % 60:D2})");
        ImGui.Text($"文件名: {recorder.CurrentFileName}");
    }
    else
    {
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), "○ 就绪");
    }

    ImGui.Spacing();
    ImGui.Separator();

    // 录制历史
    ImGui.Text("录制历史:");
    ImGui.Spacing();

    var files = recorder.GetRecordFiles();
    if (files.Length == 0)
    {
        ImGui.TextDisabled("暂无录制记录");
    }
    else
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1);
        ImGui.BeginChild("##RecordingList",
            new Vector2(-1, 80), true);

        foreach (var (name, path) in files.TakeLast(20).Reverse())
        {
            ImGui.Text(name);
            ImGui.SameLine();
            ImGui.TextDisabled($"({path})");
        }

        ImGui.EndChild();
        ImGui.PopStyleVar();
    }

    ImGui.Spacing();
    if (ImGui.Button("打开录制目录"))
    {
        var dir = Path.Combine(
            DService.Instance().PI.ConfigDirectory.FullName, "Recordings");
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", dir);
        }
        catch { }
    }
}
```

- [ ] **Step 5: 为 EncounterRecorder 添加 IsRecording / ElapsedSeconds / CurrentFileName / GetRecordFiles 属性**

在 `EncounterRecorder.cs` 添加:

```csharp
public bool IsRecording => _current != null;

public int ElapsedSeconds => (int)(_clock.Now / 1000);

public string CurrentFileName
{
    get
    {
        if (_current == null) return "";
        var safe = SanitizeFileName(_current.TerritoryName) + "_" +
                   DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json";
        return safe;
    }
}

public (string Name, string Path)[] GetRecordFiles()
{
    if (!Directory.Exists(_saveDir)) return [];
    return Directory.GetFiles(_saveDir, "*.json")
        .Select(f => (Path.GetFileName(f), f))
        .OrderByDescending(x => x.Item2)
        .ToArray();
}
```

- [ ] **Step 6: MainWindow.cs 添加 using**

在文件顶部添加:
```csharp
using HiAuRo.Recording;
```

- [ ] **Step 7: 验证编译**

```bash
dotnet build HiAuRo/HiAuRo.csproj -nologo
```

期望: 编译通过。

- [ ] **Step 8: Commit**

```bash
git add HiAuRo/Plugin.cs HiAuRo/UI/MainWindow.cs HiAuRo/Recording/EncounterRecorder.cs
git commit -m "feat(recording): integrate EncounterRecorder into Plugin lifecycle and MainWindow"
```

---

### Task 4: fact-editor.html — UI 适配

**Files:**
- Modify: `HiAuRo/UI/web/fact-editor.html`

**前置条件:** 无（前端独立任务）。

- [ ] **Step 1: 工具栏新增"从录制导入"按钮**

在 `fact-editor.html` 的 `<div class="actions">` 内添加:

```html
<button class="p-btn" id="btnRecording">从录制导入</button>
```

放在 `btnExport` 之后。

- [ ] **Step 2: 添加录制侧边栏 HTML**

在 `fact-editor.html` 的 `<aside class="sidebar-right" id="propPanel"></aside>` 之**后**（作为兄弟元素）添加:

```html
<aside class="sidebar-right" id="recordingPanel" style="display:none">
  <div class="recording-toolbar">
    <h3>录制数据</h3>
    <button class="p-btn" id="btnRecordingClose" style="padding:2px 8px;font-size:11px">关闭</button>
  </div>
  <div class="recording-info" id="recordingInfo"></div>
  <div class="recording-filters" id="recordingFilters"></div>
  <div class="recording-events" id="recordingEvents"></div>
  <div class="recording-actions">
    <button class="p-btn p-prim-col" id="btnImportToPhase">导入到当前阶段</button>
  </div>
</aside>
```

- [ ] **Step 3: 添加隐藏的文件输入用于录制 JSON**

在现有 `<input type="file" id="fileInput" ...>` 之后添加:

```html
<input type="file" id="recordingFileInput" accept=".json" style="display:none">
```

- [ ] **Step 4: Commit**

```bash
git add HiAuRo/UI/web/fact-editor.html
git commit -m "feat(recording): add import recording button and sidebar to fact-editor"
```

---

### Task 5: fact-editor.js — 录制加载/过滤/导入逻辑

**Files:**
- Modify: `HiAuRo/UI/web/fact-editor.js`

**前置条件:** Task 4 完成（HTML 元素存在）。

- [ ] **Step 1: 添加录制状态变量**

在 `fact-editor.js` 顶部变量声明区域（约第 48 行，`collapsedBranches` 之后）添加:

```javascript
// ---- 录制数据 ----
var recordingData = null;           // 已加载的 EncounterRecord
var recordingChecked = {};          // { eventIndex: true } 勾选状态
var recordingFilters = {            // 分类过滤状态
    cast: true, ability: true, buff: true, tether: true,
    spawn: true, death: true, target: true, combat: true,
    environment: true, npc: true, director: true,
    actorControl: false, chat: false
};
```

- [ ] **Step 2: 添加录制按钮事件绑定**

在 `fact-editor.js` 的 `DOMContentLoaded` 回调中（约第 1411-1415 行，`btnExport` 绑定之后）添加:

```javascript
document.getElementById('btnRecording').addEventListener('click', function() {
    document.getElementById('recordingFileInput').click();
});
document.getElementById('recordingFileInput').addEventListener('change', function() {
    if (this.files && this.files[0]) {
        loadRecordingFile(this.files[0]);
        this.value = '';
    }
});
document.getElementById('btnRecordingClose').addEventListener('click', hideRecordingPanel);
document.getElementById('btnImportToPhase').addEventListener('click', importRecordingToPhase);
```

- [ ] **Step 3: 实现录制核心函数**

在 `fact-editor.js` 末尾（约第 1597 行之后）添加:

```javascript
// ==================== 录制导入 ====================

function loadRecordingFile(file) {
    var reader = new FileReader();
    reader.onload = function() {
        try {
            recordingData = JSON.parse(reader.result);
            recordingChecked = {};
            for (var i = 0; i < (recordingData.events || []).length; i++) {
                recordingChecked[i] = false;
            }
            renderRecordingPanel();
        } catch (ex) {
            setStatus('录制文件解析失败: ' + esc(ex.message), 'error');
        }
    };
    reader.onerror = function() {
        setStatus('文件读取失败', 'error');
    };
    reader.readAsText(file);
}

function renderRecordingPanel() {
    if (!recordingData) return;

    // 显示录制面板，隐藏属性面板
    var propPanel = document.getElementById('propPanel');
    var recPanel = document.getElementById('recordingPanel');
    if (propPanel) propPanel.style.display = 'none';
    if (recPanel) recPanel.style.display = '';

    // 录制信息
    var info = document.getElementById('recordingInfo');
    if (info) {
        info.innerHTML =
            '<div><strong>' + esc(recordingData.territoryName || '未知副本') + '</strong></div>' +
            '<div style="font-size:10px;color:var(--tx2)">' +
            '时长: ' + formatTime((recordingData.totalTimeMs || 0) / 1000) +
            ' | 事件: ' + (recordingData.events ? recordingData.events.length : 0) +
            ' | 队伍: ' + (recordingData.jobComposition || []).join(', ') +
            '</div>';
    }

    // 分类过滤器
    var filtersEl = document.getElementById('recordingFilters');
    if (filtersEl) {
        var catNames = {
            cast: '读条技能', ability: '技能效果', buff: 'Buff',
            tether: '连线', spawn: '生成/消失', death: '死亡',
            target: '点名标记', combat: '战斗状态',
            environment: '环境特效', npc: 'NPC喊话',
            director: '时间轴/导演', actorControl: 'ActorControl', chat: '聊天'
        };
        var html = '<div style="font-size:11px;margin-bottom:4px;color:var(--tx1)">按分类过滤:</div>';
        for (var cat in recordingFilters) {
            var checked = recordingFilters[cat] ? ' checked' : '';
            html += '<label style="margin-right:8px;font-size:10px;cursor:pointer;white-space:nowrap">' +
                '<input type="checkbox" ' + checked +
                ' onchange="toggleRecordingFilter(\'' + cat + '\')">' +
                (catNames[cat] || cat) + '</label>';
        }
        filtersEl.innerHTML = html;
    }

    // 事件列表
    var eventsEl = document.getElementById('recordingEvents');
    if (eventsEl) {
        var filtered = filterRecordingEvents();
        if (filtered.length === 0) {
            eventsEl.innerHTML = '<div class="hint">没有匹配的事件</div>';
        } else {
            var listHtml = '';
            for (var i = 0; i < filtered.length; i++) {
                var ev = filtered[i];
                var origIdx = recordingData.events.indexOf(ev);
                var chk = recordingChecked[origIdx] ? ' checked' : '';
                var actionId = ev.data.actionId || ev.data.spellId || ev.data.statusId || ev.data.tetherId || '';
                var label = ev.category + (actionId ? ' #' + actionId : '') + ' @' + formatTime(ev.timeMs / 1000);
                if (ev.data.sourceName) label += ' [' + esc(String(ev.data.sourceName).substring(0, 15)) + ']';

                listHtml += '<div class="rec-event-row">' +
                    '<input type="checkbox"' + chk +
                    ' onchange="recordingChecked[' + origIdx + '] = this.checked">' +
                    '<span class="rec-event-label">' + esc(label) + '</span></div>';
            }
            eventsEl.innerHTML = listHtml;
        }
    }
}

function toggleRecordingFilter(cat) {
    recordingFilters[cat] = !recordingFilters[cat];
    renderRecordingPanel();
}

function filterRecordingEvents() {
    if (!recordingData || !recordingData.events) return [];
    return recordingData.events.filter(function(ev) {
        return recordingFilters[ev.category];
    });
}

function hideRecordingPanel() {
    recordingData = null;
    recordingChecked = {};
    var propPanel = document.getElementById('propPanel');
    var recPanel = document.getElementById('recordingPanel');
    if (propPanel) propPanel.style.display = '';
    if (recPanel) recPanel.style.display = 'none';
    renderProps();
    updateFooter();
}

function importRecordingToPhase() {
    if (!recordingData) return;
    var phase = getPhase(currentPhaseIdx);
    if (!phase) { setStatus('请先选择一个阶段', 'error'); return; }

    // 收集勾选事件
    var selected = [];
    for (var idx in recordingChecked) {
        if (recordingChecked[idx]) {
            selected.push(recordingData.events[parseInt(idx)]);
        }
    }
    if (selected.length === 0) {
        setStatus('请先勾选要导入的事件', 'error');
        return;
    }

    // 按时间排序
    selected.sort(function(a, b) { return a.timeMs - b.timeMs; });

    // 转换为 FactEvent
    var imported = 0;
    for (var s = 0; s < selected.length; s++) {
        var ev = selected[s];
        var timeSec = ev.timeMs / 1000;

        var actionId = ev.data.actionId || ev.data.spellId ||
                       ev.data.statusId || ev.data.abilityId || 0;

        var name = '[' + ev.category + '] ';
        if (ev.data.sourceName) name += esc(String(ev.data.sourceName).substring(0, 20)) + ' ';
        if (actionId) name += '#' + actionId;

        var fe = {
            id: 'rec_' + (phase.events.length + imported + 1),
            name: name,
            time: timeSec,
            duration: 0,
            actions: []
        };

        if (actionId) {
            fe.startSync = {
                type: 'ability',
                abilityIds: [parseInt(actionId)]
            };
        }

        // 去重检查: 相同时间的同名事件跳过
        var dup = false;
        for (var ei = 0; ei < phase.events.length; ei++) {
            var existing = phase.events[ei];
            if (Math.abs(existing.time - timeSec) < 0.01 && existing.name === name) {
                dup = true;
                break;
            }
        }
        if (dup) continue;

        phase.events.push(fe);
        imported++;
    }

    phase.events.sort(function(a, b) { return a.time - b.time; });

    setStatus('已导入 ' + imported + ' 个事件, 跳过 ' +
              (selected.length - imported) + ' 个重复', 'success');
    hideRecordingPanel();
    markDirty();
}
```

- [ ] **Step 2: Commit**

```bash
git add HiAuRo/UI/web/fact-editor.js
git commit -m "feat(recording): add EncounterRecord load/filter/import logic to fact-editor"
```

---

### Task 6: fact-editor.css — 录制面板样式

**Files:**
- Modify: `HiAuRo/UI/web/fact-editor.css`

**前置条件:** Task 4 完成。

- [ ] **Step 1: 在 CSS 文件末尾添加录制面板样式**

```css
/* ---- 录制面板 ---- */

#recordingPanel {
    display: flex;
    flex-direction: column;
    max-height: 100%;
}

.recording-toolbar {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 6px 8px;
    border-bottom: 1px solid var(--border);
    flex-shrink: 0;
}

.recording-toolbar h3 {
    font-size: 13px;
    margin: 0;
}

.recording-info {
    padding: 6px 8px;
    border-bottom: 1px solid var(--border);
    flex-shrink: 0;
}

.recording-filters {
    padding: 6px 8px;
    border-bottom: 1px solid var(--border);
    flex-shrink: 0;
    max-height: 120px;
    overflow-y: auto;
}

.recording-events {
    flex: 1;
    overflow-y: auto;
    padding: 4px 0;
}

.rec-event-row {
    display: flex;
    align-items: flex-start;
    padding: 2px 8px;
    cursor: pointer;
    font-size: 10px;
    border-bottom: 1px solid var(--border-light);
    gap: 4px;
}

.rec-event-row:hover {
    background: var(--accent-bg);
}

.rec-event-label {
    word-break: break-all;
    line-height: 1.4;
}

.recording-actions {
    padding: 6px 8px;
    border-top: 1px solid var(--border);
    flex-shrink: 0;
}

.recording-actions .p-btn {
    width: 100%;
}
```

- [ ] **Step 2: Commit**

```bash
git add HiAuRo/UI/web/fact-editor.css
git commit -m "feat(recording): add recording panel styles"
```

---

### Task 7: 最终验证

**Files:** 全部。

- [ ] **Step 1: 构建验证**

```bash
dotnet build HiAuRo/HiAuRo.csproj -nologo
```

期望: 0 错误，0 警告。

- [ ] **Step 2: 检查 Recording 目录生成**

在 WSL 中验证 Recording 文件夹存在:
```bash
ls HiAuRo/Recording/
```

期望输出: `EncounterRecord.cs  EncounterRecorder.cs`

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "chore(recording): final verification - build passes, all files in place"
```
