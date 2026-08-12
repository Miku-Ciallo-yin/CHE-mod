using TAHS.Roles;
using HarmonyLib;
using InnerNet;
using AmongUs.GameOptions;

namespace TAHS.Patches;

/// <summary>
/// 职业分配触发与对局结束重置，挂在 HudManager.Update 上检测。
///
/// 联机分配时机：等开场动画结束（本地玩家恢复可移动）再分配——
/// 此时原版身份（RpcSetRole 船员/内鬼）早已下发完毕，随后发给带刀职业的
/// RpcSetRole(Shapeshifter) 不会被原版分配覆盖，各玩家 PlayerControl 也已全部生成。
/// 注意：不要依赖 IntroCutscene.OnDestroy——该对象在部分版本不会在对局开始时销毁，
/// 回调可能直到对局结束才触发，导致分配整局不生效。
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class RoleAssignPatch
{
    public static void Postfix()
    {
        var client = AmongUsClient.Instance;
        if (client == null) return;

        // 回到大厅或主菜单才清空分配，等待下一局。
        // 注意不要用"!= Started"：客户端加载场景/播开场动画期间状态可能滞后，
        // 此时收到主机分配 RPC 后若被误判清空，职业就再也回不来（表现为被原版职业顶掉）
        if (client.GameState is InnerNetClient.GameStates.Joined or InnerNetClient.GameStates.NotJoined)
        {
            if (CustomRoleManager.Assigned)
                CustomRoleManager.Reset();
            return;
        }

        if (client.GameState != InnerNetClient.GameStates.Started) return;
        if (CustomRoleManager.Assigned) return;

        var local = PlayerControl.LocalPlayer;
        if (local == null) return;

        // 联机：开场动画期间玩家不可移动，等恢复可移动（动画结束）再分配
        if (client.NetworkMode == NetworkModes.OnlineGame && !local.moveable) return;

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
