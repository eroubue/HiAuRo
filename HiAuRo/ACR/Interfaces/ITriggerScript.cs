namespace HiAuRo.ACR;

/// <summary>
/// 触发脚本接口 — 对齐 AE ITriggerScript
/// </summary>
public interface ITriggerScript
{
    /// <summary>检查/执行脚本。OnlyCheck=true 时仅做条件判断。</summary>
    bool Check(ITriggerCondParams? condParams);
}
