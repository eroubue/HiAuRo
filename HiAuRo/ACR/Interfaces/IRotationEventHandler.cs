using HiAuRo.ACR;

namespace HiAuRo.ACR;

/// <summary>
/// 战斗事件回调处理接口 —— 对齐 AE 风格，10 个回调
/// 由 AIRunner 在 Slot 执行过程中同步调用
/// </summary>
public interface IRotationEventHandler
{
    /// <summary>非战斗情况下每帧触发（远敏唱歌、T切姿态等）</summary>
    void OnPreCombat() { }

    /// <summary>战斗重置时触发（团灭重来、脱战等）</summary>
    void OnResetBattle() { }

    /// <summary>没目标时触发（舞者转阶段提前跳舞等）</summary>
    void OnNoTarget() { }

    /// <summary>读条判定成功后（读条快结束、可滑步的时间点）</summary>
    void OnSpellCastSuccess(Slot slot, Spell spell) { }

    /// <summary>技能使用前</summary>
    void BeforeSpell(Slot slot, Spell spell) { }

    /// <summary>技能使用后（DoT刷新后记录是否强化等）</summary>
    void AfterSpell(Slot slot, Spell spell) { }

    /// <summary>战斗中每帧触发（最常用的回调）</summary>
    void OnBattleUpdate(int battleTimeMs) { }

    /// <summary>切入当前 ACR 时</summary>
    void OnEnterRotation() { }

    /// <summary>从当前 ACR 退出时</summary>
    void OnExitRotation() { }

    /// <summary>切图时触发</summary>
    void OnTerritoryChanged() { }
}
