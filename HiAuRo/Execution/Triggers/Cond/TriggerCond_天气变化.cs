using HiAuRo.ACR;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 触发条件参数 —— 天气变化
/// </summary>
public sealed class TriggerCondParams_天气变化 : ITriggerCondParams
{
    /// <summary>天气 ID（Lumina Weather 表的 RowId）</summary>
    public byte WeatherId;
}

/// <summary>
/// 检测当前天气是否与指定 ID 匹配
/// </summary>
[TriggerDisplay("天气变化", "检测天气是否变为指定ID")]
[TriggerTypeName("TriggerCondOnWeatherIdChanged")]
public sealed class TriggerCond_天气变化 : ITriggerCond
{
    private readonly byte _weatherId;

    /// <param name="weatherId">天气 ID</param>
    public TriggerCond_天气变化(byte weatherId)
    {
        _weatherId = weatherId;
    }

    /// <summary>检测当前天气是否匹配</summary>
    public bool Handle(ITriggerCondParams? condParams = null)
    {
        return OmenTools.OmenService.GameState.Weather == _weatherId;
    }
}
