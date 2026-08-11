using TAHS.Modules;
using HarmonyLib;
using UnityEngine;

namespace TAHS.Patches;

/// <summary>
/// 内鬼不互认（模组设置 -> 内鬼互认 = 关 时生效）：
/// 对局内和会议中，内鬼看其他内鬼的名字都是白色而不是红色。
/// </summary>
public static class ImpostorVisionPatch
{
    private static bool Enabled =>
        CustomOptions.ImpostorKnowEachOther.Value == 0;

    private static bool BothImpostor(PlayerControl local, PlayerControl other)
    {
        return local != null && other != null && local != other
               && local.Data != null && other.Data != null
               && local.Data.Role != null && other.Data.Role != null
               && local.Data.Role.IsImpostor && other.Data.Role.IsImpostor;
    }

    /// <summary>对局内：覆盖原版给内鬼队友标红的颜色</summary>
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class PlayerNamePatch
    {
        public static void Postfix(PlayerControl __instance)
        {
            if (!Enabled) return;
            if (!BothImpostor(PlayerControl.LocalPlayer, __instance)) return;

            var nameText = __instance.cosmetics.nameText;
            if (nameText != null)
                nameText.color = Color.white;
        }
    }

    /// <summary>会议中：覆盖玩家按钮上的红色名字</summary>
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
    public static class MeetingNamePatch
    {
        public static void Postfix(MeetingHud __instance)
        {
            if (!Enabled) return;

            var local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null || local.Data.Role == null
                || !local.Data.Role.IsImpostor) return;

            foreach (var pva in __instance.playerStates)
            {
                var target = PlayerControl.AllPlayerControls.ToArray()
                    .FirstOrDefault(p => p != null && p.PlayerId == pva.TargetPlayerId);
                if (!BothImpostor(local, target)) continue;

                if (pva.NameText != null)
                    pva.NameText.color = Color.white;
            }
        }
    }
}
