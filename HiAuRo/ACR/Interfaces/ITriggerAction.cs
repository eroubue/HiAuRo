namespace HiAuRo.ACR;

/// <summary>
/// 触发动作接口 —— 对齐 AE 签名: bool Handle()
/// </summary>
public interface ITriggerAction : ITriggerBase
{
    /// <summary>执行触发动作，返回 true 表示已处理</summary>
    bool Handle();
}
