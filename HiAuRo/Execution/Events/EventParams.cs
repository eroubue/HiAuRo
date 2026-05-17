using HiAuRo.ACR;

namespace HiAuRo.Execution.Events;

/// <summary>技能效果参数 — ActionEffect</summary>
public sealed class ActionEffectParams : ITriggerCondParams
{
    /// <summary>技能 ID</summary>
    public uint ActionID;
    /// <summary>来源 ID</summary>
    public uint SourceID;
    /// <summary>目标 OID</summary>
    public uint TargetOID;
    /// <summary>动画 ID</summary>
    public ushort AnimationID;
    /// <summary>效果类型</summary>
    public byte EffectType;
}

/// <summary>连线创建参数 — TetherCreate</summary>
public sealed class TetherCreateParams : ITriggerCondParams
{
    /// <summary>连线 ID</summary>
    public uint TetherID;
    /// <summary>来源 ID</summary>
    public uint SourceID;
    /// <summary>目标 OID</summary>
    public uint TargetOID;
    /// <summary>参数2</summary>
    public byte Param2;
    /// <summary>参数3</summary>
    public byte Param3;
    /// <summary>参数5</summary>
    public byte Param5;
}

/// <summary>连线移除参数 — TetherRemove</summary>
public sealed class TetherRemoveParams : ITriggerCondParams
{
    /// <summary>来源 ID</summary>
    public uint SourceID;
    /// <summary>参数2</summary>
    public byte Param2;
    /// <summary>参数3</summary>
    public byte Param3;
    /// <summary>参数5</summary>
    public byte Param5;
}

/// <summary>地图特效参数 — MapEffect</summary>
public sealed class MapEffectParams : ITriggerCondParams
{
    /// <summary>位置索引</summary>
    public ushort PositionIndex;
    /// <summary>参数1</summary>
    public ushort Param1;
    /// <summary>参数2</summary>
    public ushort Param2;
}

/// <summary>Director 更新参数 — DirectorUpdate</summary>
public sealed class DirectorUpdateParams : ITriggerCondParams
{
    /// <summary>更新类别</summary>
    public Structures.DirectorUpdateCategory Category;
    /// <summary>参数1</summary>
    public uint Param1;
    /// <summary>参数2</summary>
    public uint Param2;
    /// <summary>参数3</summary>
    public uint Param3;
    /// <summary>参数4</summary>
    public uint Param4;
    /// <summary>参数6</summary>
    public uint A6;
    /// <summary>参数7</summary>
    public uint A7;
    /// <summary>参数8</summary>
    public uint A8;
    /// <summary>参数9</summary>
    public uint A9;
}

/// <summary>ActorControl 通用参数</summary>
public sealed class ActorControlParams : ITriggerCondParams
{
    /// <summary>来源 ID</summary>
    public uint SourceID;
    /// <summary>命令类型</summary>
    public ushort Command;
    /// <summary>参数1</summary>
    public uint P1;
    /// <summary>参数2</summary>
    public uint P2;
    /// <summary>参数3</summary>
    public uint P3;
    /// <summary>参数4</summary>
    public uint P4;
    /// <summary>参数5</summary>
    public uint P5;
    /// <summary>参数6</summary>
    public uint P6;
    /// <summary>目标 ID</summary>
    public uint TargetID;
}

/// <summary>ActorControl command=2 — Actor 死亡</summary>
public sealed class ActorControlDeathParams : ITriggerCondParams
{
    /// <summary>来源 ID</summary>
    public uint SourceID;
    /// <summary>目标 ID</summary>
    public uint TargetID;
}

/// <summary>ActorControl command=34 — 技能点名图标 TargetIcon (HeadMarker)</summary>
public sealed class ActorControlTargetIconParams : ITriggerCondParams
{
    /// <summary>来源 ID</summary>
    public uint SourceID;
    /// <summary>目标 ID</summary>
    public uint TargetID;
    /// <summary>图标 ID</summary>
    public uint IconID;
}

/// <summary>ActorControl command=54 — 单位可选中状态变化</summary>
public sealed class ActorControlTargetableParams : ITriggerCondParams
{
    /// <summary>来源 ID</summary>
    public uint SourceID;
    /// <summary>目标 ID</summary>
    public uint TargetID;
    /// <summary>是否可选中</summary>
    public bool IsTargetable;
}

/// <summary>ActorControl command=109 — 战斗状态 (p4 sub-command: 0x40000001=进战, 0x40000003=脱战)</summary>
public sealed class ActorControlCombatParams : ITriggerCondParams
{
    /// <summary>是否进入战斗</summary>
    public bool IsEntering;
}

/// <summary>ActorControl command=407 — 播放动作时间轴</summary>
public sealed class ActorControlTimelineParams : ITriggerCondParams
{
    /// <summary>来源 ID</summary>
    public uint SourceID;
    /// <summary>时间轴 ID</summary>
    public uint TimelineID;
}

/// <summary>读条参数 — ActorCast</summary>
public sealed class ActorCastParams : ITriggerCondParams
{
    /// <summary>技能 ID</summary>
    public ushort ActionID;
    /// <summary>读条时间</summary>
    public float CastTime;
    /// <summary>目标 ID</summary>
    public uint TargetID;
    /// <summary>来源 ID</summary>
    public uint SourceID;
    /// <summary>X 坐标</summary>
    public float PosX;
    /// <summary>Y 坐标</summary>
    public float PosY;
    /// <summary>Z 坐标</summary>
    public float PosZ;
}

/// <summary>NPC 喊话参数 — NpcYell</summary>
public sealed class NpcYellParams : ITriggerCondParams
{
    /// <summary>来源 ID</summary>
    public uint SourceID;
    /// <summary>来源名称</summary>
    public string SourceName = string.Empty;
    /// <summary>喊话 ID</summary>
    public ushort YellID;
    /// <summary>喊话内容</summary>
    public string YellMsg = string.Empty;
}

/// <summary>环境控制参数 — EnvControl</summary>
public sealed class EnvControlParams : ITriggerCondParams
{
    /// <summary>索引</summary>
    public uint Index;
    /// <summary>标志</summary>
    public uint Flag;
}

/// <summary>无目标技能效果参数</summary>
public sealed class NoTargetAbilityEffectParams : ITriggerCondParams
{
    /// <summary>来源 ID</summary>
    public uint SourceID;
    /// <summary>技能 ID</summary>
    public uint ActionID;
    /// <summary>X 坐标</summary>
    public float PosX;
    /// <summary>Y 坐标</summary>
    public float PosY;
    /// <summary>Z 坐标</summary>
    public float PosZ;
}

/// <summary>单位创建参数 — UnitCreate</summary>
public sealed class UnitCreateParams : ITriggerCondParams
{
    /// <summary>实体 ID</summary>
    public uint EntityId;
    /// <summary>数据 ID</summary>
    public uint DataId;
    /// <summary>名称</summary>
    public string Name = string.Empty;
}

/// <summary>单位删除参数 — UnitDelete</summary>
public sealed class UnitDeleteParams : ITriggerCondParams
{
    /// <summary>实体 ID</summary>
    public uint EntityId;
    /// <summary>数据 ID</summary>
    public uint DataId;
    /// <summary>名称</summary>
    public string Name = string.Empty;
}

/// <summary>天气变化参数 — WeatherChanged</summary>
public sealed class WeatherChangedParams : ITriggerCondParams
{
    /// <summary>新天气 ID</summary>
    public int NewWeatherId;
}

/// <summary>Buff 获得参数 — BuffGain</summary>
public sealed class BuffGainParams : ITriggerCondParams
{
    /// <summary>来源 ID</summary>
    public uint SourceID;
    /// <summary>状态 ID</summary>
    public ushort StatusID;
    /// <summary>层数</summary>
    public ushort StackCount;
}

/// <summary>Buff 移除参数 — BuffRemove</summary>
public sealed class BuffRemoveParams : ITriggerCondParams
{
    /// <summary>来源 ID</summary>
    public uint SourceID;
    /// <summary>状态 ID</summary>
    public ushort StatusID;
}

/// <summary>技能后参数 — AfterSpell</summary>
public sealed class AfterSpellParams : ITriggerCondParams
{
    /// <summary>技能 ID</summary>
    public uint SpellID;
}

/// <summary>战斗状态参数 — CombatState</summary>
public sealed class CombatStateParams : ITriggerCondParams
{
    /// <summary>是否进入战斗</summary>
    public bool IsEntering;
}

/// <summary>对象变化 — ActorControl 未分类命令</summary>
public sealed class ObjectChangeParams : ITriggerCondParams
{
    /// <summary>来源 ID</summary>
    public uint SourceID;
    /// <summary>目标 ID</summary>
    public uint TargetID;
    /// <summary>ActorControl Command</summary>
    public ushort Command;
    /// <summary>参数1</summary>
    public uint P1;
    /// <summary>参数2</summary>
    public uint P2;
    /// <summary>参数3</summary>
    public uint P3;
    /// <summary>参数4</summary>
    public uint P4;
}

/// <summary>对象特效 — ProcessObjectEffect 函数钩子</summary>
public sealed class ObjectEffectParams : ITriggerCondParams
{
    /// <summary>来源游戏对象 ID</summary>
    public uint ObjectID;
    /// <summary>特效数据1</summary>
    public ushort Data1;
    /// <summary>特效数据2</summary>
    public ushort Data2;
}
