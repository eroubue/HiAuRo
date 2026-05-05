using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.JobGauge.Enums;

namespace HiAuRo.Data;

/// <summary>
/// 诗人 (BRD) 职业快捷入口 —— 常用状态短路径
/// </summary>
public static class BRDHelp
{
    #region 技能 / Buff / DoT ID

    private const uint StraightShotReady = 122;
    private const uint StormbiteDot       = 1201;
    private const uint CausticBiteDot     = 1200;
    private const uint RagingStrikes      = 125;
    private const uint BattleVoice        = 141;
    private const uint RadiantFinale      = 2722;
    private const uint Barrage            = 128;
    private const uint HawkEye            = 3122;

    #endregion

    /// <summary>诗人职业量谱</summary>
    public static BRDGauge? Gauge =>
        DService.Instance().JobGauges.Get<BRDGauge>();

    /// <summary>当前歌曲类型</summary>
    public static Song CurrentSong => Gauge?.Song ?? Song.None;

    /// <summary>歌曲剩余时间 (ms)</summary>
    public static ushort SongTimer => Gauge?.SongTimer ?? 0;

    /// <summary>乐章层数</summary>
    public static byte Repertoire => Gauge?.Repertoire ?? 0;

    /// <summary>灵魂之声值 (0-100)</summary>
    public static byte SoulVoice => Gauge?.SoulVoice ?? 0;

    /// <summary>是否在歌曲中</summary>
    public static bool InSong => CurrentSong != Song.None;

    /// <summary>直线射击预备是否激活</summary>
    public static bool HasStraightShotReady =>
        Me.HasStatus(StraightShotReady, out _);

    /// <summary>猛者强击是否激活</summary>
    public static bool HasRagingStrikes =>
        Me.HasStatus(RagingStrikes, out _);

    /// <summary>战斗之声是否激活</summary>
    public static bool HasBattleVoice =>
        Me.HasStatus(BattleVoice, out _);

    /// <summary>光明神的最终乐章是否激活</summary>
    public static bool HasRadiantFinale =>
        Me.HasStatus(RadiantFinale, out _);

    /// <summary>纷乱箭是否激活</summary>
    public static bool HasBarrage =>
        Me.HasStatus(Barrage, out _);

    /// <summary>鹰眼是否激活</summary>
    public static bool HasHawkEye =>
        Me.HasStatus(HawkEye, out _);

    /// <summary>目标上风蚀 DoT 是否激活</summary>
    public static bool HasStormbiteOnTarget =>
        HasDotOnTarget(StormbiteDot);

    /// <summary>目标上毒咬 DoT 是否激活</summary>
    public static bool HasCausticBiteOnTarget =>
        HasDotOnTarget(CausticBiteDot);

    private static bool HasDotOnTarget(uint statusId)
    {
        var target = Target.Current;
        return target is IBattleChara bc
            && bc.StatusList.Any(s => s.StatusID == statusId
                                   && s.SourceID == Me.Object?.EntityID
                                   && s.RemainingTime > 0);
    }
}
