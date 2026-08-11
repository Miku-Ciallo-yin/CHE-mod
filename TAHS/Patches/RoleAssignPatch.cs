using TAHS.Roles;
using HarmonyLib;
using InnerNet;
using AmongUs.GameOptions;

namespace TAHS.Patches;

/// <summary>
/// 对局开始时分配职业；对局结束后重置。
/// 挂在 HudManager.Update 上检测游戏状态，避免依赖易随版本变动的协程补丁。
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class RoleAssignPatch
{
    public static void Postfix()
    {
        var client = AmongUsClient.Instance;
        if (client == null) return;

        if (client.GameState != InnerNetClient.GameStates.Started)
        {
            // 不在对局中：若已分配过则清空，等待下一局
            if (CustomRoleManager.Assigned)
                CustomRoleManager.Reset();
            return;
        }

        if (CustomRoleManager.Assigned) return;
        if (PlayerControl.LocalPlayer == null) return;

        // 职业系统只在经典模式启用（躲猫猫等模式不分配职业）
        var options = GameOptionsManager.Instance?.CurrentGameOptions;
        if (options != null && options.GameMode != GameModes.Normal
            && options.GameMode != GameModes.NormalFools) return;

        // 联机时只有主机分配（随后 RPC 广播）；单机 / 离线局（无其他客户端）直接本地分配
        if (!client.AmHost && client.allClients.Count > 0) return;

        CustomRoleManager.AssignRoles();
    }
}
