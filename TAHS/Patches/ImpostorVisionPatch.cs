using TAHS.Modules;
using TAHS.Roles;
using HarmonyLib;
using UnityEngine;

namespace TAHS.Patches;

/// <summary>
/// 内鬼不互认（模组设置 -> 内鬼互认 = 关 时生效）：
/// 对局内和会议中，内鬼看其他内鬼的名字都是白色而不是红色。
/// 另包含叛徒附加职业的红名互认（模组端本地即时表现）：
/// - 与内鬼互认：叛徒看内鬼红名、内鬼看叛徒红名；
/// - 与其他叛徒互认：叛徒之间互见红名。
/// 无模组客户端的红名由主机经 PrivateTag 定向改名下发（见 TraitorNameColors）。
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

    /// <summary>叛徒视角的红名规则：viewer 看 target 是否应显示红名</summary>
    private static bool TraitorSeesRed(PlayerControl viewer, PlayerControl target)
    {
        if (viewer == null || target == null || viewer == target) return false;
        if (viewer.Data == null || target.Data == null) return false;

        // 追杀者等临时内鬼身份的红名始终隐藏，叛徒逻辑不覆盖
        if (CustomRoleManager.FakeImpostors.Contains(target.PlayerId)) return false;
        // 双方都是内鬼身份时由内鬼互认开关决定，叛徒逻辑不插手
        if (viewer.Data.Role != null && viewer.Data.Role.IsImpostor
            && target.Data.Role != null && target.Data.Role.IsImpostor) return false;

        var viewerIsTraitor = Roles.Addons.Traitor.IsTraitor(viewer);
        var targetIsTraitor = Roles.Addons.Traitor.IsTraitor(target);
        if (!viewerIsTraitor && !targetIsTraitor) return false;

        var knowImpostors = CustomOptions.TraitorKnowImpostors.Value == 1;
        var knowEachOther = CustomOptions.TraitorKnowEachOther.Value == 1;

        // 叛徒之间互认
        if (viewerIsTraitor && targetIsTraitor) return knowEachOther;

        // 叛徒与内鬼互认（按自定义职业阵营判定，追杀者等临时内鬼身份不算）
        if (knowImpostors)
        {
            var viewerFaction = CustomRoleManager.GetFaction(viewer);
            var targetFaction = CustomRoleManager.GetFaction(target);
            if (viewerIsTraitor && targetFaction == Faction.Impostor) return true;
            if (targetIsTraitor && viewerFaction == Faction.Impostor) return true;
        }
        return false;
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
                && CustomRoleManager.FakeImpostors.Contains(__instance.PlayerId))
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

    /// <summary>对局内：叛徒互认红名（在内鬼标红/隐藏逻辑之后覆盖）</summary>
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class TraitorNamePatch
    {
        public static void Postfix(PlayerControl __instance)
        {
            var local = PlayerControl.LocalPlayer;
            if (local == null) return;
            if (!TraitorSeesRed(local, __instance)) return;

            var nameText = __instance.cosmetics.nameText;
            if (nameText != null)
                nameText.color = Palette.ImpostorRed;
        }
    }

    /// <summary>会议中：覆盖玩家按钮上的红色名字；追杀者红名同样隐藏</summary>
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
    public static class MeetingNamePatch
    {
        public static void Postfix(MeetingHud __instance)
        {
            var local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null || local.Data.Role == null) return;

            var localIsImpostor = local.Data.Role.IsImpostor;
            var localIsTraitor = Roles.Addons.Traitor.IsTraitor(local);
            if (!localIsImpostor && !localIsTraitor) return;

            foreach (var pva in __instance.playerStates)
            {
                var target = PlayerControl.AllPlayerControls.ToArray()
                    .FirstOrDefault(p => p != null && p.PlayerId == pva.TargetPlayerId);
                if (target == null) continue;

                // 追杀者红名隐藏（不受内鬼互认开关影响；仅内鬼视角原本可见红名）
                if (localIsImpostor
                    && CustomRoleManager.FakeImpostors.Contains(target.PlayerId)
                    && target.Data != null && target.Data.Role != null && target.Data.Role.IsImpostor)
                {
                    if (pva.NameText != null) pva.NameText.color = Color.white;
                    continue;
                }

                // 叛徒互认红名
                if (TraitorSeesRed(local, target))
                {
                    if (pva.NameText != null) pva.NameText.color = Palette.ImpostorRed;
                    continue;
                }

                if (!localIsImpostor) continue;
                if (!Enabled) continue;
                if (!BothImpostor(local, target)) continue;

                if (pva.NameText != null)
                    pva.NameText.color = Color.white;
            }
        }
    }
}
