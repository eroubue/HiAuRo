namespace HiAuRo.ACR;

/// <summary>
/// 触发条件接口 —— 对齐 AE 签名: bool Handle(ITriggerCondParams?)
/// </summary>
public interface ITriggerCond : ITriggerBase
{
    /// <summary>检查触发条件是否满足</summary>
    bool Handle(ITriggerCondParams? condParams = null);
}
