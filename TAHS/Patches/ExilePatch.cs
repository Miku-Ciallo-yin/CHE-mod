using TAHS.Roles;
using HarmonyLib;

namespace TAHS.Patches;

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

        Modules.DeathTracker.RecordExile(player.Object);
        ConverterPatch.ApostleTags.TagForApostles(player.Object); // 使徒私有标签

        // 算命师预言结算（放逐也算死亡，仅主机）
        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            Roles.Impostor.FortuneTeller.OnDeath(player.Object);

        var role = CustomRoleManager.GetRole(player.Object);
        if (role == null) return;

        TAHSPlugin.Log.LogInfo($"[TAHS] {player.PlayerName} ({role.Name}) 被放逐");
        role.OnExile();
    }
}
