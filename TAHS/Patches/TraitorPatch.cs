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
        if (CustomOptions.TestMode.Value == 1) return true; // 测试模式：自动结束由 RpcEndGame 补丁拦截
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

/// <summary>
/// 叛徒互认红名的主机驱动（参考 TONE 定向改名，无模组客户端也可见）：
/// 定期按配置计算"谁应该看到谁的红色名字"，经 PrivateTag 定向 SetName 下发；
/// 名字颜色只作用于指定观看者的客户端，其他人看到的仍是原名色。
/// </summary>
public static class TraitorNameColors
{
    private const string Red = "#FF1919";
    private const float Interval = 1.5f;
    private static float _timer;

    /// <summary>每帧驱动（AnnouncementPatch 调用）；仅主机执行</summary>
    public static void Tick()
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;

        _timer -= UnityEngine.Time.deltaTime;
        if (_timer > 0f) return;
        _timer = Interval;

        // 非对局状态：清掉全部红名
        if (AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started
            || GameData.Instance == null)
        {
            foreach (var (viewer, target) in PrivateTag.ColorPairs.ToList())
            {
                var t = FindPlayer(target);
                if (t != null) PrivateTag.RemoveColor(viewer, t);
            }
            return;
        }

        var desired = ComputeDesired();

        // 移除不再需要的（差量对比 PrivateTag 实际登记，ClearAll 后可自愈）
        foreach (var (viewer, target) in PrivateTag.ColorPairs.ToList())
        {
            if (desired.Contains((viewer, target))) continue;
            var t = FindPlayer(target);
            if (t != null) PrivateTag.RemoveColor(viewer, t);
        }

        // 补上新增的
        foreach (var (viewer, target) in desired)
        {
            if (PrivateTag.GetColor(viewer, target) != null) continue;
            var t = FindPlayer(target);
            if (t != null) PrivateTag.SetColor(viewer, t, Red);
        }
    }

    /// <summary>按当前配置计算全部互认对（观看者 ClientId, 目标 PlayerId）</summary>
    private static HashSet<(int Viewer, byte Target)> ComputeDesired()
    {
        var desired = new HashSet<(int, byte)>();
        var knowImpostors = CustomOptions.TraitorKnowImpostors.Value == 1;
        var knowEachOther = CustomOptions.TraitorKnowEachOther.Value == 1;
        if (!knowImpostors && !knowEachOther) return desired;

        var traitors = new List<PlayerControl>();
        var impostors = new List<PlayerControl>();
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p == null || p.Data == null) continue;
            if (Traitor.IsTraitor(p))
                traitors.Add(p);
            else if (CustomRoleManager.GetFaction(p) == Faction.Impostor
                     && !CustomRoleManager.FakeImpostors.Contains(p.PlayerId)) // 追杀者临时身份不算
                impostors.Add(p);
        }

        if (knowImpostors)
            foreach (var traitor in traitors)
                foreach (var impostor in impostors)
                {
                    desired.Add((traitor.OwnerId, impostor.PlayerId)); // 叛徒看内鬼红
                    desired.Add((impostor.OwnerId, traitor.PlayerId)); // 内鬼看叛徒红
                }

        if (knowEachOther)
            foreach (var t1 in traitors)
                foreach (var t2 in traitors)
                    if (t1 != t2)
                        desired.Add((t1.OwnerId, t2.PlayerId));

        return desired;
    }

    private static PlayerControl? FindPlayer(byte playerId)
    {
        return PlayerControl.AllPlayerControls.ToArray()
            .FirstOrDefault(p => p != null && p.PlayerId == playerId);
    }
}
