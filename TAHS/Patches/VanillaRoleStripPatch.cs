using AmongUs.GameOptions;
using HarmonyLib;

namespace TAHS.Patches;

/// <summary>
/// 去除原版特殊职业（工程师/科学家/变形者等）：
/// 原版分配（SelectRoles）前把全部特殊职业的人数与概率清零，结束后再恢复——
/// 大厅设置界面里的数值不受影响。
/// 这样原版只分配普通船员/内鬼，模组职业的身份统一由模组发放
/// （否则佃农等会残留工程师通风口、科学家生命监测等多余 UI 与技能）。
/// </summary>
[HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
public static class VanillaRoleStripPatch
{
    /// <summary>需要清零的原版特殊职业（船员特殊 + 内鬼特殊 + 守护天使）</summary>
    private static readonly RoleTypes[] SpecialRoles =
    {
        RoleTypes.Engineer,
        RoleTypes.Scientist,
        RoleTypes.Noisemaker,
        RoleTypes.Detective,
        RoleTypes.Tracker,
        RoleTypes.Shapeshifter,
        RoleTypes.Phantom,
        RoleTypes.Viper,
        RoleTypes.GuardianAngel,
    };

    private static readonly System.Collections.Generic.Dictionary<RoleTypes, (int Num, int Chance)> _backup = new();

    public static void Prefix()
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
        var opts = GameOptionsManager.Instance?.CurrentGameOptions?.RoleOptions;
        if (opts == null) return;

        _backup.Clear();
        foreach (var roleType in SpecialRoles)
        {
            _backup[roleType] = (opts.GetNumPerGame(roleType), opts.GetChancePerGame(roleType));
            opts.SetRoleRate(roleType, 0, 0);
        }
    }

    public static void Postfix()
    {
        var opts = GameOptionsManager.Instance?.CurrentGameOptions?.RoleOptions;
        if (opts == null || _backup.Count == 0) return;

        foreach (var (roleType, (num, chance)) in _backup)
            opts.SetRoleRate(roleType, num, chance);
        _backup.Clear();
    }
}
