using TAHS.Roles;
using HarmonyLib;

namespace TAHS.Patches;

/// <summary>
/// 击杀结算钩子：通知攻击者职业（佃农误杀船员转中立等）。
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
public static class MurderPatch
{
    public static void Postfix(PlayerControl __instance, PlayerControl target, MurderResultFlags resultFlags)
    {
        if (target == null) return;
        if (!resultFlags.HasFlag(MurderResultFlags.Succeeded)) return;

        Modules.DeathTracker.Record(__instance, target);
        Modules.GameArchive.RecordKill(KillText(__instance, target));
        ConverterPatch.ApostleTags.TagForApostles(target); // 使徒私有标签（含无模组端使徒）
        CustomRoleManager.GetRole(__instance)?.OnMurder(target);
    }

    private static string KillText(PlayerControl killer, PlayerControl victim)
    {
        string Info(PlayerControl p)
        {
            if (p == null || p.Data == null) return "?";
            var role = CustomRoleManager.GetRole(p);
            var roleName = role != null
                ? role.Name
                : (p.Data.Role != null && p.Data.Role.IsImpostor ? "内鬼" : "船员");
            return $"{p.Data.PlayerName}（{roleName}）";
        }
        return $"{Info(killer)} 击杀了 {Info(victim)}";
    }
}
