using TAHS.Roles;
using TAHS.Roles.Crewmate;
using TAHS.Roles.Impostor;
using TAHS.Roles.Neutral;
using HarmonyLib;

namespace TAHS.Patches;

/// <summary>
/// 击杀规则统一关口（原版击杀按钮路径）——挂在主机 CheckMurder 上：
/// 本版本击杀流程为"凶手客户端 CmdCheckMurder → 主机 CheckMurder → RpcMurderPlayer 广播"，
/// 在这里拦截可以在广播发出前阻断，无模组端与主机/模组端看到的结果一致。
/// （此前规则挂在 MurderPlayer：只能挡主机本地执行，广播已抵达无模组端，造成双向不同步。）
///
/// - 首刀保护（目标侧）：保护对象不可被首刀
/// - 法军（目标侧）：被内鬼出刀时不死亡，缴械成为叛徒
/// - 佃农：未解锁或冷却中不可杀
/// - 懦弱者：转变后或冷却中不可杀
/// - 美警：击杀船员走 ExecuteCrewKill（主机结算：船员按配置死亡 + 美警自杀抵命）
/// - 忏悔者：转变后不可杀
/// - 月跑入机：不能手动击杀（技能即 Shift）
/// - 中东机长：配置关闭正常击杀时不可杀
/// 自杀式处决（killer == target，职业技能/赌怪路径）不在此拦截。
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckMurder))]
public static class CheckMurderPatch
{
    public static bool Prefix(PlayerControl __instance, PlayerControl target)
    {
        if (__instance == null || target == null || __instance == target) return true;

        var host = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;

        // 摄梦免疫（目标侧）：抵消一次任意击杀
        if (DreamEater.IsImmune(target))
        {
            if (host)
            {
                DreamEater.TryConsumeImmunity(target);
                Modules.ChatHelper.ShowPrivate(__instance, "[TAHS] 目标处于摄梦保护中，本次击杀被抵消");
            }
            return false;
        }

        // 首刀保护（目标侧）：上一局第一个死亡的玩家不能被首刀
        if (Modules.FirstKillProtection.IsProtected(target))
        {
            if (host)
                Modules.ChatHelper.ShowPrivate(__instance, "[TAHS] 首刀保护：该玩家上一局第一个死亡，本局不能被首刀");
            return false;
        }

        // 法军（目标侧）：被内鬼击杀时不死亡，缴械成为叛徒（已缴械则正常死亡）
        if (CustomRoleManager.GetRole(target) is FrenchArmy frenchArmy
            && !frenchArmy.Disarmed
            && CustomRoleManager.GetFaction(__instance) == Faction.Impostor)
        {
            if (host) frenchArmy.OnAttackedByImpostor(__instance);
            return false;
        }

        // 月跑入机（目标侧）：技能期间无敌；追杀者在后者死亡前无法被击杀
        if (MoonRunner.HasActiveBuffAnywhere(target)) return false;
        if (MoonRunner.IsProtectedHunter(target)) return false;
        // 追杀者（出刀侧）：只能击杀后者
        if (MoonRunner.HunterPrey.TryGetValue(__instance.PlayerId, out var preyId)
            && target.PlayerId != preyId)
            return false;

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
                    // 主机结算（自杀/按配置双杀），各端经主机 RPC 一致呈现
                    if (host) cop.ExecuteCrewKill(target);
                    return false;
                }
                return true;

            case Repenter repenter:
                return !repenter.Converted;

            case MoonRunner:
                return false;

            case Pilot:
                // 配置关闭正常击杀时不可主动出刀
                return Modules.CustomOptions.PilotCanNormalKill.Value == 1;

            case FrenchArmy army:
                return !army.Disarmed && army.KillTimer <= 0f;
        }
        return true;
    }
}
