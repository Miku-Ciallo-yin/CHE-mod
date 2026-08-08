using CHE.Roles;
using HarmonyLib;

namespace CHE.Patches;

/// <summary>
/// 职业技能驱动：本机玩家的职业每帧收到 OnUpdate。
/// （佃农的接近抢夺、击杀按键等逻辑在 Farmer.OnUpdate 中）
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
public static class RoleUpdatePatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (!__instance.AmOwner) return;
        if (AmongUsClient.Instance == null ||
            AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started) return;

        CustomRoleManager.GetRole(__instance)?.OnUpdate();
    }
}
