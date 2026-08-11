using TAHS.Modules;
using TAHS.Roles;
using TAHS.Roles.Crewmate;
using HarmonyLib;
using UnityEngine;

namespace TAHS.Patches;

/// <summary>
/// 使徒相关补丁：
/// - 完成任务：主机赐予随机船员一个良性附加职业并广播
/// - 会议中：使徒可见死者阵营（红内鬼/灰中立/青船员）与死因
/// </summary>
public static class ApostlePatch
{
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CompleteTask))]
    public static class TaskGrant
    {
        public static void Postfix(PlayerControl __instance)
        {
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
            if (CustomRoleManager.GetRole(__instance) is not Apostle) return;

            TAHSPlugin.Log.LogInfo($"[TAHS] 使徒 {__instance.Data?.PlayerName} 完成任务，赐予良性附加职业");
            CustomRoleManager.GrantRandomBenignAddon();
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
    public static class MeetingDeathInfo
    {
        public static void Postfix(MeetingHud __instance)
        {
            if (!Apostle.LocalIsApostle()) return;

            foreach (var pva in __instance.playerStates)
            {
                var target = PlayerControl.AllPlayerControls.ToArray()
                    .FirstOrDefault(p => p != null && p.PlayerId == pva.TargetPlayerId);
                if (target == null || target.Data == null || !target.Data.IsDead) continue;
                if (pva.NameText == null) continue;

                var faction = CustomRoleManager.GetFaction(target);
                var (color, factionName) = faction switch
                {
                    Faction.Impostor => (new Color(1f, 0.3f, 0.3f), "内鬼"),
                    Faction.Neutral => (new Color(0.6f, 0.6f, 0.6f), "中立"),
                    _ => (new Color(0.3f, 0.9f, 1f), "船员"),
                };

                var cause = DeathTracker.GetCause(target.PlayerId) ?? "击杀";
                pva.NameText.color = color;
                pva.NameText.text = $"{target.Data.PlayerName}\n<size=70%>{factionName}·{cause}</size>";
            }
        }
    }
}
