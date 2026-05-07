using HiAuRo.ACR;

namespace HiAuRo.Execution.Events;

public sealed class ActionEffectParams : ITriggerCondParams
{
    public uint ActionID;
    public uint SourceID;
    public uint TargetOID;
    public ushort AnimationID;
    public byte EffectType;
}

public sealed class TetherCreateParams : ITriggerCondParams
{
    public uint TetherID;
    public uint SourceID;
    public uint TargetOID;
    public byte Param2;
    public byte Param3;
    public byte Param5;
}

public sealed class TetherRemoveParams : ITriggerCondParams
{
    public uint SourceID;
    public byte Param2;
    public byte Param3;
    public byte Param5;
}

public sealed class MapEffectParams : ITriggerCondParams
{
    public ushort PositionIndex;
    public ushort Param1;
    public ushort Param2;
}

public sealed class DirectorUpdateParams : ITriggerCondParams
{
    public Structures.DirectorUpdateCategory Category;
    public uint Param1;
    public uint Param2;
    public uint Param3;
    public uint Param4;
    public uint A6;
    public uint A7;
    public uint A8;
    public uint A9;
}

public sealed class ActorControlParams : ITriggerCondParams
{
    public uint SourceID;
    public ushort Command;
    public uint P1;
    public uint P2;
    public uint P3;
    public uint P4;
    public uint P5;
    public uint P6;
    public uint TargetID;
}

/// <summary>ActorControl command=2 — Actor 死亡</summary>
public sealed class ActorControlDeathParams : ITriggerCondParams
{
    public uint SourceID;
    public uint TargetID;
}

/// <summary>ActorControl command=34 — 技能点名图标 TargetIcon (HeadMarker)</summary>
public sealed class ActorControlTargetIconParams : ITriggerCondParams
{
    public uint SourceID;
    public uint TargetID;
    public uint IconID;
}

/// <summary>ActorControl command=54 — 单位可选中状态变化</summary>
public sealed class ActorControlTargetableParams : ITriggerCondParams
{
    public uint SourceID;
    public uint TargetID;
    public bool IsTargetable;
}

/// <summary>ActorControl command=109 — 战斗状态 (p4 sub-command: 0x40000001=进战, 0x40000003=脱战)</summary>
public sealed class ActorControlCombatParams : ITriggerCondParams
{
    public bool IsEntering;
}

/// <summary>ActorControl command=407 — 播放动作时间轴</summary>
public sealed class ActorControlTimelineParams : ITriggerCondParams
{
    public uint SourceID;
    public uint TimelineID;
}

public sealed class ActorCastParams : ITriggerCondParams
{
    public ushort ActionID;
    public float CastTime;
    public uint TargetID;
    public uint SourceID;
    public float PosX;
    public float PosY;
    public float PosZ;
}

public sealed class NpcYellParams : ITriggerCondParams
{
    public uint SourceID;
    public string SourceName;
    public ushort YellID;
    public string YellMsg;
}

public sealed class EnvControlParams : ITriggerCondParams
{
    public uint Index;
    public uint Flag;
}

public sealed class NoTargetAbilityEffectParams : ITriggerCondParams
{
    public uint SourceID;
    public uint ActionID;
    public float PosX;
    public float PosY;
    public float PosZ;
}

public sealed class UnitCreateParams : ITriggerCondParams
{
    public uint EntityId;
    public uint DataId;
    public string Name;
}

public sealed class UnitDeleteParams : ITriggerCondParams
{
    public uint EntityId;
    public uint DataId;
    public string Name;
}

public sealed class WeatherChangedParams : ITriggerCondParams
{
    public int NewWeatherId;
}

public sealed class BuffGainParams : ITriggerCondParams
{
    public uint SourceID;
    public ushort StatusID;
    public ushort StackCount;
}

public sealed class BuffRemoveParams : ITriggerCondParams
{
    public uint SourceID;
    public ushort StatusID;
}

public sealed class AfterSpellParams : ITriggerCondParams
{
    public uint SpellID;
}

public sealed class CombatStateParams : ITriggerCondParams
{
    public bool IsEntering;
}
