namespace HiAuRo.ACR;

/// <summary>
/// 连击辅助 —— 通过 EventSystem 自动追踪每次技能使用
/// LastComboSpellId = 上一个成功使用的技能 ID
/// </summary>
public static class ComboHelper
{
    /// <summary>上一个成功使用的技能 ID（连击判断核心）</summary>
    public static uint LastComboSpellId
        => Runtime.EventSystem.LastComboSpellId;

    /// <summary>最近一次成功使用的技能 ID</summary>
    public static uint LastSpellId
        => Runtime.EventSystem.LastCompletedActionId;

    /// <summary>上一个技能是指定 ID 吗？</summary>
    public static bool WasLastCombo(uint spellId)
        => LastComboSpellId == spellId;

    /// <summary>连击是否在窗口内（上一个技能在 N 秒内使用过）</summary>
    public static bool ComboInWindow(uint spellId, int windowMs = 15000)
        => SpellHistoryHelper.GetLastSpellTime(spellId) >= 0
        && SpellHistoryHelper.GetLastSpellTime(spellId) <= windowMs;

    /// <summary>连击是否即将过期（< 500ms）</summary>
    public static bool ComboAboutToExpire(uint spellId, int withinMs = 500)
    {
        var elapsed = SpellHistoryHelper.GetLastSpellTime(spellId);
        return elapsed >= 0 && elapsed >= 15000 - withinMs;
    }
}
