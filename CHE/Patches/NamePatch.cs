using CHE.Roles;
using HarmonyLib;

namespace CHE.Patches;

/// <summary>
/// 在自己名字下方显示职业名和阵营（仅本机可见）。
/// TODO: 参考 TONE 增加同阵营互见、会议中显示、Tag 系统。
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
public static class NamePatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (!__instance.AmOwner) return;
        if (__instance.Data == null) return;

        var role = CustomRoleManager.GetRole(__instance);
        if (role == null) return;

        var nameText = __instance.cosmetics.nameText;
        if (nameText == null) return;

        var status = role.GetStatusText();
        var statusLine = string.IsNullOrEmpty(status)
            ? string.Empty
            : $"\n<color=#FFFFFF><size=60%>{status}</size></color>";

        nameText.text =
            $"{__instance.Data.PlayerName}\n" +
            $"{role.ColorTag}<size=75%>{role.Name} / {role.NameEn}</size></color>" +
            statusLine;
    }
}
