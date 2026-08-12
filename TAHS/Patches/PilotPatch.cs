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
        public static bool Prefix(PlayerControl __instance, PlayerControl targetPlayer)
        {
            var role = CustomRoleManager.GetRole(__instance);
            if (role == null) return true;

            var host = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;

            switch (role)
            {
                // 准则：技能职业用原版变形按钮释放技能（不触发原版变形）
                case Pilot pilot:
                    if (host) pilot.TryStartDash();
                    return false;
                case Miner miner:
                    if (host) miner.PlaceMine();
                    return false;
                case Repenter repenter:
                    if (host && repenter.CanConvert) repenter.ServerConvert();
                    return false;
                case TAHS.Roles.Neutral.MoonRunner runner:
                    if (host && targetPlayer != null) runner.UseSkill(targetPlayer); // 菜单选中的目标即增益对象
                    return false;
            }

            // 其余假内鬼（带刀的非内鬼阵营职业）不允许原版变形
            if (CustomRoleManager.FakeImpostors.Contains(__instance.PlayerId))
                return false;
            return true;
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
