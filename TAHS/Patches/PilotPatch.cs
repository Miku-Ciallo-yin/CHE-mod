using TAHS.Modules;
using TAHS.Roles;
using TAHS.Roles.Impostor;
using HarmonyLib;

namespace TAHS.Patches;

/// <summary>
/// 中东机长配套补丁：
/// - Shift（变形）被劫持为冲刺技能，不触发原版变形
/// - 配置关闭正常击杀时拦截其手动击杀
/// - 击杀后应用配置的击杀冷却
/// </summary>
public static class PilotPatch
{
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Shapeshift))]
    public static class ShapeshiftHijack
    {
        public static bool Prefix(PlayerControl __instance)
        {
            if (CustomRoleManager.GetRole(__instance) is not Pilot pilot) return true;

            // 主机触发技能；各端都阻断原版变形
            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                pilot.TryStartDash();
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
    public static class KillControl
    {
        public static bool Prefix(PlayerControl __instance)
        {
            // 技能击杀走的是受害者自杀式 RPC（killer==target），不受影响；
            // 这里只拦中东机长的主动击杀
            if (CustomRoleManager.GetRole(__instance) is Pilot
                && CustomOptions.PilotCanNormalKill.Value != 1)
                return false;
            return true;
        }

        public static void Postfix(PlayerControl __instance, PlayerControl target, MurderResultFlags resultFlags)
        {
            if (__instance == null || __instance == target) return;
            if (!resultFlags.HasFlag(MurderResultFlags.Succeeded)) return;
            if (CustomRoleManager.GetRole(__instance) is not Pilot) return;

            // 击杀后应用配置的击杀冷却（替代全局 CD）
            __instance.SetKillTimer(CustomOptions.PilotKillCd.ScaledValue);
        }
    }
}
