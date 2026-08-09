using BepInEx.Configuration;

namespace CHE.Modules;

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
    }
}
