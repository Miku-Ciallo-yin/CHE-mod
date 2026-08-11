using TAHS.Modules;
using TAHS.Roles;
using HarmonyLib;
using InnerNet;

namespace TAHS.Patches;

/// <summary>
/// 反作弊与黑名单（参考 TONE）：
/// - 黑名单：好友代码在 BanList.txt 中的玩家进房即踢出封禁
/// - 作弊检测：非内鬼且无合法带刀职业的玩家触发击杀 → 按配置处理
/// - 处理方式（模组设置）：警告 / 踢出 / 封禁 / 加入黑名单
/// 全部判定与执行仅在主机进行（Host Only）。
/// </summary>
public static class AntiCheatPatch
{
    /// <summary>黑名单拦截：进房即踢</summary>
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerJoined)), HarmonyPostfix]
    public static void OnPlayerJoinedPostfix(ClientData client)
    {
        if (client == null) return;
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
        if (!BanManager.IsBanned(client.FriendCode)) return;

        TAHSPlugin.Log.LogInfo($"[TAHS] 黑名单玩家 {client.PlayerName}（{client.FriendCode}）进房，已踢出封禁");
        AmongUsClient.Instance.KickPlayer(client.Id, true);
    }

    /// <summary>击杀检测：非内鬼且无合法带刀职业的玩家击杀 = 作弊</summary>
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer)), HarmonyPostfix]
    public static void MurderPlayerPostfix(PlayerControl __instance, PlayerControl target, MurderResultFlags resultFlags)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
        if (__instance == null || target == null) return;
        if (!resultFlags.HasFlag(MurderResultFlags.Succeeded)) return;
        if (__instance == target) return; // 自杀式击杀（赌怪/职业技能处决）放行

        if (!IsLegitKiller(__instance))
            HandleCheat(__instance, "非法击杀");
    }

    /// <summary>合法击杀者：内鬼，或当前拥有击杀能力的模组职业</summary>
    private static bool IsLegitKiller(PlayerControl killer)
    {
        if (killer.Data != null && killer.Data.Role != null && killer.Data.Role.IsImpostor)
            return true;

        return CustomRoleManager.GetRole(killer) switch
        {
            Roles.Crewmate.Farmer farmer => farmer.HasKillAbility,
            Roles.Neutral.Coward coward => coward.HasKillAbility,
            Roles.Crewmate.Cop => true,
            Roles.Impostor.Repenter repenter => !repenter.Converted,
            _ => false,
        };
    }

    /// <summary>按配置处理作弊行为</summary>
    private static void HandleCheat(PlayerControl player, string reason)
    {
        var name = player.Data?.PlayerName ?? "?";
        var code = player.Data?.FriendCode;
        TAHSPlugin.Log.LogWarning($"[TAHS] 反作弊：{name}（{code}）{reason}");

        switch (CustomOptions.CheatAction.Value)
        {
            case 0: // 警告
                PlayerControl.LocalPlayer?.RpcSendChat($"【反作弊】{name} 检测到作弊行为：{reason}");
                break;
            case 1: // 踢出
                AmongUsClient.Instance.KickPlayer(player.OwnerId, false);
                break;
            case 2: // 封禁
                AmongUsClient.Instance.KickPlayer(player.OwnerId, true);
                break;
            case 3: // 加入黑名单
                BanManager.Add(code);
                AmongUsClient.Instance.KickPlayer(player.OwnerId, true);
                break;
        }
    }
}
