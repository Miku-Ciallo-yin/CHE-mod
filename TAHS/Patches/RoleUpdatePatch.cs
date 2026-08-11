using TAHS.Roles;
using HarmonyLib;
using InnerNet;

namespace TAHS.Patches;

/// <summary>
/// 职业技能驱动（Host Only 架构）：
/// - 主机：对局中驱动所有玩家的职业逻辑（佃农抢夺等），无模组客户端也能生效
/// - 非主机模组端：仅驱动自身输入（如佃农按 Q 请求击杀，主机验证后执行）
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
public static class RoleUpdatePatch
{
    public static void Postfix(PlayerControl __instance)
    {
        var client = AmongUsClient.Instance;
        if (client == null || client.GameState != InnerNetClient.GameStates.Started) return;

        var role = CustomRoleManager.GetRole(__instance);
        if (role == null) return;

        if (client.AmHost)
        {
            role.OnUpdate();
            return;
        }

        if (__instance.AmOwner)
            role.OnClientUpdate();
    }
}
