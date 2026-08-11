using TAHS.Roles;
using TAHS.Roles.Addons;

namespace TAHS.Modules;

/// <summary>
/// 自定义游戏选项：显示在大厅"游戏设置"菜单中，值由主机调整并经 RPC 同步给所有客户端。
/// 统一用整数存储，通过 <see cref="Scale"/> 换算实际值（如 15 × 0.1 = 1.5）。
/// </summary>
public class CustomOption
{
    /// <summary>全部已注册选项（也是 UI 和 RPC 的顺序）</summary>
    public static readonly List<CustomOption> All = new();

    /// <summary>稳定 ID（RPC 同步用，勿改动已有 ID）</summary>
    public byte Id { get; }

    /// <summary>所属职业 ID（与 RoleRegistry 一致），用于设置菜单按职业分组</summary>
    public byte RoleId { get; }

    /// <summary>设置菜单中的显示名</summary>
    public string Name { get; }

    public int Min { get; }
    public int Max { get; }
    public int Step { get; }

    /// <summary>实际值 = Value × Scale</summary>
    public float Scale { get; }

    /// <summary>布尔选项（0=关，1=开）</summary>
    public bool IsBool { get; }

    /// <summary>父选项 ID：父选项未开启（=0）时本选项在设置界面隐藏</summary>
    public byte? ParentId { get; }

    /// <summary>枚举选项的显示文本表（设置后按 Value 显示文本而不是数字）</summary>
    public string[]? FormatNames { get; }

    /// <summary>当前值（原始整数）</summary>
    public int Value;

    public float ScaledValue => Value * Scale;

    /// <summary>界面显示用的值文本</summary>
    public string DisplayValue
    {
        get
        {
            if (FormatNames != null && Value >= 0 && Value < FormatNames.Length)
                return FormatNames[Value];
            return IsBool ? (Value == 1 ? "开" : "关") : Value.ToString();
        }
    }

    private CustomOption(byte id, byte roleId, string name, int defaultValue, int min, int max, int step, float scale, bool isBool = false, byte? parentId = null, string[]? formatNames = null)
    {
        Id = id;
        RoleId = roleId;
        Name = name;
        Value = defaultValue;
        Min = min;
        Max = max;
        Step = step;
        Scale = scale;
        IsBool = isBool;
        ParentId = parentId;
        FormatNames = formatNames;
    }

    public static CustomOption Register(byte id, byte roleId, string name, int defaultValue, int min, int max, int step, float scale, bool isBool = false, byte? parentId = null, string[]? formatNames = null)
    {
        var opt = new CustomOption(id, roleId, name, defaultValue, min, max, step, scale, isBool, parentId, formatNames);
        All.Add(opt);
        return opt;
    }

    public static CustomOption? Get(byte id) => All.FirstOrDefault(o => o.Id == id);

    /// <summary>某职业的全部选项（含生成概率）</summary>
    public static IEnumerable<CustomOption> OfRole(byte roleId) => All.Where(o => o.RoleId == roleId);
}

/// <summary>
/// TAHS 的全部游戏选项。默认值取自 BepInEx 配置（ModConfig），
/// 大厅中主机调整后经 RPC 覆盖到各端。
/// </summary>
public static class CustomOptions
{
    /// <summary>模组全局设置的组 ID（RoleId 0，职业 ID 从 1 开始）</summary>
    public const byte ModGroupId = 0;

    public static CustomOption ImpostorKnowEachOther { get; private set; } = null!;
    public static CustomOption TestMode { get; private set; } = null!;
    public static CustomOption ModeratorList { get; private set; } = null!;
    public static CustomOption ModAllowStart { get; private set; } = null!;
    public static CustomOption ModAllowS { get; private set; } = null!;
    public static CustomOption ModAllowEnd { get; private set; } = null!;
    public static CustomOption CheatAction { get; private set; } = null!;
    public static CustomOption NeutralKnifeCount { get; private set; } = null!;
    public static CustomOption NeutralNoKnifeCount { get; private set; } = null!;
    public static CustomOption MoonSkillCd { get; private set; } = null!;
    public static CustomOption MoonBuffDuration { get; private set; } = null!;
    public static CustomOption MoonBuffInitial { get; private set; } = null!;
    public static CustomOption MoonBuffRate { get; private set; } = null!;
    public static CustomOption MoonBuffMaxStacks { get; private set; } = null!;
    public static CustomOption MoonReveal { get; private set; } = null!;
    public static CustomOption MoonHuntCd { get; private set; } = null!;
    public static CustomOption MoonHuntSuicideTime { get; private set; } = null!;
    public static CustomOption PilotSkillCd { get; private set; } = null!;
    public static CustomOption PilotCanNormalKill { get; private set; } = null!;
    public static CustomOption PilotKillCd { get; private set; } = null!;
    public static CustomOption PilotSurviveExplosion { get; private set; } = null!;
    public static CustomOption PilotFriendlyFire { get; private set; } = null!;
    public static CustomOption PilotDashSpeed { get; private set; } = null!;
    public static CustomOption PilotDashKillRange { get; private set; } = null!;
    public static CustomOption PilotExplosionRange { get; private set; } = null!;
    public static CustomOption GuessMode { get; private set; } = null!;
    public static CustomOption GuessCrewmate { get; private set; } = null!;
    public static CustomOption GuessImpostor { get; private set; } = null!;
    public static CustomOption GuessFriendlyNeutral { get; private set; } = null!;
    public static CustomOption GuessHostileNeutral { get; private set; } = null!;
    public static CustomOption CowardKillsToConvert { get; private set; } = null!;
    public static CustomOption CowardConvertTime { get; private set; } = null!;
    public static CustomOption CowardConvertRange { get; private set; } = null!;
    public static CustomOption CopKillCooldown { get; private set; } = null!;
    public static CustomOption CopAutoKillRange { get; private set; } = null!;
    public static CustomOption CopAutoKillTime { get; private set; } = null!;
    public static CustomOption CopAutoKillsToConvert { get; private set; } = null!;
    public static CustomOption CopKillCrewmateAlsoDies { get; private set; } = null!;
    public static CustomOption RepenterKillsToConvert { get; private set; } = null!;
    public static CustomOption RepenterSuicideTime { get; private set; } = null!;
    public static CustomOption MinisterCustomTaskCount { get; private set; } = null!;
    public static CustomOption MinisterLongTasks { get; private set; } = null!;
    public static CustomOption MinisterMidTasks { get; private set; } = null!;
    public static CustomOption MinisterShortTasks { get; private set; } = null!;
    public static CustomOption MinisterStealCount { get; private set; } = null!;
    public static CustomOption CopKillMinisterRange { get; private set; } = null!;
    public static CustomOption MinisterTaskDeadline { get; private set; } = null!;
    public static CustomOption GuesserCanGuessAddons { get; private set; } = null!;
    public static CustomOption FarmerStealChance { get; private set; } = null!;
    public static CustomOption FarmerStealsForKill { get; private set; } = null!;
    public static CustomOption FarmerKillCooldown { get; private set; } = null!;
    public static CustomOption FarmerStealRange { get; private set; } = null!;

    public static void Init()
    {
        if (CustomOption.All.Count > 0) return;

        // 模组全局设置（ID 100 起）
        ImpostorKnowEachOther = CustomOption.Register(100, ModGroupId, "内鬼互认",
            ModConfig.ImpostorKnowEachOther.Value ? 1 : 0, 0, 1, 1, 1f, isBool: true);
        TestMode = CustomOption.Register(106, ModGroupId, "测试模式",
            ModConfig.TestMode.Value ? 1 : 0, 0, 1, 1, 1f, isBool: true);
        ModeratorList = CustomOption.Register(129, ModGroupId, "协管名单",
            ModConfig.ModeratorList.Value ? 1 : 0, 0, 1, 1, 1f, isBool: true);
        ModAllowStart = CustomOption.Register(130, ModGroupId, "协管权限：/start",
            ModConfig.ModAllowStart.Value ? 1 : 0, 0, 1, 1, 1f, isBool: true, parentId: 129);
        ModAllowS = CustomOption.Register(131, ModGroupId, "协管权限：/s",
            ModConfig.ModAllowS.Value ? 1 : 0, 0, 1, 1, 1f, isBool: true, parentId: 129);
        ModAllowEnd = CustomOption.Register(132, ModGroupId, "协管权限：/end与ALT+F4",
            ModConfig.ModAllowEnd.Value ? 1 : 0, 0, 1, 1, 1f, isBool: true, parentId: 129);
        CheatAction = CustomOption.Register(133, ModGroupId, "作弊处理方式",
            ModConfig.CheatAction.Value, 0, 3, 1, 1f,
            formatNames: new[] { "警告", "踢出", "封禁", "加入黑名单" });

        // 中立阵营数量（RoleId 98 = 中立分类页）
        NeutralKnifeCount = CustomOption.Register(150, 98, "带刀中立数量",
            ModConfig.NeutralKnifeCount.Value, 0, 7, 1, 1f);
        NeutralNoKnifeCount = CustomOption.Register(151, 98, "无刀中立数量",
            ModConfig.NeutralNoKnifeCount.Value, 0, 7, 1, 1f);

        // 月跑入机参数（RoleId 10 = MoonRunner）
        MoonSkillCd = CustomOption.Register(134, 10, "技能CD(秒)",
            ModConfig.MoonSkillCd.Value, 5, 60, 5, 1f);
        MoonBuffDuration = CustomOption.Register(135, 10, "增益持续时间(秒)",
            ModConfig.MoonBuffDuration.Value, 5, 120, 5, 1f);
        MoonBuffInitial = CustomOption.Register(136, 10, "初始增益速度%",
            ModConfig.MoonBuffInitial.Value, 100, 200, 5, 0.01f);
        MoonBuffRate = CustomOption.Register(137, 10, "每次叠加增益倍率%",
            ModConfig.MoonBuffRate.Value, 100, 200, 5, 0.01f);
        MoonBuffMaxStacks = CustomOption.Register(138, 10, "达到最大值所需次数",
            ModConfig.MoonBuffMaxStacks.Value, 2, 10, 1, 1f);
        MoonReveal = CustomOption.Register(139, 10, "是否暴露双方身份",
            ModConfig.MoonReveal.Value ? 1 : 0, 0, 1, 1, 1f, isBool: true);
        MoonHuntCd = CustomOption.Register(140, 10, "追杀击杀CD(秒)",
            ModConfig.MoonHuntCd.Value, 5, 60, 5, 1f);
        MoonHuntSuicideTime = CustomOption.Register(141, 10, "追杀自杀时间(秒)",
            ModConfig.MoonHuntSuicideTime.Value, 10, 180, 5, 1f);

        // 中东机长参数（RoleId 11 = Pilot）
        PilotSkillCd = CustomOption.Register(142, 11, "技能冷却(秒)",
            ModConfig.PilotSkillCd.Value, 5, 120, 5, 1f);
        PilotCanNormalKill = CustomOption.Register(143, 11, "是否可以正常击杀",
            ModConfig.PilotCanNormalKill.Value ? 1 : 0, 0, 1, 1, 1f, isBool: true);
        PilotKillCd = CustomOption.Register(144, 11, "击杀冷却(秒)",
            ModConfig.PilotKillCd.Value, 5, 120, 5, 1f);
        PilotSurviveExplosion = CustomOption.Register(145, 11, "爆炸中是否存活",
            ModConfig.PilotSurviveExplosion.Value ? 1 : 0, 0, 1, 1, 1f, isBool: true);
        PilotFriendlyFire = CustomOption.Register(146, 11, "技能是否误杀队友",
            ModConfig.PilotFriendlyFire.Value ? 1 : 0, 0, 1, 1, 1f, isBool: true);
        PilotDashSpeed = CustomOption.Register(147, 11, "冲刺速度×0.1",
            ModConfig.PilotDashSpeed.Value, 10, 100, 5, 0.1f);
        PilotDashKillRange = CustomOption.Register(148, 11, "冲刺击杀范围×0.1",
            ModConfig.PilotDashKillRange.Value, 5, 50, 5, 0.1f);
        PilotExplosionRange = CustomOption.Register(149, 11, "爆炸击杀范围×0.1",
            ModConfig.PilotExplosionRange.Value, 5, 60, 5, 0.1f);
        GuessMode = CustomOption.Register(107, ModGroupId, "猜测模式",
            ModConfig.GuessMode.Value ? 1 : 0, 0, 1, 1, 1f, isBool: true);
        GuessCrewmate = CustomOption.Register(108, ModGroupId, "猜测模式：船员可猜测",
            ModConfig.GuessCrewmate.Value ? 1 : 0, 0, 1, 1, 1f, isBool: true, parentId: 107);
        GuessImpostor = CustomOption.Register(109, ModGroupId, "猜测模式：内鬼可猜测",
            ModConfig.GuessImpostor.Value ? 1 : 0, 0, 1, 1, 1f, isBool: true, parentId: 107);
        GuessFriendlyNeutral = CustomOption.Register(110, ModGroupId, "猜测模式：友好中立可猜测",
            ModConfig.GuessFriendlyNeutral.Value ? 1 : 0, 0, 1, 1, 1f, isBool: true, parentId: 107);
        GuessHostileNeutral = CustomOption.Register(111, ModGroupId, "猜测模式：敌对中立可猜测",
            ModConfig.GuessHostileNeutral.Value ? 1 : 0, 0, 1, 1, 1f, isBool: true, parentId: 107);

        // 懦弱者参数（RoleId 5 = Coward）
        CowardKillsToConvert = CustomOption.Register(112, 5, "转变阵营所需击杀数",
            ModConfig.CowardKillsToConvert.Value, 1, 10, 1, 1f);
        CowardConvertTime = CustomOption.Register(113, 5, "转变阵营所需贴近时间(秒)",
            ModConfig.CowardConvertTime.Value, 1, 30, 1, 1f);
        CowardConvertRange = CustomOption.Register(114, 5, "转变阵营所需贴近距离×0.1",
            ModConfig.CowardConvertRange.Value, 5, 30, 5, 0.1f);

        // 美警参数（RoleId 6 = Cop）
        CopKillCooldown = CustomOption.Register(115, 6, "击杀时间(秒)",
            ModConfig.CopKillCooldown.Value, 5, 120, 5, 1f);
        CopAutoKillRange = CustomOption.Register(116, 6, "自动击杀距离×0.1",
            ModConfig.CopAutoKillRange.Value, 5, 30, 5, 0.1f);
        CopAutoKillTime = CustomOption.Register(117, 6, "自动击杀所需时间(秒)",
            ModConfig.CopAutoKillTime.Value, 1, 30, 1, 1f);
        CopAutoKillsToConvert = CustomOption.Register(118, 6, "转内鬼所需自动击杀人数",
            ModConfig.CopAutoKillsToConvert.Value, 1, 10, 1, 1f);
        CopKillCrewmateAlsoDies = CustomOption.Register(119, 6, "手动击杀船员时船员是否死亡",
            ModConfig.CopKillCrewmateAlsoDies.Value ? 1 : 0, 0, 1, 1, 1f, isBool: true);

        // 忏悔者参数（RoleId 7 = Repenter）
        RepenterKillsToConvert = CustomOption.Register(120, 7, "击杀多少人可转换阵营",
            ModConfig.RepenterKillsToConvert.Value, 1, 10, 1, 1f);
        RepenterSuicideTime = CustomOption.Register(121, 7, "转换阵营后多少秒自杀",
            ModConfig.RepenterSuicideTime.Value, 10, 300, 10, 1f);

        // 内阁参数（RoleId 8 = Minister）
        MinisterCustomTaskCount = CustomOption.Register(122, 8, "是否单独设置任务数量",
            ModConfig.MinisterCustomTaskCount.Value ? 1 : 0, 0, 1, 1, 1f, isBool: true);
        MinisterLongTasks = CustomOption.Register(123, 8, "长任务数",
            ModConfig.MinisterLongTasks.Value, 0, 10, 1, 1f, parentId: 122);
        MinisterMidTasks = CustomOption.Register(124, 8, "中任务数",
            ModConfig.MinisterMidTasks.Value, 0, 10, 1, 1f, parentId: 122);
        MinisterShortTasks = CustomOption.Register(125, 8, "短任务数",
            ModConfig.MinisterShortTasks.Value, 0, 10, 1, 1f, parentId: 122);
        MinisterStealCount = CustomOption.Register(126, 8, "完成任务时夺取的任务数量",
            ModConfig.MinisterStealCount.Value, 1, 10, 1, 1f);
        CopKillMinisterRange = CustomOption.Register(127, 8, "美警击杀内阁距离×0.1",
            ModConfig.CopKillMinisterRange.Value, 5, 50, 5, 0.1f);
        MinisterTaskDeadline = CustomOption.Register(128, 8, "任务限时(秒)",
            ModConfig.MinisterTaskDeadline.Value, 10, 300, 10, 1f);

        // 每个职业一项生成概率（ID 与 RoleRegistry 的职业 ID 相同）+ 一项人数（ID + 50）
        foreach (var (id, name, _) in CustomRoleManager.GetRegisteredRoles())
        {
            CustomOption.Register(id, id, "生成概率%", 100, 0, 100, 10, 1f);
            CustomOption.Register((byte)(id + 50), id, "人数", 1, 1, 15, 1, 1f);
        }

        // 每个附加职业一项生成概率 + 一项人数（ID 规则同上）
        foreach (var (id, name) in CustomRoleManager.GetRegisteredAddons())
        {
            CustomOption.Register(id, id, "生成概率%", 100, 0, 100, 10, 1f);
            CustomOption.Register((byte)(id + 50), id, "人数", 1, 1, 15, 1, 1f);
        }

        // 赌怪参数（RoleId 4 = Guesser.AddonId）
        GuesserCanGuessAddons = CustomOption.Register(105, Guesser.AddonId, "可猜测附加职业",
            ModConfig.GuesserCanGuessAddons.Value ? 1 : 0, 0, 1, 1, 1f, isBool: true);

        // 佃农职业参数（ID 从 101 起；RoleId 2 对应 RoleRegistry 中的 Farmer）
        const byte farmerRoleId = 2;
        FarmerStealChance = CustomOption.Register(101, farmerRoleId, "抢夺概率%",
            (int)(ModConfig.FarmerStealChance.Value * 100), 0, 100, 5, 0.01f);
        FarmerStealsForKill = CustomOption.Register(102, farmerRoleId, "解锁击杀任务数",
            ModConfig.FarmerStealsForKill.Value, 1, 10, 1, 1f);
        FarmerKillCooldown = CustomOption.Register(103, farmerRoleId, "击杀CD(秒)",
            (int)ModConfig.FarmerKillCooldown.Value, 5, 120, 5, 1f);
        FarmerStealRange = CustomOption.Register(104, farmerRoleId, "抢夺范围×0.1",
            (int)(ModConfig.FarmerStealRange.Value * 10), 5, 50, 5, 0.1f);
    }

    /// <summary>某职业的生成概率（0~100），未注册默认 100</summary>
    public static int GetRoleChance(byte roleId) => CustomOption.Get(roleId)?.Value ?? 100;

    /// <summary>某职业的人数（每次出场独立判定概率），未注册默认 1</summary>
    public static int GetRoleCount(byte roleId) => CustomOption.Get((byte)(roleId + 50))?.Value ?? 1;
}