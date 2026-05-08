using HiAuRo.ACR;
using HiAuRo.Data;

namespace HiAuRo.Decision;

/// <summary>
/// 决策引擎 — 根据队伍组成 + 需求，分配减伤/治疗
/// 对齐 AE TriggerCond/Action 的决策辅助逻辑
/// </summary>
public sealed class DecisionEngine
{
    public static DecisionEngine Instance { get; } = new();

    private DecisionOutput _output = new();
    private bool _initialized;

    private DecisionEngine() { }

    /// <summary>初始化内置技能数据</summary>
    public void Init()
    {
        if (_initialized) return;
        _initialized = true;
        LoadBuiltinSkills();
    }

    /// <summary>
    /// 根据事实轴需求 + 当前队伍状态，计算本帧决策
    /// </summary>
    /// <param name="需求减伤">需求值，0=无</param>
    /// <param name="需求治疗">需求值，0=无</param>
    public DecisionOutput 计算(int 需求减伤, int 需求治疗)
    {
        _output = new DecisionOutput();

        if (需求减伤 == 0 && 需求治疗 == 0) return _output;

        var 队伍 = GetAvailableRoles();

        if (需求减伤 > 0)
            分配减伤(需求减伤, 队伍);

        if (需求治疗 > 0)
            分配治疗(需求治疗, 队伍);

        return _output;
    }

    #region 减伤分配

    private void 分配减伤(int 需求, List<(Jobs, bool)> 队伍)
    {
        var 候选 = new List<团队减伤>();

        foreach (var (job, isInParty) in 队伍)
        {
            if (!isInParty) continue;
            if (!DecisionSkillRegistry.团队减伤表.TryGetValue(job, out var skills)) continue;

            foreach (var skill in skills)
            {
                if (GetCooldownRemaining(skill.技能ID) > 0) continue;
                候选.Add(skill);
            }
        }

        // 冷却升序排列（冷却短的优先）
        候选.Sort((a, b) => a.冷却秒.CompareTo(b.冷却秒));

        int 已分配 = 0;
        foreach (var skill in 候选)
        {
            if (已分配 >= 需求) break;

            已分配 += skill.减伤百分比;
            _output.减伤分配.Add(new 减伤分配
            {
                技能ID = skill.技能ID, 技能名称 = skill.名称,
                职业 = skill.职业, 减伤值 = skill.减伤百分比, 团队减伤 = true
            });
            _output.执行技能IDs.Add(skill.技能ID);
        }

        _output.不足 = 已分配 < 需求;
    }

    #endregion

    #region 治疗分配

    private void 分配治疗(int 需求, List<(Jobs, bool)> 队伍)
    {
        var 候选 = new List<团队治疗>();

        foreach (var (job, isInParty) in 队伍)
        {
            if (!isInParty) continue;
            if (!DecisionSkillRegistry.团队治疗表.TryGetValue(job, out var skills)) continue;

            foreach (var skill in skills)
            {
                if (GetCooldownRemaining(skill.技能ID) > 0) continue;
                候选.Add(skill);
            }
        }

        候选.Sort((a, b) => a.冷却秒.CompareTo(b.冷却秒));

        int 已分配 = 0;
        foreach (var skill in 候选)
        {
            if (已分配 >= 需求) break;

            已分配 += skill.恢复力;
            _output.治疗分配.Add(new 治疗分配
            {
                技能ID = skill.技能ID, 技能名称 = skill.名称,
                职业 = skill.职业, 恢复力 = skill.恢复力, 是否持续 = skill.是否持续
            });
            _output.执行技能IDs.Add(skill.技能ID);
        }

        _output.不足 = 已分配 < 需求;
    }

    #endregion

    #region 工具方法

    /// <summary>获取当前队伍中所有角色的职业列表</summary>
    private static List<(Jobs Job, bool IsInParty)> GetAvailableRoles()
    {
        var result = new List<(Jobs, bool)>();
        var selfJob = (Jobs)Data.Me.ClassJob;
        result.Add((selfJob, true));

        foreach (var member in DService.Instance().PartyList)
        {
            try
            {
                var jobId = member.ClassJob.RowId;
                if (jobId == 0) continue;
                var job = (Jobs)jobId;
                if (!result.Any(r => r.Item1 == job))
                    result.Add((job, true));
            }
            catch { }
        }

        foreach (var job in DecisionSkillRegistry.团队减伤表.Keys
            .Concat(DecisionSkillRegistry.团队治疗表.Keys).Distinct())
        {
            if (!result.Any(r => r.Item1 == job))
                result.Add((job, false));
        }

        return result;
    }

    private static int GetCooldownRemaining(uint spellId) =>
        (int)global::HiAuRo.ACR.CooldownHelper.GetCooldownRemaining(spellId);

    #endregion

    #region 内置技能数据

    private static void LoadBuiltinSkills()
    {
        // BRD 诗人
        DecisionSkillRegistry.注册(Jobs.BRD,
            teamMit:
            [
                new() { 技能ID = 7561, 名称 = "策动", 职业 = Jobs.BRD, 减伤百分比 = 10, 持续秒 = 15, 冷却秒 = 90, 减伤类型 = 减伤类型.全能 },
                new() { 技能ID = 7559, 名称 = "行吟", 职业 = Jobs.BRD, 减伤百分比 = 10, 持续秒 = 15, 冷却秒 = 90, 减伤类型 = 减伤类型.全能 },
            ],
            teamHeal:
            [
                new() { 技能ID = 7560, 名称 = "光阴神的礼赞凯歌", 职业 = Jobs.BRD, 恢复力 = 200, 冷却秒 = 90, 治疗类型 = 治疗类型.持续, 是否持续 = true }
            ]);

        // MNK 武僧
        DecisionSkillRegistry.注册(Jobs.MNK,
            teamMit:
            [
                new() { 技能ID = 7549, 名称 = "牵制", 职业 = Jobs.MNK, 减伤百分比 = 10, 持续秒 = 15, 冷却秒 = 90, 减伤类型 = 减伤类型.全能 },
            ],
            personalMit:
            [
                new() { 技能ID = 3547, 名称 = "内丹", 职业 = Jobs.MNK, 减伤百分比 = 0, 持续秒 = 0, 冷却秒 = 120, 减伤类型 = 减伤类型.全能 },
            ]);

        // WHM 白魔
        DecisionSkillRegistry.注册(Jobs.WHM,
            teamMit:
            [
                new() { 技能ID = 7433, 名称 = "节制", 职业 = Jobs.WHM, 减伤百分比 = 10, 持续秒 = 20, 冷却秒 = 120, 减伤类型 = 减伤类型.全能 },
            ],
            teamHeal:
            [
                new() { 技能ID = 124, 名称 = "医济", 职业 = Jobs.WHM, 恢复力 = 600, 冷却秒 = 60, 治疗类型 = 治疗类型.持续, 是否持续 = true },
                new() { 技能ID = 7434, 名称 = "全大赦", 职业 = Jobs.WHM, 恢复力 = 800, 冷却秒 = 60, 治疗类型 = 治疗类型.直疗 },
            ]);
    }

    #endregion
}
