using HiAuRo.ACR;
using HiAuRo.ACR.HotkeyResolvers;
using HiAuRo.Runtime;

namespace HiAuRo.Jobs.BRD;

/// <summary>
/// 诗人 ACR 入口 —— QT 悬浮窗 + 热键使用示例
/// </summary>
public sealed class BRDRotationEntry : IRotationEntry
{
    public string AuthorName => "HiAuRo";
    public bool UseCustomUi => false;

    public Rotation? Build(string settingFolder)
    {
        BRDBattleData.Init(settingFolder);

        return new Rotation
        {
            TargetJob = ACR.Jobs.BRD,
            AcrType = AcrType.PvE,
            MinLevel = 1,
            MaxLevel = 100,
            Description = "HiAuRo 诗人打样 — 1 GCD (强力射击) + 1 oGCD (失血箭)",
            SlotResolvers =
            [
                new SlotResolverData { Resolver = new BRD_GCD_强力射击(), Mode = SlotMode.Gcd },
                new SlotResolverData { Resolver = new BRD_oGCD_失血箭(), Mode = SlotMode.OffGcd },
            ],
            Opener = new BRDOpener(),
        };
    }

    public IRotationUI? GetRotationUI() => new BRDRotationUI();
    public void OnDrawSetting() { }
    public void Dispose() => BRDBattleData.Reset();
    public void OnEnterRotation() { }
    public void OnExitRotation() { }

    public IEnumerable<ACR.Jobs> TargetJobs => [ACR.Jobs.BRD];
}

/// <summary>
/// 诗人 QT 悬浮窗 + 热键注册
/// </summary>
public sealed class BRDRotationUI : IRotationUI
{
    public void RegisterControls(IUiBuilder builder)
    {
        builder.AddMainControl(showPause: true, showSave: true);

        // === Setting Tab ===
        builder.AddTab("settings", "设置");
        builder.AddGroup("basic", "基础");
        builder.AddCheckbox("intelligent", "智能模式", true);
        builder.AddTooltip("intelligent", "自动选择最优技能，关闭后精打循环");
        builder.AddCheckbox("aoe", "AOE 模式", false);
        builder.AddTooltip("aoe", "开启后优先使用群体技能");
        builder.AddCheckbox("autoTarget", "自动选敌", true);
        builder.AddTooltip("autoTarget", "无目标时自动选择附近敌人");

        builder.AddGroup("combat", "战斗参数");
        builder.AddSlider("aoeCount", "AOE 目标数", 2, 10, 3);
        builder.AddDropdown("songPriority", "歌曲优先", ["军神", "放浪神", "贤者"], "军神");
        builder.AddIntInput("maxOgcd", "最大 oGCD", 2, 1, 2);

        // === Debug Tab ===
        builder.AddTab("debug", "调试");
        builder.AddGroup("info", "状态");
        builder.AddLabel("brdState", "职业: 诗人");
        builder.AddLabel("gcdInfo", "GCD: 2.50s");
        builder.AddCheckbox("debugLog", "调试日志", false);

        // === QT 开关 ===
        builder.AddQtToggle("intelligent", "智能模式", true,
            tooltip: "自动选择最优技能，关闭后精打循环");
        builder.AddQtToggle("aoe", "AOE 模式", false,
            tooltip: "开启后优先使用群体技能");
        builder.AddQtToggle("autoTarget", "自动选敌", true,
            tooltip: "无目标时自动选择附近敌人");

        // === QT 热键 ===
        builder.AddQtHotkey("疾跑", new HotkeyResolver_Sprint());
        builder.AddQtHotkey("爆发药", new HotkeyResolver_Potion());
        builder.AddQtHotkey("极限技", new HotkeyResolver_LB());
        builder.AddQtHotkey("猛者强击", new HotkeyResolver_NormalSpell(
            SpellsDefine.猛者强击, "猛者强击", SpellTargetType.Self));
        builder.AddQtHotkey("失血箭", new HotkeyResolver_NormalSpell(
            SpellsDefine.失血箭, "失血箭", SpellTargetType.Target));
        builder.AddQtHotkey("战斗之声", new HotkeyResolver_NormalSpell(
            SpellsDefine.战斗之声, "战斗之声", SpellTargetType.Self));
    }
}
