using System.Numerics;

namespace HiAuRo.ACR.Shapes;

/// <summary>
/// AOE 区域接口 —— 判断点是否在区域内
/// </summary>
public interface IAoeZone
{
    /// <summary>点是否在AOE区域内（XZ平面，忽略Y）</summary>
    bool Contains(Vector3 point);
}
