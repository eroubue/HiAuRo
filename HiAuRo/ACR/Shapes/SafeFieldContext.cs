namespace HiAuRo.ACR.Shapes;

/// <summary>
/// 全局副本级共享入口 —— 跨命名空间访问同一个场地计算器
/// </summary>
public static class SafeFieldContext
{
    /// <summary>当前副本的计算器实例（轴脚本初始化时设置一次）</summary>
    public static SafePointCalculator? Current { get; set; }
}
