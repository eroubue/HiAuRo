using HiAuRo.ACR;
using HiAuRo.Data;
using HiAuRo.Infrastructure;
using Dalamud.Hooking;
using OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds;
using OmenTools.OmenService;
using IFramework = Dalamud.Plugin.Services.IFramework;

namespace HiAuRo.Execution.Events;

public sealed class GameEventHook
{
    public static GameEventHook Instance { get; } = new();
    public event Action<ITriggerCondParams>? OnEventFired;

    private void Fire(ITriggerCondParams p)
    {
        LogManager.Instance.Log(p);
        OnEventFired?.Invoke(p);
    }

    private bool _initialized;

    private delegate void ActionEffectDelegate(
        uint sourceId, nint sourceCharacter, nint pos,
        nint effectHeader, nint effectArray, nint effectTail);
    private Hook<ActionEffectDelegate>? _actionEffectHook;

    public void Init()
    {
        if (_initialized) return;
        _initialized = true;

        var csm = CharacterStatusManager.Instance();
        csm.RegGain(OnGainStatus);
        csm.RegLose(OnLoseStatus);

        var gpm = GamePacketManager.Instance();
        gpm.RegPostReceivePacket(OnReceivePacket);

        var cm = ChatManager.Instance();
        cm.RegPostProcessChatBoxEntry((msg, save) => OnChatMessage(msg.ToString(), save));

        FrameworkManager.Instance().Reg(OnFrameworkUpdate);

        try
        {
            var sigScanner = DService.Instance().SigScanner;
            var actionEffectAddr = sigScanner.ScanText("40 55 53 56 57 41 54 41 55 41 56 41 57 48 8D AC 24 ?? ?? ?? ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 70 4C 8B BD");
            if (actionEffectAddr != nint.Zero)
            {
                _actionEffectHook = DService.Instance().Hook.HookFromAddress<ActionEffectDelegate>(
                    actionEffectAddr, OnActionEffect);
                _actionEffectHook.Enable();
            }
            else
            {
                DService.Instance().Log.Warning("[GameEventHook] ActionEffect 签名未找到，跳过 Hook 挂载");
            }
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[GameEventHook] ActionEffect Hook 挂载失败 (可能是版本更新): {ex.Message}");
        }
    }

    public void Shutdown()
    {
        if (!_initialized) return;
        _initialized = false;

        FrameworkManager.Instance().Unreg(OnFrameworkUpdate);

        _actionEffectHook?.Disable();
        _actionEffectHook?.Dispose();
        _actionEffectHook = null;

        var csm = CharacterStatusManager.Instance();
        csm.Unreg(OnGainStatus);
        csm.Unreg(OnLoseStatus);

        var gpm = GamePacketManager.Instance();
        gpm.Unreg(OnReceivePacket);

        // ChatManager 在 plugin dispose 时由 OmenTools 自行清理
    }

    private void OnGainStatus(IBattleChara player, ushort id, ushort param, ushort stackCount, TimeSpan remainingTime, ulong sourceID)
    {
        Fire(new BuffGainParams
        {
            SourceID = player.EntityID,
            StatusID = id,
            StackCount = stackCount
        });
    }

    private void OnLoseStatus(IBattleChara player, ushort id, ushort param, ushort stackCount, ulong sourceID)
    {
        Fire(new BuffRemoveParams
        {
            SourceID = player.EntityID,
            StatusID = id
        });
    }

    private void OnReceivePacket(int opcode, nint packet)
    {
        if (packet == nint.Zero) return;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        switch (opcode)
        {
            case 0x01F8:
            {
                unsafe
                {
                    var tether = System.Runtime.InteropServices.Marshal.PtrToStructure<TetherRaw>(packet);
                    if (tether.TargetOID == 0xE0000000)
                    {
                        BattleData.RecentTethers.RemoveAll(e => e.TetherId == tether.TetherID && e.SourceId == tether.SourceID);
                        Fire(new TetherRemoveParams
                        {
                            SourceID = tether.SourceID,
                            Param2 = tether.A2, Param3 = tether.A3, Param5 = tether.A5
                        });
                    }
                    else
                    {
                        BattleData.OnTetherAdded(tether.TetherID, tether.SourceID, (uint)tether.TargetOID, now);
                        Fire(new TetherCreateParams
                        {
                            TetherID = tether.TetherID,
                            SourceID = tether.SourceID,
                            TargetOID = (uint)tether.TargetOID,
                            Param2 = tether.A2, Param3 = tether.A3, Param5 = tether.A5
                        });
                    }
                }
                break;
            }
            case 0x02B6:
            {
                unsafe
                {
                    var me = System.Runtime.InteropServices.Marshal.PtrToStructure<MapEffectRaw>(packet);
                    BattleData.OnMapEffect(me.PositionIndex, new System.Numerics.Vector3(), now);
                    Fire(new MapEffectParams
                    {
                        PositionIndex = me.PositionIndex,
                        Param1 = me.Param1,
                        Param2 = me.Param2
                    });
                }
                break;
            }
            case 0x0228:
            {
                unsafe
                {
                    var ac = System.Runtime.InteropServices.Marshal.PtrToStructure<ActorControlRaw>(packet);
                    OnActorControl(ac);
                }
                break;
            }
            case 0x039A:
            {
                unsafe
                {
                    var cast = System.Runtime.InteropServices.Marshal.PtrToStructure<ActorCastRaw>(packet);
                    Fire(new ActorCastParams
                    {
                        ActionID = cast.ActionID,
                        CastTime = cast.CastTime,
                        TargetID = cast.TargetID,
                        SourceID = cast.SourceID,
                        PosX = cast.PosX,
                        PosY = cast.PosY,
                        PosZ = cast.PosZ
                    });
                }
                break;
            }
            case 0x01C2:
            {
                unsafe
                {
                    var yell = System.Runtime.InteropServices.Marshal.PtrToStructure<NpcYellRaw>(packet);
                    var name = System.Runtime.InteropServices.Marshal.PtrToStringUTF8((nint)yell.SourceNamePtr, 32) ?? "";
                    var msg = System.Runtime.InteropServices.Marshal.PtrToStringUTF8((nint)yell.YellMsgPtr, 128) ?? "";
                    Fire(new NpcYellParams
                    {
                        SourceID = yell.SourceID,
                        SourceName = name,
                        YellID = yell.YellID,
                        YellMsg = msg
                    });
                }
                break;
            }
            case 0x01F4:
            {
                unsafe
                {
                    var env = System.Runtime.InteropServices.Marshal.PtrToStructure<EnvControlRaw>(packet);
                    Fire(new EnvControlParams
                    {
                        Index = env.Index,
                        Flag = env.Flag
                    });
                }
                break;
            }
        }
    }

    private void OnChatMessage(string text, bool saveToHistory)
    {
        Fire(new ChatMessageParams { Message = text });
    }

    private void OnActorControl(ActorControlRaw ac)
    {
        var raw = new ActorControlParams
        {
            SourceID = ac.SourceID,
            Command = ac.Command,
            P1 = ac.P1, P2 = ac.P2, P3 = ac.P3,
            P4 = ac.P4, P5 = ac.P5, P6 = ac.P6,
            TargetID = ac.TargetID
        };
        Fire(raw);

        switch (ac.Command)
        {
            case 2 when ac.P3 == 2:
                Fire(new ActorControlDeathParams
                {
                    SourceID = ac.SourceID,
                    TargetID = ac.TargetID
                });
                break;
            case 34:
                Fire(new ActorControlTargetIconParams
                {
                    SourceID = ac.SourceID,
                    TargetID = ac.TargetID,
                    IconID = ac.P1
                });
                break;
            case 54:
                Fire(new ActorControlTargetableParams
                {
                    SourceID = ac.SourceID,
                    TargetID = ac.TargetID,
                    IsTargetable = ac.P3 != 0
                });
                break;
            case 109:
                // CombatStateParams already dispatched by CombatContext (authoritative source)
                break;
            case 407:
                Fire(new ActorControlTimelineParams
                {
                    SourceID = ac.SourceID,
                    TimelineID = ac.P2
                });
                break;
        }
    }

    #region 轮询事件 (Unit创建/消失, 天气变化)

    private readonly Dictionary<uint, (uint DataId, string Name)> _knownEntities = [];
    private uint _lastWeatherId;
    private long _nextPollAt;

    private void OnFrameworkUpdate(IFramework _)
    {
        var now = Environment.TickCount64;
        if (now < _nextPollAt) return;
        _nextPollAt = now + 500;

        var objectTable = DService.Instance().ObjectTable;
        if (objectTable == null) return;

        var currentIds = new HashSet<uint>();
        foreach (var obj in objectTable)
        {
            if (obj == null || obj.EntityID == 0) continue;
            currentIds.Add(obj.EntityID);

            if (_knownEntities.Count > 0 && !_knownEntities.ContainsKey(obj.EntityID))
            {
                var bn = obj as IBattleNPC;
                Fire(new UnitCreateParams
                {
                    EntityId = obj.EntityID,
                    DataId = bn?.DataID ?? 0,
                    Name = obj.Name.ToString()
                });
            }
        }

        if (_knownEntities.Count > 0)
        {
            foreach (var (id, info) in _knownEntities)
            {
                if (!currentIds.Contains(id))
                {
                    Fire(new UnitDeleteParams
                    {
                        EntityId = id,
                        DataId = info.DataId,
                        Name = info.Name
                    });
                }
            }
        }

        var oldSnapshot = new Dictionary<uint, (uint, string)>(_knownEntities);
        _knownEntities.Clear();
        foreach (var id in currentIds)
        {
            if (oldSnapshot.TryGetValue(id, out var cached))
                _knownEntities[id] = cached;
            else
                _knownEntities[id] = (((objectTable.SearchByID(id) as IBattleNPC)?.DataID ?? 0,
                    objectTable.SearchByID(id)?.Name.ToString() ?? ""));
        }

        var currentWeather = OmenTools.OmenService.GameState.Weather;
        if (_lastWeatherId != 0 && currentWeather != _lastWeatherId)
        {
            Fire(new WeatherChangedParams
            {
                NewWeatherId = (int)currentWeather
            });
        }
        _lastWeatherId = currentWeather;
    }

    #endregion

    #region ActionEffect Hook

    private void OnActionEffect(
        uint sourceId, nint sourceCharacter, nint pos,
        nint effectHeader, nint effectArray, nint effectTail)
    {
        _actionEffectHook!.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTail);

        if (effectHeader == nint.Zero || effectArray == nint.Zero) return;

        unsafe
        {
            var header = (EffectHeader*)effectHeader;
            var actionId = header->ActionID;
            var animationId = header->AnimationId;
            var targetCount = header->TargetCount;
            if (targetCount == 0) return;

            var entries = (EffectEntry*)effectArray;
            for (byte i = 0; i < targetCount; i++)
            {
                var entry = entries[i * 8];
                if (entry.Type == 0) continue; // Nothing/NoEffect

                var targetOid = i == 0 ? header->AnimationTargetId 
                    : (effectTail != nint.Zero ? *(ulong*)(effectTail + i * 8) : 0xE0000000);

                Fire(new ActionEffectParams
                {
                    ActionID = actionId,
                    SourceID = sourceId,
                    TargetOID = (uint)(targetOid & 0xFFFFFFFF),
                    AnimationID = animationId,
                    EffectType = entry.Type
                });

                if (targetOid == 0xE0000000)
                {
                    var p = pos != nint.Zero ? (Vector3Raw*)pos : null;
                    Fire(new NoTargetAbilityEffectParams
                    {
                        SourceID = sourceId,
                        ActionID = actionId,
                        PosX = p != null ? p->X : 0,
                        PosY = p != null ? p->Y : 0,
                        PosZ = p != null ? p->Z : 0
                    });
                }
            }
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
    private struct EffectHeader
    {
        [System.Runtime.InteropServices.FieldOffset(0)]  public ulong AnimationTargetId;
        [System.Runtime.InteropServices.FieldOffset(8)]  public uint ActionID;
        [System.Runtime.InteropServices.FieldOffset(12)] public uint GlobalEffectCounter;
        [System.Runtime.InteropServices.FieldOffset(16)] public float AnimationLockTime;
        [System.Runtime.InteropServices.FieldOffset(20)] public uint SomeTargetID;
        [System.Runtime.InteropServices.FieldOffset(24)] public ushort SourceSequence;
        [System.Runtime.InteropServices.FieldOffset(26)] public ushort Rotation;
        [System.Runtime.InteropServices.FieldOffset(28)] public ushort AnimationId;
        [System.Runtime.InteropServices.FieldOffset(30)] public byte Variation;
        [System.Runtime.InteropServices.FieldOffset(31)] public byte ActionType;
        [System.Runtime.InteropServices.FieldOffset(32)] public byte TargetCount;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Size = 8)]
    private struct EffectEntry
    {
        public byte Type;
        public byte Param1;
        public byte Param2;
        public byte Param3;
        public uint Value;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Vector3Raw
    {
        public float X;
        public float Y;
        public float Z;
    }

    #endregion

    #region raw packet structs

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    private struct TetherRaw
    {
        public uint SourceID;
        public byte A2;
        public ushort TetherID;
        public byte A5;
        public ulong TargetOID;
        public byte A3;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    private struct MapEffectRaw
    {
        public ushort PositionIndex;
        public ushort Param1;
        public ushort Param2;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    private struct ActorControlRaw
    {
        public ushort Command;
        public uint P1;
        public uint P2;
        public uint P3;
        public uint P4;
        public uint P5;
        public uint P6;
        public uint SourceID;
        public uint TargetID;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    private struct ActorCastRaw
    {
        public ushort ActionID;
        public byte SkillType;
        public uint Unk;
        public float CastTime;
        public uint TargetID;
        public float Rotation;
        public float PosX;
        public float PosY;
        public float PosZ;
        public uint SourceID;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    private unsafe struct NpcYellRaw
    {
        public uint SourceID;
        public byte* SourceNamePtr;
        public ushort YellID;
        public byte* YellMsgPtr;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    private struct EnvControlRaw
    {
        public uint Index;
        public uint Flag;
    }

    #endregion
}

public sealed class ChatMessageParams : ITriggerCondParams
{
    public string Message = "";
}
