using HiAuRo.ACR;

namespace HiAuRo.Runtime;

/// <summary>
/// 倒计时管理器 —— 管理副本倒计时阶段的预注册行为
/// </summary>
public sealed class CountDownHandler
{
    /// <summary>已注册的倒计时行为（按 timeLeft 降序排列确保先到时的后触发）</summary>
    private readonly List<(int TimeLeft, uint SpellId, SpellTargetType TargetType)> _actions = [];

    /// <summary>是否已触发过（防止同一行为重复执行）</summary>
    private readonly HashSet<(int, uint)> _fired = [];

    /// <summary>注册倒计时阶段行为</summary>
    public void AddAction(int timeLeft, uint spellId, SpellTargetType targetType)
    {
        _actions.Add((timeLeft, spellId, targetType));
        // 按 timeLeft 降序排序（大的先注册但不影响触发逻辑）
    }

    /// <summary>
    /// 每帧推进 —— 检查倒计时剩余时间
    /// </summary>
    /// <param name="countdownSec">当前倒计时剩余秒数（从游戏 IPC 获取）</param>
    public void Update(float countdownSec)
    {
        if (countdownSec <= 0) return;

        foreach (var (timeLeft, spellId, _) in _actions)
        {
            // 倒计时刚好到达目标秒数时触发
            if (Math.Abs(countdownSec - timeLeft) <= 0.5f && _fired.Add((timeLeft, spellId)))
            {
                OmenTools.OmenService.UseActionManager.Instance().UseAction(
                    FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action,
                    spellId, 0, 0, 0, 0);
            }
        }
    }

    /// <summary>是否还有未执行的行为</summary>
    public bool HasPending => _actions.Count > 0;

    /// <summary>重置倒计时</summary>
    public void Reset()
    {
        _actions.Clear();
        _fired.Clear();
    }
}
