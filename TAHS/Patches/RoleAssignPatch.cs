using TAHS.Roles;
using HarmonyLib;
using InnerNet;
using AmongUs.GameOptions;

namespace TAHS.Patches;

/// <summary>
/// 对局状态监控：离开对局后重置职业分配。
/// 分配时机见 <see cref="IntroAssignPatch"/>。
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class RoleAssignPatch
{
    public static void Postfix()
    {
        var client = AmongUsClient.Instance;
        if (client == null) return;

        // 不在对局中：若已分配过则清空，等待下一局
        if (client.GameState != InnerNetClient.GameStates.Started)
        {
            if (CustomRoleManager.Assigned)
                CustomRoleManager.Reset();
            return;
        }

        // 单机/练习模式没有开场动画，直接分配（联机走 IntroAssignPatch）
        if (client.NetworkMode == NetworkModes.OnlineGame) return;
        if (CustomRoleManager.Assigned) return;
        if (PlayerControl.LocalPlayer == null) return;

        var options = GameOptionsManager.Instance?.CurrentGameOptions;
        if (options != null && options.GameMode != GameModes.Normal
            && options.GameMode != GameModes.NormalFools) return;

        CustomRoleManager.AssignRoles();
    }
}

/// <summary>
/// 职业分配触发（参考 TONE 的 IntroCutsceneDestroyPatch）：
/// 等到开场动画结束（IntroCutscene.OnDestroy）再分配——此时原版身份
/// （RpcSetRole 船员/内鬼）已下发完毕，随后发给带刀职业的 RpcSetRole(Shapeshifter)
/// 不会被原版分配覆盖；各玩家 PlayerControl 也已全部生成。
/// 之前在 GameState=Started 时立即分配，会与原版身份分配竞争，
/// 导致无模组端（纯靠原版 RPC 驱动）拿不到职业按钮。
/// </summary>
[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.OnDestroy))]
public static class IntroAssignPatch
{
    public static void Postfix()
    {
        var client = AmongUsClient.Instance;
        if (client == null || client.GameState != InnerNetClient.GameStates.Started) return;
        if (CustomRoleManager.Assigned) return;
        if (PlayerControl.LocalPlayer == null) return;

        // 职业系统只在经典模式启用（躲猫猫等模式不分配职业）
        var options = GameOptionsManager.Instance?.CurrentGameOptions;
        if (options != null && options.GameMode != GameModes.Normal
            && options.GameMode != GameModes.NormalFools) return;

        // 联机时只有主机分配（随后 RPC 广播）；单机 / 离线局（无其他客户端）直接本地分配
        if (!client.AmHost && client.allClients.Count > 0) return;

        TAHSPlugin.Log.LogInfo("[TAHS] 开场动画结束，开始分配职业");
        CustomRoleManager.AssignRoles();
    }
}
