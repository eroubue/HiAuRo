using System.Numerics;

namespace HiAuRo.ACR.Shapes;

/// <summary>
/// 场地接口 —— 判断点是否在场内 + 网格采样生成候选点
/// </summary>
public interface IField
{
    /// <summary>点是否在场地内（XZ平面，忽略Y）</summary>
    bool Contains(Vector3 point);
    /// <summary>网格采样生成候选点（spacing=采样间距）</summary>
    List<Vector3> SampleGrid(float spacing);
    /// <summary>获取场地中心点（用于靠边/靠心排序）</summary>
    Vector3 GetCenter();
}
