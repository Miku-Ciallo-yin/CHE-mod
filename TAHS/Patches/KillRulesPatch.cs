using TAHS.Roles;
using TAHS.Roles.Crewmate;
using TAHS.Roles.Impostor;
using TAHS.Roles.Neutral;
using HarmonyLib;

namespace TAHS.Patches;

/// <summary>
/// 击杀规则统一拦截（原版击杀按钮路径）：
/// - 佃农：未解锁或冷却中不可杀
/// - 懦弱者：转变后（失去击杀能力）或冷却中不可杀
/// - 美警：击杀船员走 ExecuteCrewKill（船员按配置死亡 + 美警自杀抵命）
/// - 忏悔者：转变后不可杀
/// - 月跑入机：不能手动击杀（技能即 Shift）
/// 自杀式处决（killer == target）不在此拦截。
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
public static class KillRulesPatch
{
    public static bool Prefix(PlayerControl __instance, PlayerControl target)
    {
        if (__instance == null || target == null || __instance == target) return true;

        switch (CustomRoleManager.GetRole(__instance))
        {
            case Farmer farmer:
                return farmer.HasKillAbility && farmer.KillTimer <= 0f;

            case Coward coward:
                return coward.HasKillAbility && coward.KillTimer <= 0f;

            case Cop cop:
                if (cop.KillTimer > 0f) return false;
                if (!cop.Converted && CustomRoleManager.GetFaction(target) == Faction.Crewmate)
                {
                    cop.ExecuteCrewKill(target);
                    return false;
                }
                return true;

            case Repenter repenter:
                return !repenter.Converted;

            case MoonRunner:
                return false;
        }
        return true;
    }
}
