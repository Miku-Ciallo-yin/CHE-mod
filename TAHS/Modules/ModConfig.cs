using BepInEx.Configuration;

namespace TAHS.Modules;

/// <summary>
/// 模组配置。在游戏目录 BepInEx\config\com.mikuqiayou.che.cfg 中修改，重启游戏生效。
/// TODO: 参考 TONE 做大厅内的图形化选项界面。
/// </summary>
public static class ModConfig
{
    /// <summary>佃农：靠近船员时每秒抢夺任务的概率（0~1，默认 0.2）</summary>
    public static ConfigEntry<float> FarmerStealChance { get; private set; } = null!;

    /// <summary>佃农：获得击杀能力所需抢夺的任务数（默认 3）</summary>
    public static ConfigEntry<int> FarmerStealsForKill { get; private set; } = null!;

    /// <summary>佃农：击杀冷却时间（秒，默认 30）</summary>
    public static ConfigEntry<float> FarmerKillCooldown { get; private set; } = null!;

    /// <summary>佃农：抢夺任务的靠近范围（游戏单位，默认 1.5）</summary>
    public static ConfigEntry<float> FarmerStealRange { get; private set; } = null!;

    /// <summary>模组设置：内鬼是否互认（默认开）</summary>
    public static ConfigEntry<bool> ImpostorKnowEachOther { get; private set; } = null!;

    /// <summary>赌怪：是否可以猜测附加职业（默认关）</summary>
    public static ConfigEntry<bool> GuesserCanGuessAddons { get; private set; } = null!;

    /// <summary>猜测模式：总开关</summary>
    public static ConfigEntry<bool> GuessMode { get; private set; } = null!;
    /// <summary>猜测模式：船员可猜测</summary>
    public static ConfigEntry<bool> GuessCrewmate { get; private set; } = null!;
    /// <summary>猜测模式：内鬼可猜测</summary>
    public static ConfigEntry<bool> GuessImpostor { get; private set; } = null!;
    /// <summary>猜测模式：友好中立可猜测</summary>
    public static ConfigEntry<bool> GuessFriendlyNeutral { get; private set; } = null!;
    /// <summary>猜测模式：敌对中立可猜测</summary>
    public static ConfigEntry<bool> GuessHostileNeutral { get; private set; } = null!;

    /// <summary>懦弱者：转变阵营所需击杀数（默认 3）</summary>
    public static ConfigEntry<int> CowardKillsToConvert { get; private set; } = null!;
    /// <summary>懦弱者：转变阵营所需贴近时间秒（默认 5）</summary>
    public static ConfigEntry<int> CowardConvertTime { get; private set; } = null!;
    /// <summary>懦弱者：转变阵营所需贴近距离 ×0.1（默认 15 = 1.5）</summary>
    public static ConfigEntry<int> CowardConvertRange { get; private set; } = null!;

    /// <summary>美警：击杀时间秒（默认 25，转变内鬼前）</summary>
    public static ConfigEntry<int> CopKillCooldown { get; private set; } = null!;
    /// <summary>美警：自动击杀距离 ×0.1（默认 15 = 1.5）</summary>
    public static ConfigEntry<int> CopAutoKillRange { get; private set; } = null!;
    /// <summary>美警：自动击杀所需时间秒（默认 5）</summary>
    public static ConfigEntry<int> CopAutoKillTime { get; private set; } = null!;
    /// <summary>美警：转内鬼所需自动击杀人数（默认 3）</summary>
    public static ConfigEntry<int> CopAutoKillsToConvert { get; private set; } = null!;
    /// <summary>美警：手动击杀船员时船员是否也死亡（默认关，美警始终自杀抵命）</summary>
    public static ConfigEntry<bool> CopKillCrewmateAlsoDies { get; private set; } = null!;

    /// <summary>忏悔者：击杀多少人可转换阵营（默认 3）</summary>
    public static ConfigEntry<int> RepenterKillsToConvert { get; private set; } = null!;
    /// <summary>忏悔者：转换阵营后多少秒自杀（默认 60）</summary>
    public static ConfigEntry<int> RepenterSuicideTime { get; private set; } = null!;

    /// <summary>内阁：是否单独设置任务数量（默认关）</summary>
    public static ConfigEntry<bool> MinisterCustomTaskCount { get; private set; } = null!;
    /// <summary>内阁：长任务数（默认 1）</summary>
    public static ConfigEntry<int> MinisterLongTasks { get; private set; } = null!;
    /// <summary>内阁：中任务数（默认 2）</summary>
    public static ConfigEntry<int> MinisterMidTasks { get; private set; } = null!;
    /// <summary>内阁：短任务数（默认 2）</summary>
    public static ConfigEntry<int> MinisterShortTasks { get; private set; } = null!;
    /// <summary>内阁：完成任务时夺取的任务数量（默认 2）</summary>
    public static ConfigEntry<int> MinisterStealCount { get; private set; } = null!;
    /// <summary>内阁：美警击杀内阁距离 ×0.1（默认 20 = 2.0）</summary>
    public static ConfigEntry<int> CopKillMinisterRange { get; private set; } = null!;
    /// <summary>内阁：任务限时秒（默认 60，超时自杀）</summary>
    public static ConfigEntry<int> MinisterTaskDeadline { get; private set; } = null!;

    /// <summary>模组设置：测试模式，游戏不会正常结束（默认关）</summary>
    public static ConfigEntry<bool> TestMode { get; private set; } = null!;

    /// <summary>模组设置：协管名单（默认关），名单文件 TAHS-DATA/Moderators.txt</summary>
    public static ConfigEntry<bool> ModeratorList { get; private set; } = null!;
    /// <summary>协管权限：/start（默认开）</summary>
    public static ConfigEntry<bool> ModAllowStart { get; private set; } = null!;
    /// <summary>协管权限：/s（默认开）</summary>
    public static ConfigEntry<bool> ModAllowS { get; private set; } = null!;
    /// <summary>协管权限：/end 与 ALT+F4（默认开）</summary>
    public static ConfigEntry<bool> ModAllowEnd { get; private set; } = null!;
    /// <summary>作弊处理方式（0=警告 1=踢出 2=封禁 3=加入黑名单，默认踢出）</summary>
    public static ConfigEntry<int> CheatAction { get; private set; } = null!;
    /// <summary>带刀中立数量（默认 1，0~7）</summary>
    public static ConfigEntry<int> NeutralKnifeCount { get; private set; } = null!;
    /// <summary>无刀中立数量（默认 1，0~7）</summary>
    public static ConfigEntry<int> NeutralNoKnifeCount { get; private set; } = null!;
    /// <summary>玩家附加职业数量上限（默认 1，1~5）</summary>
    public static ConfigEntry<int> MaxAddonsPerPlayer { get; private set; } = null!;

    public static ConfigEntry<int> MoonSkillCd { get; private set; } = null!;
    public static ConfigEntry<int> MoonBuffDuration { get; private set; } = null!;
    public static ConfigEntry<int> MoonBuffInitial { get; private set; } = null!;
    public static ConfigEntry<int> MoonBuffRate { get; private set; } = null!;
    public static ConfigEntry<int> MoonBuffMaxStacks { get; private set; } = null!;
    public static ConfigEntry<bool> MoonReveal { get; private set; } = null!;
    public static ConfigEntry<int> MoonHuntCd { get; private set; } = null!;
    public static ConfigEntry<int> MoonHuntSuicideTime { get; private set; } = null!;

    public static ConfigEntry<int> PilotSkillCd { get; private set; } = null!;
    public static ConfigEntry<bool> PilotCanNormalKill { get; private set; } = null!;
    public static ConfigEntry<int> PilotKillCd { get; private set; } = null!;
    public static ConfigEntry<bool> PilotSurviveExplosion { get; private set; } = null!;
    public static ConfigEntry<bool> PilotFriendlyFire { get; private set; } = null!;
    public static ConfigEntry<int> PilotDashSpeed { get; private set; } = null!;
    public static ConfigEntry<int> PilotDashKillRange { get; private set; } = null!;
    public static ConfigEntry<int> PilotExplosionRange { get; private set; } = null!;

    /// <summary>主菜单：GitHub 按钮链接</summary>
    public static ConfigEntry<string> GithubUrl { get; private set; } = null!;

    /// <summary>主菜单：交流群按钮链接</summary>
    public static ConfigEntry<string> CommunityUrl { get; private set; } = null!;

    public static void Init(ConfigFile config)
    {
        FarmerStealChance = config.Bind("佃农 Farmer", "抢夺概率 StealChance", 0.2f,
            "靠近船员时每秒抢夺一个任务的概率（0~1）");
        FarmerStealsForKill = config.Bind("佃农 Farmer", "解锁击杀所需任务数 StealsForKill", 3,
            "抢夺多少个任务后（并完成现有任务）获得击杀能力");
        FarmerKillCooldown = config.Bind("佃农 Farmer", "击杀冷却 KillCooldown", 30f,
            "击杀能力冷却时间（秒）");
        FarmerStealRange = config.Bind("佃农 Farmer", "抢夺范围 StealRange", 1.5f,
            "距离船员多近可以抢夺任务（游戏单位）");

        ImpostorKnowEachOther = config.Bind("模组设置 Mod", "内鬼互认 ImpostorKnowEachOther", true,
            "内鬼之间是否互相可见（红色名字）；关闭后内鬼互不相识");

        GuesserCanGuessAddons = config.Bind("赌怪 Guesser", "可猜测附加职业 CanGuessAddons", false,
            "开启后赌怪的猜测列表包含附加职业");

        GuessMode = config.Bind("猜测模式 GuessMode", "开启 Enable", false,
            "开启后按阵营勾选决定谁可以猜测（无需赌怪附加职业）");
        GuessCrewmate = config.Bind("猜测模式 GuessMode", "船员可猜测 Crewmate", false, "船员阵营可使用猜测");
        GuessImpostor = config.Bind("猜测模式 GuessMode", "内鬼可猜测 Impostor", false, "内鬼阵营可使用猜测");
        GuessFriendlyNeutral = config.Bind("猜测模式 GuessMode", "友好中立可猜测 FriendlyNeutral", false, "友好中立可使用猜测");
        GuessHostileNeutral = config.Bind("猜测模式 GuessMode", "敌对中立可猜测 HostileNeutral", false, "敌对中立可使用猜测");

        CowardKillsToConvert = config.Bind("懦弱者 Coward", "转变阵营所需击杀数 KillsToConvert", 3,
            "击杀多少人后进入贴近转化阶段");
        CowardConvertTime = config.Bind("懦弱者 Coward", "转变阵营所需贴近时间 ConvertTime", 5,
            "贴近同一名玩家多少秒后转变阵营（秒）");
        CowardConvertRange = config.Bind("懦弱者 Coward", "转变阵营所需贴近距离 ConvertRange", 15,
            "判定贴近的距离（×0.1 游戏单位，默认 15 = 1.5）");

        CopKillCooldown = config.Bind("美警 Cop", "击杀时间 KillCooldown", 25,
            "手动击杀冷却（秒），转变内鬼前生效，转变后跟随全局设置");
        CopAutoKillRange = config.Bind("美警 Cop", "自动击杀距离 AutoKillRange", 15,
            "自动击杀深色船员的贴近距离（×0.1 游戏单位，默认 15 = 1.5）");
        CopAutoKillTime = config.Bind("美警 Cop", "自动击杀所需时间 AutoKillTime", 5,
            "贴近深色船员多少秒后自动击杀（秒）");
        CopAutoKillsToConvert = config.Bind("美警 Cop", "转内鬼所需自动击杀人数 AutoKillsToConvert", 3,
            "自动击杀多少名深色船员后转变为内鬼阵营");
        CopKillCrewmateAlsoDies = config.Bind("美警 Cop", "手动击杀船员时船员是否死亡 KillCrewmateAlsoDies", false,
            "开启后美警手动击杀船员时船员也死亡（美警仍会自杀抵命）；关闭则只有美警死亡");

        RepenterKillsToConvert = config.Bind("忏悔者 Repenter", "击杀多少人可转换阵营 KillsToConvert", 3,
            "击杀多少人后可以使用变形转变为船员阵营");
        RepenterSuicideTime = config.Bind("忏悔者 Repenter", "转换阵营后多少秒自杀 SuicideTime", 60,
            "转变为船员阵营后多少秒自裁（秒）");

        MinisterCustomTaskCount = config.Bind("内阁 Minister", "是否单独设置任务数量 CustomTaskCount", false,
            "开启后内阁的任务按下方长/中/短任务数分配；关闭则使用原版任务");
        MinisterLongTasks = config.Bind("内阁 Minister", "长任务数 LongTasks", 1, "单独设置时的长任务数量");
        MinisterMidTasks = config.Bind("内阁 Minister", "中任务数 MidTasks", 2, "单独设置时的中（普通）任务数量");
        MinisterShortTasks = config.Bind("内阁 Minister", "短任务数 ShortTasks", 2, "单独设置时的短任务数量");
        MinisterStealCount = config.Bind("内阁 Minister", "完成任务时夺取的任务数量 StealCount", 2,
            "内阁完成全部任务时从随机船员处夺取的任务数量");
        CopKillMinisterRange = config.Bind("内阁 Minister", "美警击杀内阁距离 KillMinisterRange", 20,
            "美警贴近内阁直接击杀的距离（×0.1 游戏单位，默认 20 = 2.0）");
        MinisterTaskDeadline = config.Bind("内阁 Minister", "任务限时 TaskDeadline", 60,
            "内阁完成全部任务的限时（秒），超时自杀");

        TestMode = config.Bind("模组设置 Mod", "测试模式 TestMode", false,
            "开启后游戏不会正常结束，需用 /end 或 ALT+F4 手动强制结束");

        ModeratorList = config.Bind("模组设置 Mod", "协管名单 ModeratorList", false,
            "开启后协管名单（TAHS-DATA/Moderators.txt）内的玩家可使用 /start、/end 等房主指令");
        ModAllowStart = config.Bind("模组设置 Mod", "协管权限允许start ModAllowStart", true, "协管可使用 /start");
        ModAllowS = config.Bind("模组设置 Mod", "协管权限允许s ModAllowS", true, "协管可使用 /s 发布公告");
        ModAllowEnd = config.Bind("模组设置 Mod", "协管权限允许end ModAllowEnd", true, "协管可使用 /end 与 ALT+F4 结束对局");
        CheatAction = config.Bind("模组设置 Mod", "作弊处理方式 CheatAction", 1,
            "反作弊发现作弊的处理方式：0=警告 1=踢出 2=封禁 3=加入黑名单");
        NeutralKnifeCount = config.Bind("职业数量 RoleCount", "带刀中立数量 NeutralKnifeCount", 1,
            "每局分配的敌对（带刀）中立职业数量（0~7）");
        NeutralNoKnifeCount = config.Bind("职业数量 RoleCount", "无刀中立数量 NeutralNoKnifeCount", 1,
            "每局分配的友好（无刀）中立职业数量（0~7）");
        MaxAddonsPerPlayer = config.Bind("职业数量 RoleCount", "玩家附加职业数量上限 MaxAddonsPerPlayer", 1,
            "每名玩家最多拥有的附加职业数量（1~5）");

        MoonSkillCd = config.Bind("月跑入机 MoonRunner", "技能CD SkillCd", 15, "技能冷却（秒）");
        MoonBuffDuration = config.Bind("月跑入机 MoonRunner", "增益持续时间 BuffDuration", 30, "速度增益持续（秒）");
        MoonBuffInitial = config.Bind("月跑入机 MoonRunner", "初始增益速度 BuffInitial", 110, "初始增益速度（%，默认 110 = 1.1 倍）");
        MoonBuffRate = config.Bind("月跑入机 MoonRunner", "每次叠加增益倍率 BuffRate", 125, "对同一人每次叠加倍率（%，默认 125 = ×1.25）");
        MoonBuffMaxStacks = config.Bind("月跑入机 MoonRunner", "达到最大值所需次数 BuffMaxStacks", 3, "对同一玩家使用几次技能达到增益最大值");
        MoonReveal = config.Bind("月跑入机 MoonRunner", "是否暴露双方身份 Reveal", true, "增益达到最大值时是否自动透露双方身份");
        MoonHuntCd = config.Bind("月跑入机 MoonRunner", "追杀击杀CD HuntCd", 10, "追杀者击杀后者的冷却（秒）");
        MoonHuntSuicideTime = config.Bind("月跑入机 MoonRunner", "追杀自杀时间 HuntSuicideTime", 45, "追杀者限期击杀后者的秒数，超时自杀且无法胜利");

        PilotSkillCd = config.Bind("中东机长 Pilot", "技能冷却 SkillCd", 30, "冲刺技能冷却（秒）");
        PilotCanNormalKill = config.Bind("中东机长 Pilot", "是否可以正常击杀 CanNormalKill", true, "关闭后中东机长不能手动击杀");
        PilotKillCd = config.Bind("中东机长 Pilot", "击杀冷却 KillCd", 25, "手动击杀冷却（秒）");
        PilotSurviveExplosion = config.Bind("中东机长 Pilot", "爆炸中是否存活 SurviveExplosion", true, "关闭后爆炸会杀死中东机长自己");
        PilotFriendlyFire = config.Bind("中东机长 Pilot", "技能是否误杀队友 FriendlyFire", false, "开启后技能会波及内鬼队友");
        PilotDashSpeed = config.Bind("中东机长 Pilot", "冲刺速度 DashSpeed", 25, "冲刺速度（×0.1，默认 25 = 2.5）");
        PilotDashKillRange = config.Bind("中东机长 Pilot", "冲刺击杀范围 DashKillRange", 10, "冲刺沿途击杀范围（×0.1，默认 10 = 1.0）");
        PilotExplosionRange = config.Bind("中东机长 Pilot", "爆炸击杀范围 ExplosionRange", 20, "爆炸击杀范围（×0.1，默认 20 = 2.0）");

        GithubUrl = config.Bind("主菜单 MainMenu", "GitHub 地址 GithubUrl", "https://github.com/",
            "主菜单 GitHub 按钮打开的链接");
        CommunityUrl = config.Bind("主菜单 MainMenu", "交流群地址 CommunityUrl", "https://qm.qq.com/",
            "主菜单交流群按钮打开的链接");
    }
}
