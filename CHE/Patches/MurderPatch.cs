using CHE.Roles;
using HarmonyLib;

namespace CHE.Patches;

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

        CustomRoleManager.GetRole(__instance)?.OnMurder(target);
    }
}
