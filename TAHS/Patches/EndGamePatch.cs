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
        if (winners.Count == 0)
        {
            AdjustForTraitors();
            return;
        }

        EndGameResult.CachedWinners.Clear();
        foreach (var winner in winners)
            if (winner != null && winner.Data != null)
                EndGameResult.CachedWinners.Add(new CachedPlayerData(winner.Data));
    }

    /// <summary>
    /// 叛徒：原本胜利条件失效，跟随内鬼胜负。
    /// 内鬼获胜时把叛徒并入胜利名单；其他结果（船员/中立）把叛徒从原版胜利名单剔除。
    /// 自定义胜利（小丑等）整单替换，无需调整。
    /// 无模组客户端走原版结算，叛徒仍按原身份显示胜负（Host Only 降级点）。
    /// </summary>
    private static void AdjustForTraitors()
    {
        var traitors = new System.Collections.Generic.List<PlayerControl>();
        foreach (var p in PlayerControl.AllPlayerControls)
            if (p != null && p.Data != null && TAHS.Roles.Addons.Traitor.IsTraitor(p))
                traitors.Add(p);
        if (traitors.Count == 0) return;

        var reason = EndGameResult.CachedGameOverReason;
        var impostorWin = reason is GameOverReason.ImpostorsByKill
            or GameOverReason.ImpostorsBySabotage
            or GameOverReason.ImpostorsByVote
            or GameOverReason.ImpostorDisconnect;

        if (impostorWin)
        {
            foreach (var traitor in traitors)
            {
                var exists = false;
                foreach (var w in EndGameResult.CachedWinners)
                    if (w.PlayerName == traitor.Data.PlayerName) { exists = true; break; }
                if (!exists)
                    EndGameResult.CachedWinners.Add(new CachedPlayerData(traitor.Data));
            }
        }
        else
        {
            for (var i = EndGameResult.CachedWinners.Count - 1; i >= 0; i--)
            {
                var name = EndGameResult.CachedWinners[i].PlayerName;
                foreach (var traitor in traitors)
                    if (name == traitor.Data.PlayerName)
                    {
                        EndGameResult.CachedWinners.RemoveAt(i);
                        break;
                    }
            }
        }
    }

    /// <summary>替换胜利文字和颜色</summary>
    public static void Postfix(EndGameManager __instance)
    {
        var winners = CustomRoleManager.CustomWinners;

        // 无自定义胜利者时：月跑入机的共同幸存者并入原版胜利名单
        if (winners.Count == 0)
        {
            foreach (var coWinner in TAHS.Roles.Neutral.MoonRunner.CoWinners)
            {
                if (coWinner == null || coWinner.Data == null) continue;
                var exists = false;
                foreach (var w in EndGameResult.CachedWinners)
                    if (w.PlayerName == coWinner.Data.PlayerName) { exists = true; break; }
                if (!exists)
                    EndGameResult.CachedWinners.Add(new CachedPlayerData(coWinner.Data));
            }
            return;
        }

        var first = winners[0];
        var role = CustomRoleManager.GetRole(first);
        var name = role?.Name ?? first.Data?.PlayerName ?? "";
        var suffix = winners.Count > 1 ? $" 等{winners.Count}人" : string.Empty;

        __instance.WinText.text = $"{name}{suffix} 获胜！";
        if (role != null)
            __instance.WinText.color = role.Color;
    }
}
