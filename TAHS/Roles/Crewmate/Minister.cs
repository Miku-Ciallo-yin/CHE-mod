using TAHS.Modules;
using UnityEngine;

namespace TAHS.Roles.Crewmate;

/// <summary>
/// 内阁（船员阵营）：
/// - 获得职业时自动变为黑色，无法被赌（猜测者收到提示）
/// - 必须在限时内完成自己的全部任务，否则自杀
/// - 完成全部任务时从随机船员处夺取配置数量的任务（一人不够则继续夺取他人）
/// - 击杀内阁的中立/内鬼会收到提示；若此后一轮会议前没有再次击杀，则转变为内阁（跟随船员胜利）
/// - 美警贴近内阁会直接击杀（无需计时，不计入美警转变人数）
/// </summary>
public class Minister : RoleBase
{
    /// <summary>任务限时（秒，职业设置中可调）：超时未完成则自杀</summary>
    private static float TaskDeadline => CustomOptions.MinisterTaskDeadline.Value;

    /// <summary>击杀内阁后等待转变的凶手（PlayerId），一轮会议前未再次击杀则变内阁</summary>
    public static readonly HashSet<byte> PendingKillers = new();

    public override string Name => "内阁";
    public override string NameEn => "Minister";
    public override Faction Faction => Faction.Crewmate;
    public override Color Color => new(0.15f, 0.15f, 0.15f); // 黑
    public override string Description => "高雅的灵魂不容揣测。";

    private float _deadline = TaskDeadline;
    private bool _tasksDone;
    private bool _stolen;
    private int _stolenCount;

    public override void OnAssign(PlayerControl player)
    {
        base.OnAssign(player);
        // 自动变为黑色（颜色 ID 6）
        player.RpcSetColor(6);
    }

    public override void OnGameStart()
    {
        ApplyCustomTasks();
    }

    /// <summary>单独设置任务数量：按配置的长/中/短任务数重新分配任务</summary>
    private void ApplyCustomTasks()
    {
        if (CustomOptions.MinisterCustomTaskCount.Value != 1) return;
        if (Player == null || Player.Data == null) return;

        var ship = ShipStatus.Instance;
        if (ship == null) return;

        var ids = new List<byte>();
        PickTasks(ship.LongTasks, CustomOptions.MinisterLongTasks.Value, ids);
        PickTasks(ship.CommonTasks, CustomOptions.MinisterMidTasks.Value, ids);
        PickTasks(ship.ShortTasks, CustomOptions.MinisterShortTasks.Value, ids);
        if (ids.Count > 0)
            Player.Data.RpcSetTasks(ids.ToArray());
    }

    private static void PickTasks(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<NormalPlayerTask> pool, int count, List<byte> output)
    {
        if (pool == null || count <= 0) return;
        var types = pool.Select(t => (byte)t.TaskType).OrderBy(_ => Guid.NewGuid()).ToList();
        for (var i = 0; i < count && i < types.Count; i++)
            output.Add(types[i]);
    }

    /// <summary>主机驱动（Host Only）</summary>
    public override void OnUpdate()
    {
        if (Player == null || Player.Data == null || Player.Data.IsDead) return;
        if (_tasksDone) return;

        var tasks = Player.Data.Tasks.ToArray();
        if (tasks.Length > 0 && tasks.All(t => t.Complete))
        {
            _tasksDone = true;
            StealTasks();
            return;
        }

        // 限时未完成：自杀
        _deadline -= Time.fixedDeltaTime;
        if (_deadline <= 0f)
        {
            Player.RpcMurderPlayer(Player, true);
            TAHSPlugin.Log.LogInfo("[TAHS] 内阁限时未完成任务，自杀");
        }
    }

    /// <summary>完成全部任务：从随机船员处夺取配置数量的任务</summary>
    private void StealTasks()
    {
        if (_stolen) return;
        _stolen = true;

        var remaining = CustomOptions.MinisterStealCount.Value;
        var rng = new System.Random();
        var victims = PlayerControl.AllPlayerControls.ToArray()
            .Where(p => p != null && p != Player && p.Data != null && !p.Data.IsDead)
            .Where(p => CustomRoleManager.GetFaction(p) == Faction.Crewmate)
            .OrderBy(_ => rng.Next())
            .ToList();

        foreach (var victim in victims)
        {
            if (remaining <= 0) break;

            var stealable = victim.Data!.Tasks.ToArray().Where(t => !t.Complete).ToList();
            if (stealable.Count == 0) continue;

            // 该船员任务不够则夺取其全部，继续夺取他人
            var stolen = stealable.Take(remaining).ToList();
            var victimIds = victim.Data.Tasks.ToArray()
                .Where(t => !stolen.Contains(t))
                .Select(t => t.TypeId)
                .ToArray();
            var ministerIds = Player!.Data!.Tasks.ToArray()
                .Select(t => t.TypeId)
                .Concat(stolen.Select(t => t.TypeId))
                .ToArray();

            victim.Data.RpcSetTasks(victimIds);
            Player.Data.RpcSetTasks(ministerIds);

            remaining -= stolen.Count;
            _stolenCount += stolen.Count;
            TAHSPlugin.Log.LogInfo($"[TAHS] 内阁夺取了 {victim.Data.PlayerName} 的 {stolen.Count} 个任务");
        }
    }

    public override string GetStatusText()
    {
        if (_stolen) return $"任务完成（已夺取 {_stolenCount} 个任务）";
        var total = Player?.Data?.Tasks.ToArray();
        var done = total?.Count(t => t.Complete) ?? 0;
        return $"任务 {done}/{total?.Length ?? 0}（限时 {_deadline:0}s）";
    }
}
