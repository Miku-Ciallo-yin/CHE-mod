using CHE.Roles;
using CHE.Roles.Addons;

namespace CHE.Modules;

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

    /// <summary>当前值（原始整数）</summary>
    public int Value;

    public float ScaledValue => Value * Scale;

    /// <summary>界面显示用的值文本</summary>
    public string DisplayValue => IsBool ? (Value == 1 ? "开" : "关") : Value.ToString();

    private CustomOption(byte id, byte roleId, string name, int defaultValue, int min, int max, int step, float scale, bool isBool = false)
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
    }

    public static CustomOption Register(byte id, byte roleId, string name, int defaultValue, int min, int max, int step, float scale, bool isBool = false)
    {
        var opt = new CustomOption(id, roleId, name, defaultValue, min, max, step, scale, isBool);
        All.Add(opt);
        return opt;
    }

    public static CustomOption? Get(byte id) => All.FirstOrDefault(o => o.Id == id);

    /// <summary>某职业的全部选项（含生成概率）</summary>
    public static IEnumerable<CustomOption> OfRole(byte roleId) => All.Where(o => o.RoleId == roleId);
}

/// <summary>
/// CHE 的全部游戏选项。默认值取自 BepInEx 配置（ModConfig），
/// 大厅中主机调整后经 RPC 覆盖到各端。
/// </summary>
public static class CustomOptions
{
    /// <summary>模组全局设置的组 ID（RoleId 0，职业 ID 从 1 开始）</summary>
    public const byte ModGroupId = 0;

    public static CustomOption ImpostorKnowEachOther { get; private set; } = null!;
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

        // 每个职业一项生成概率（ID 与 RoleRegistry 的职业 ID 相同）
        foreach (var (id, name, _) in CustomRoleManager.GetRegisteredRoles())
            CustomOption.Register(id, id, "生成概率%", 100, 0, 100, 10, 1f);

        // 每个附加职业一项生成概率（ID 与 AddonRegistry 的 ID 相同）
        foreach (var (id, name) in CustomRoleManager.GetRegisteredAddons())
            CustomOption.Register(id, id, "生成概率%", 100, 0, 100, 10, 1f);

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
}
