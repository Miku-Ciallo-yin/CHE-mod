using TAHS.Roles;
using HarmonyLib;

namespace TAHS.Patches;

/// <summary>
/// 结算画面覆盖：当存在自定义胜利者（如小丑被投出获胜、懦弱者链接共同胜利）时，
/// 替换官方结算为只显示这些玩家和自定义胜利文字。
/// </summary>
[HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.SetEverythingUp))]
public static class EndGamePatch
{
    /// <summary>在原版搭建结算画面前替换胜利者列表</summary>
    public static void Prefix()
    {
        var winners = CustomRoleManager.CustomWinners;
        if (winners.Count == 0) return;

        EndGameResult.CachedWinners.Clear();
        foreach (var winner in winners)
            if (winner != null && winner.Data != null)
                EndGameResult.CachedWinners.Add(new CachedPlayerData(winner.Data));
    }

    /// <summary>替换胜利文字和颜色</summary>
    public static void Postfix(EndGameManager __instance)
    {
        var winners = CustomRoleManager.CustomWinners;
        if (winners.Count == 0) return;

        var first = winners[0];
        var role = CustomRoleManager.GetRole(first);
        var name = role?.Name ?? first.Data?.PlayerName ?? "";
        var suffix = winners.Count > 1 ? $" 等{winners.Count}人" : string.Empty;

        __instance.WinText.text = $"{name}{suffix} 获胜！";
        if (role != null)
            __instance.WinText.color = role.Color;
    }
}
