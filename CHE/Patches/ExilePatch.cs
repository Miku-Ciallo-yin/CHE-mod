using CHE.Roles;
using HarmonyLib;

namespace CHE.Patches;

/// <summary>
/// 检测玩家被投票放逐，触发其职业的 <see cref="RoleBase.OnExile"/> 钩子。
/// 小丑被投出获胜就是在这里触发的。
/// </summary>
[HarmonyPatch(typeof(ExileController), nameof(ExileController.BeginForGameplay))]
public static class ExilePatch
{
    public static void Postfix(NetworkedPlayerInfo player, bool voteTie)
    {
        // 平票时 player 为 null，无人被放逐
        if (voteTie || player == null || player.Object == null) return;

        var role = CustomRoleManager.GetRole(player.Object);
        if (role == null) return;

        CHEPlugin.Log.LogInfo($"[CHE] {player.PlayerName} ({role.Name}) 被放逐");
        role.OnExile();
    }
}
