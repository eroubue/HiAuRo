using System.Numerics;

namespace HiAuRo.ACR.Shapes;

/// <summary>
/// 安全点位计算结果 —— 近远两组坐标
/// </summary>
public sealed class SafePointResult
{
    public List<Vector3> NearPoints { get; } = new();
    public List<Vector3> FarPoints { get; } = new();
}
