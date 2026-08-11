using TAHS.Roles;
using TAHS.Roles.Crewmate;
using HarmonyLib;

namespace TAHS.Patches;

/// <summary>
/// 内阁联动：
/// - 中立/内鬼击杀内阁后进入待转变名单（名牌提示"你击杀了内阁"）
/// - 待转变名单中的凶手再次击杀则移出名单
/// - 会议开始时：名单内存活的凶手转变为内阁（跟随船员胜利）
/// </summary>
public static class MinisterPatch
{
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
    public static class MurderTrack
    {
        public static void Postfix(PlayerControl __instance, PlayerControl target, MurderResultFlags resultFlags)
        {
            if (__instance == null || target == null) return;
            if (!resultFlags.HasFlag(MurderResultFlags.Succeeded)) return;

            if (CustomRoleManager.GetRole(target) is Minister)
            {
                if (CustomRoleManager.GetFaction(__instance) != Faction.Crewmate)
                {
                    Minister.PendingKillers.Add(__instance.PlayerId);
                    TAHSPlugin.Log.LogInfo($"[TAHS] {__instance.Data?.PlayerName} 击杀了内阁，进入待转变名单");
                }
            }
            else if (Minister.PendingKillers.Contains(__instance.PlayerId))
            {
                // 再次击杀：取消转变资格
                Minister.PendingKillers.Remove(__instance.PlayerId);
            }
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    public static class MeetingTransform
    {
        public static void Postfix()
        {
            if (Minister.PendingKillers.Count == 0) return;

            foreach (var playerId in Minister.PendingKillers.ToArray())
            {
                var player = PlayerControl.AllPlayerControls.ToArray()
                    .FirstOrDefault(p => p != null && p.PlayerId == playerId);
                if (player == null || player.Data == null || player.Data.IsDead) continue;

                CustomRoleManager.TransformToRole(player, new Minister());
                TAHSPlugin.Log.LogInfo($"[TAHS] {player.Data.PlayerName} 转变为内阁（将跟随船员胜利）");
                Modules.GameArchive.RecordTransition($"{player.Data.PlayerName} 转变为内阁（将跟随船员胜利）");
            }

            Minister.PendingKillers.Clear();
        }
    }
}
