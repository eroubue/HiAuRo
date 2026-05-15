namespace HiAuRo.ACR.Shapes;

/// <summary>
/// 每次计算的链式构建器 —— 持有 AOE 列表，终端方法 Calculate 触发实际计算
/// </summary>
public sealed class CalculationBuilder
{
    readonly SafePointCalculator _calculator;
    readonly List<IAoeZone> _aoes = new();
    bool _used;

    internal CalculationBuilder(SafePointCalculator calculator) { _calculator = calculator; }

    /// <summary>添加一个 AOE 区域</summary>
    public CalculationBuilder WithAoe(IAoeZone aoe) { _aoes.Add(aoe); return this; }

    /// <summary>执行计算并返回结果（仅可调用一次）</summary>
    public SafePointResult Calculate(SafePointConfig config)
    {
        if (_used) throw new InvalidOperationException("CalculationBuilder 已使用，请通过 Begin() 获取新实例");
        _used = true;
        return _calculator.Calculate(_aoes, config);
    }
}
