using HiAuRo.ACR;

namespace HiAuRo.Runtime;

/// <summary>
/// AI 循环模式接口
/// </summary>
public interface IAILoop
{
    /// <summary>返回下一个要执行的 Slot（null=无可用）</summary>
    Slot? GetNextSlot(bool blockBuild = false);
}
