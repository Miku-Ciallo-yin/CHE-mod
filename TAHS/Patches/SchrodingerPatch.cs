using TAHS.Roles;
using TAHS.Roles.Crewmate;
using HarmonyLib;

namespace TAHS.Patches;

/// <summary>
/// 薛定谔的船员轮次判定（仅主机）：会议开始时，
/// 本轮未击杀的薛定谔的船员自杀（视为被自己的规则处死），存活的进入新一轮。
/// </summary>
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
public static class SchrodingerPatch
{
    public static void Postfix()
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;

        foreach (var role in CustomRoleManager.ActiveRoles)
        {
            if (role is not SchrodingerCrew schrodinger) continue;

            var player = schrodinger.Player;
            if (player == null || player.Data == null) continue;

            if (!player.Data.IsDead && !schrodinger.KilledThisRound)
            {
                TAHSPlugin.Log.LogInfo($"[TAHS] 薛定谔的船员 {player.Data.PlayerName} 本轮未猎杀，散会自杀");
                Modules.GameArchive.RecordKill($"{player.Data.PlayerName} 未猎杀自杀（薛定谔的船员）");
                player.RpcMurderPlayer(player, true);
            }
            schrodinger.NewRound();
        }
    }
}
