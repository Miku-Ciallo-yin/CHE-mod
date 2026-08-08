using CHE.Roles;
using HarmonyLib;

namespace CHE.Patches;

/// <summary>
/// 结算画面覆盖：当存在自定义胜利者（如小丑被投出获胜）时，
/// 替换官方结算为只显示该玩家和自定义胜利文字。
/// </summary>
[HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.SetEverythingUp))]
public static class EndGamePatch
{
    /// <summary>在原版搭建结算画面前替换胜利者列表</summary>
    public static void Prefix()
    {
        var winner = CustomRoleManager.CustomWinner;
        if (winner == null || winner.Data == null) return;

        EndGameResult.CachedWinners.Clear();
        EndGameResult.CachedWinners.Add(new CachedPlayerData(winner.Data));
    }

    /// <summary>替换胜利文字和颜色</summary>
    public static void Postfix(EndGameManager __instance)
    {
        var winner = CustomRoleManager.CustomWinner;
        if (winner == null) return;

        var role = CustomRoleManager.GetRole(winner);
        __instance.WinText.text = $"{role?.Name ?? winner.Data?.PlayerName} 获胜！";
        if (role != null)
            __instance.WinText.color = role.Color;
    }
}
