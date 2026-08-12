using TAHS.Modules;
using TAHS.Roles;
using TAHS.Roles.Addons;
using HarmonyLib;

namespace TAHS.Patches;

/// <summary>
/// 叛徒「记入内鬼阵营人数」开启时的结束判定接管（仅主机，且场上有存活叛徒时生效）：
/// 复刻原版判定（破坏倒计时 / 任务完成 / 人数），人数统计时把非内鬼身份的存活叛徒计入内鬼一侧，
/// 阻止原版用不含叛徒的人数误判。关闭该配置或无存活叛徒时不干预，走原版判定。
/// </summary>
[HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.CheckEndCriteria))]
public static class TraitorEndCriteriaPatch
{
    public static bool Prefix()
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return true;
        if (CustomOptions.TestMode.Value == 1) return true; // 测试模式由其补丁阻断原版判定
        if (CustomOptions.TraitorCountAsImpostor.Value != 1) return true;
        if (GameManager.Instance == null || GameData.Instance == null) return true;

        // 有存活叛徒才需要接管（内鬼身份的叛徒原版已计入，无需处理）
        var anyAliveTraitor = false;
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p == null || p.Data == null || p.Data.IsDead || p.Data.Disconnected) continue;
            if (p.Data.Role != null && p.Data.Role.IsImpostor) continue;
            if (Traitor.IsTraitor(p)) { anyAliveTraitor = true; break; }
        }
        if (!anyAliveTraitor) return true;

        // 破坏倒计时（生命维持 / 反应堆等关键系统）
        var ship = ShipStatus.Instance;
        if (ship != null)
        {
            if (ship.Systems.TryGetValue(SystemTypes.LifeSupp, out var supp))
            {
                var life = supp.TryCast<LifeSuppSystemType>();
                if (life != null && life.Countdown < 0f)
                    return EndGame(GameOverReason.ImpostorsBySabotage);
            }
            if (ship.Systems.TryGetValue(SystemTypes.Reactor, out var react))
            {
                var critical = react.TryCast<ICriticalSabotage>();
                if (critical != null && critical.Countdown < 0f)
                    return EndGame(GameOverReason.ImpostorsBySabotage);
            }
        }

        // 任务完成：船员胜利（叛徒不随船员获胜，结算名单由 EndGamePatch 剔除）
        if (GameData.Instance.TotalTasks <= GameData.Instance.CompletedTasks)
            return EndGame(GameOverReason.CrewmatesByTask);

        // 人数判定：叛徒计入内鬼一侧
        var impostors = 0;
        var crew = 0;
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p == null || p.Data == null || p.Data.IsDead || p.Data.Disconnected) continue;
            if ((p.Data.Role != null && p.Data.Role.IsImpostor) || Traitor.IsTraitor(p))
                impostors++;
            else
                crew++;
        }
        if (impostors == 0)
            return EndGame(GameOverReason.CrewmatesByVote);
        if (impostors >= crew)
            return EndGame(GameOverReason.ImpostorsByKill);

        return false; // 已接管，阻断原版人数误判（游戏继续）
    }

    private static bool EndGame(GameOverReason reason)
    {
        TAHSPlugin.Log.LogInfo($"[TAHS] 叛徒计入人数，接管结束判定：{reason}");
        GameManager.Instance.RpcEndGame(reason, false);
        return false;
    }
}
