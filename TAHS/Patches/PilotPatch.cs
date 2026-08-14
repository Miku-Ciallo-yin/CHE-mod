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
                case TAHS.Roles.Crewmate.FengshuiMaster fengshui:
                    if (host && targetPlayer != null) fengshui.KillByButton(targetPlayer); // 菜单选中的目标即点杀对象
                    return false;
            }

            // 其余假内鬼（带刀的非内鬼阵营职业）不允许原版变形
            if (CustomRoleManager.FakeImpostors.Contains(__instance.PlayerId))
                return false;
            return true;
        }
    }

    /// <summary>
    /// 技能直发按钮（手机端/模组端便捷操作，参考 TONE）：
    /// 忏悔者/中东机长/埋雷兵点变形按钮直接放技能，不打开选人菜单、不消耗变形次数。
    /// 走原版 RpcShapeshift（自身为目标），主机按 ShapeshiftHijack 劫持执行——
    /// 无模组端打开菜单选人后殊途同归（同样由主机劫持）。
    /// 月跑入机需要选人目标，保留菜单流程。
    /// </summary>
    [HarmonyPatch(typeof(ShapeshifterRole), nameof(ShapeshifterRole.UseAbility))]
    public static class DirectSkillButton
    {
        public static bool Prefix()
        {
            var local = PlayerControl.LocalPlayer;
            if (local == null) return true;

            var trigger = CustomRoleManager.GetRole(local) switch
            {
                Repenter repenter => repenter.CanConvert,
                Pilot pilot => pilot.SkillTimer <= 0f && !pilot.Dashing,
                Miner => true, // 冷却/数量校验在 PlaceMine 内
                _ => false,
            };
            if (!trigger) return true;

            local.RpcShapeshift(local, false); // 主机劫持为技能
            return false; // 不打开选人菜单，不消耗次数
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
    public static class KillControl
    {
        // 主动击杀的拦截在 CheckMurderPatch（主机验证关口，广播前阻断）

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
