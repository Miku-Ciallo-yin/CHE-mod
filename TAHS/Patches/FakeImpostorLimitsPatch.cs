using TAHS.Roles;
using HarmonyLib;

namespace TAHS.Patches;

/// <summary>
/// 假内鬼（带刀/技能的非内鬼阵营职业）的原版技能限制：
/// - 通风口：RpcEnterVent 拦截，不允许进通风口
/// - 破坏/通风口按钮：模组端本地隐藏（无模组端仍会显示，属固有降级）
/// - Shift 变形按钮：非技能职业隐藏（技能职业的 Shift 已被劫持占用）
/// </summary>
public static class FakeImpostorLimitsPatch
{
    /// <summary>本机玩家是否是假内鬼（带刀/技能的非内鬼阵营）</summary>
    private static bool LocalIsFakeImpostor(out RoleBase? role)
    {
        role = null;
        var local = PlayerControl.LocalPlayer;
        if (local == null) return false;
        if (!CustomRoleManager.FakeImpostors.Contains(local.PlayerId)) return false;
        role = CustomRoleManager.GetRole(local);
        return role == null || role.Faction != Faction.Impostor;
    }

    [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.RpcEnterVent))]
    public static class VentBlock
    {
        public static bool Prefix(PlayerPhysics __instance)
        {
            var player = __instance.myPlayer;
            if (player == null) return true;
            if (!CustomRoleManager.FakeImpostors.Contains(player.PlayerId)) return true;

            // 内鬼阵营（中东机长等）允许通风口；其余假内鬼拦截
            return CustomRoleManager.GetFaction(player) == Faction.Impostor;
        }
    }

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    public static class HideButtons
    {
        public static void Postfix(HudManager __instance)
        {
            if (!LocalIsFakeImpostor(out var role)) return;

            if (__instance.SabotageButton != null)
                __instance.SabotageButton.gameObject.SetActive(false);
            if (__instance.ImpostorVentButton != null)
                __instance.ImpostorVentButton.gameObject.SetActive(false);
            if (__instance.AbilityButton != null && (role == null || !role.UsesShapeshiftButton))
                __instance.AbilityButton.gameObject.SetActive(false);
        }
    }
}
