using CHE.Roles;
using HarmonyLib;
using InnerNet;

namespace CHE.Patches;

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

        CustomRoleManager.AssignRoles();
    }
}
