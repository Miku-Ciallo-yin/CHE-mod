using CHE.Modules;
using CHE.Roles;
using HarmonyLib;

namespace CHE.Patches;

/// <summary>
/// 名牌显示：
/// - 所有玩家名字前显示 [id] 前缀（大厅和对局内，仅模组端可见）
/// - 本机玩家名字下方显示职业名与状态行
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
public static class NamePatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (__instance.Data == null) return;

        var nameText = __instance.cosmetics.nameText;
        if (nameText == null) return;

        var name = __instance.Data.PlayerName;
        var id = PlayerIdManager.GetId(__instance);
        var prefix = id.HasValue ? $"<color=#4FC3F7>[{id.Value}]</color>" : string.Empty;

        // 本机玩家：职业名 + 状态行
        if (__instance.AmOwner && CustomRoleManager.GetRole(__instance) is { } role)
        {
            var status = role.GetStatusText();
            var statusLine = string.IsNullOrEmpty(status)
                ? string.Empty
                : $"\n<color=#FFFFFF><size=60%>{status}</size></color>";

            nameText.text =
                $"{prefix}{name}\n" +
                $"{role.ColorTag}<size=75%>{role.Name} / {role.NameEn}</size></color>" +
                statusLine;
            return;
        }

        nameText.text = prefix + name;
    }
}
