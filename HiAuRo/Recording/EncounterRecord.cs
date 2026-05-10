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
