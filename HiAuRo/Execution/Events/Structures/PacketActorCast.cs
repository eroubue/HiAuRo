using System.Runtime.InteropServices;

namespace HiAuRo.Execution.Events.Structures;

[StructLayout(LayoutKind.Explicit, Size = 0x30)]
public struct PacketActorCast
{
    [FieldOffset(0x00)] public ushort ActionID;
    [FieldOffset(0x04)] public float CastTime;
    [FieldOffset(0x08)] public uint TargetID;
    [FieldOffset(0x0E)] public float Rotation;
    [FieldOffset(0x1C)] public uint SourceID;
    [FieldOffset(0x28)] public ushort SourceSequence;
    [FieldOffset(0x2A)] public ushort Unk;
}
