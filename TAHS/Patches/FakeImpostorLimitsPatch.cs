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

    /// <summary>
    /// 破坏拦截（参考 TONE 的 MessageReaderUpdateSystemPatch）：
    /// 破坏走 ShipStatus.RpcUpdateSystem → 主机 UpdateSystem(SystemTypes.Sabotage)，
    /// 主机在 RPC 处理处拦截，破坏不会应用也不会转发，各端一致（无模组端点破坏无效）。
    /// </summary>
    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.UpdateSystem),
        new[] { typeof(SystemTypes), typeof(PlayerControl), typeof(Hazel.MessageReader) })]
    public static class SabotageBlock
    {
        public static bool Prefix(SystemTypes systemType, PlayerControl player)
        {
            if (systemType != SystemTypes.Sabotage) return true;
            if (player == null || player.Data == null) return true;

            // 仅内鬼阵营可破坏（带刀中立/假内鬼不可）
            if (CustomRoleManager.GetFaction(player) == Faction.Impostor) return true;

            TAHSPlugin.Log.LogInfo($"[TAHS] 拦截非内鬼破坏：{player.Data.PlayerName}");
            return false;
        }
    }
}
