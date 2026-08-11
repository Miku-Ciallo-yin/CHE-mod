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

    /// <summary>对局内：覆盖原版给内鬼队友标红的颜色；追杀者（临时变形者）的红名对内鬼隐藏</summary>
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class PlayerNamePatch
    {
        public static void Postfix(PlayerControl __instance)
        {
            var local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null || local.Data.Role == null) return;
            if (__instance.Data == null || __instance.Data.Role == null) return;

            // 追杀者（月跑入机链接中临时变成变形者的玩家）的红名对内鬼隐藏
            if (local.Data.Role.IsImpostor
                && __instance.Data.Role.IsImpostor
                && TAHS.Roles.Neutral.MoonRunner.IsProtectedHunter(__instance))
            {
                var t = __instance.cosmetics.nameText;
                if (t != null) t.color = Color.white;
                return;
            }

            if (!Enabled) return;
            if (!BothImpostor(local, __instance)) return;

            var nameText = __instance.cosmetics.nameText;
            if (nameText != null)
                nameText.color = Color.white;
        }
    }

    /// <summary>会议中：覆盖玩家按钮上的红色名字；追杀者红名同样隐藏</summary>
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
    public static class MeetingNamePatch
    {
        public static void Postfix(MeetingHud __instance)
        {
            var local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null || local.Data.Role == null
                || !local.Data.Role.IsImpostor) return;

            foreach (var pva in __instance.playerStates)
            {
                var target = PlayerControl.AllPlayerControls.ToArray()
                    .FirstOrDefault(p => p != null && p.PlayerId == pva.TargetPlayerId);
                if (target == null) continue;

                // 追杀者红名隐藏（不受内鬼互认开关影响）
                if (TAHS.Roles.Neutral.MoonRunner.IsProtectedHunter(target)
                    && target.Data != null && target.Data.Role != null && target.Data.Role.IsImpostor)
                {
                    if (pva.NameText != null) pva.NameText.color = Color.white;
                    continue;
                }

                if (!Enabled) continue;
                if (!BothImpostor(local, target)) continue;

                if (pva.NameText != null)
                    pva.NameText.color = Color.white;
            }
        }
    }
}
