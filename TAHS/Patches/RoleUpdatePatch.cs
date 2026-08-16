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

        // 非内鬼带刀职业的视野规则（按配置收回内鬼视野；每帧应用自愈，等待身份同步到位）
        if (__instance.AmOwner)
            CustomRoleManager.ApplyVisionRule(__instance);

        if (client.AmHost)
        {
            // 会议/放逐动画期间暂停职业逻辑：
            // 防止贴近判定在会议桌触发（美警误杀、内阁限时自杀、佃农误抢等）
            // 会议中杀人会导致会议状态损坏（会议结束黑屏/卡死）
            if (MeetingHud.Instance != null || ExileController.Instance != null) return;
            role.OnUpdate();
            return;
        }

        if (__instance.AmOwner)
            role.OnClientUpdate();
    }
}
