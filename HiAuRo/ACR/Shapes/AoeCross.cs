using System.Numerics;

namespace HiAuRo.ACR.Shapes;

/// <summary>
/// 十字 AOE —— 两条垂直矩形臂，绕中心旋转
/// </summary>
public sealed class AoeCross : IAoeZone
{
    readonly AoeRect _armX;
    readonly AoeRect _armZ;

    public AoeCross(Vector3 center, float totalLenX, float totalLenZ, float armWidth, float rotationDeg)
    {
        // 水平臂：宽=totalLenX，高=armWidth
        _armX = new AoeRect(center, totalLenX, armWidth, rotationDeg);
        // 垂直臂：宽=armWidth，高=totalLenZ
        _armZ = new AoeRect(center, armWidth, totalLenZ, rotationDeg);
    }

    public bool Contains(Vector3 point)
        => _armX.Contains(point) || _armZ.Contains(point);
}
