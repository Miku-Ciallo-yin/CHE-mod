using CHE.Modules;
using UnityEngine;

namespace CHE.Roles.Crewmate;

/// <summary>
/// 佃农（船员阵营）：靠近其他船员可抢夺其任务；
/// 抢够数量并完成现有任务后获得击杀能力（按 Q 击杀最近玩家）；
/// 若击杀船员阵营玩家则转化为中立阵营。
/// 概率 / 所需任务数 / 击杀冷却 / 抢夺范围均可在 BepInEx 配置中调整。
/// </summary>
public class Farmer : RoleBase
{
    /// <summary>击杀范围（游戏单位）</summary>
    private const float KillRange = 2.5f;

    /// <summary>抢夺判定间隔（秒）</summary>
    private const float StealCheckInterval = 1f;

    private Faction _faction = Faction.Crewmate;

    public override string Name => "佃农";
    public override string NameEn => "Farmer";
    public override Faction Faction => _faction;
    public override Color Color => new(0.55f, 0.35f, 0.17f); // 土棕色
    public override string Description => "抢夺船员的任务，积蓄力量后反戈一击。小心别杀错人。";

    /// <summary>已抢夺的任务数</summary>
    public int StealCount { get; private set; }

    /// <summary>是否已解锁击杀能力（解锁后不再抢夺）</summary>
    public bool HasKillAbility { get; private set; }

    /// <summary>击杀冷却剩余时间</summary>
    public float KillTimer { get; private set; }

    private float _stealTimer;
    private readonly System.Random _rng = new();

    public override void OnUpdate()
    {
        if (Player == null || Player.Data == null || Player.Data.IsDead) return;

        var dt = Time.fixedDeltaTime;

        if (HasKillAbility)
        {
            // 已解锁击杀：不再抢夺，只处理冷却和击杀按键（仅主机本地佃农直接按 Q）
            if (KillTimer > 0f) KillTimer -= dt;
            if (KillTimer <= 0f && Player!.AmOwner && Input.GetKeyDown(KeyCode.Q))
                TryKill();
            return;
        }

        _stealTimer -= dt;
        if (_stealTimer <= 0f)
        {
            _stealTimer = StealCheckInterval;
            TryStealFromNearby();
        }

        CheckKillUnlock();
    }

    /// <summary>非主机模组端：按 Q 向主机请求击杀（主机用 ServerKillRequest 验证执行）</summary>
    public override void OnClientUpdate()
    {
        if (Player == null || Player.Data == null || Player.Data.IsDead) return;
        if (!Input.GetKeyDown(KeyCode.Q)) return;

        var target = FindNearestTarget();
        if (target != null)
            RpcSync.SendKillRequest(target.PlayerId);
    }

    /// <summary>主机：处理模组端的击杀请求，验证解锁状态/冷却/距离后执行</summary>
    public void ServerKillRequest(PlayerControl target)
    {
        if (!HasKillAbility || KillTimer > 0f || Player == null || target == null) return;
        if (Vector2.Distance(Player.GetTruePosition(), target.GetTruePosition()) > KillRange) return;

        // 用自杀式 RPC 保证各端一致（与赌怪同一模式），转化判定手动触发
        target.RpcMurderPlayer(target, true);
        OnMurder(target);
        KillTimer = CustomOptions.FarmerKillCooldown.ScaledValue;
        CHEPlugin.Log.LogInfo($"[CHE] 佃农（远程请求）击杀了 {target.Data!.PlayerName}");
    }

    /// <summary>对范围内的每个船员按概率抢夺一个任务</summary>
    private void TryStealFromNearby()
    {
        var range = CustomOptions.FarmerStealRange.ScaledValue;
        var chance = Mathf.Clamp01(CustomOptions.FarmerStealChance.ScaledValue);
        if (chance <= 0f) return;

        foreach (var victim in PlayerControl.AllPlayerControls)
        {
            if (victim == null || victim == Player) continue;
            if (victim.Data == null || victim.Data.IsDead) continue;
            if (CustomRoleManager.GetFaction(victim) != Faction.Crewmate) continue;
            if (Vector2.Distance(Player!.GetTruePosition(), victim.GetTruePosition()) > range) continue;
            if (_rng.NextDouble() > chance) continue;

            if (StealTask(victim))
                break; // 每次判定最多抢一个任务
        }
    }

    /// <summary>把受害者的一个未完成任务转移给佃农</summary>
    private bool StealTask(PlayerControl victim)
    {
        var stealable = victim.Data!.Tasks.ToArray().Where(t => !t.Complete).ToList();
        if (stealable.Count == 0) return false;

        var stolen = stealable[_rng.Next(stealable.Count)];

        var victimIds = victim.Data.Tasks.ToArray()
            .Where(t => t != stolen)
            .Select(t => t.TypeId)
            .ToArray();
        var farmerIds = Player!.Data!.Tasks.ToArray()
            .Select(t => t.TypeId)
            .Append(stolen.TypeId)
            .ToArray();

        victim.Data.RpcSetTasks(victimIds);
        Player.Data.RpcSetTasks(farmerIds);

        StealCount++;
        CHEPlugin.Log.LogInfo(
            $"[CHE] 佃农抢夺了 {victim.Data.PlayerName} 的一个任务 " +
            $"({StealCount}/{CustomOptions.FarmerStealsForKill.Value})");
        return true;
    }

    /// <summary>抢够数量且现有任务全部完成时解锁击杀能力</summary>
    private void CheckKillUnlock()
    {
        if (StealCount < CustomOptions.FarmerStealsForKill.Value) return;
        if (Player!.Data!.Tasks.ToArray().Any(t => !t.Complete)) return;

        HasKillAbility = true;
        KillTimer = 0f;
        CHEPlugin.Log.LogInfo("[CHE] 佃农已获得击杀能力");
    }

    /// <summary>击杀范围内最近的存活玩家</summary>
    private PlayerControl? FindNearestTarget()
    {
        var pos = Player!.GetTruePosition();
        return PlayerControl.AllPlayerControls.ToArray()
            .Where(p => p != null && p != Player && p.Data != null && !p.Data.IsDead)
            .Where(p => Vector2.Distance(pos, p.GetTruePosition()) <= KillRange)
            .OrderBy(p => Vector2.Distance(pos, p.GetTruePosition()))
            .FirstOrDefault();
    }

    /// <summary>主机本地佃农击杀（Q 键）</summary>
    private void TryKill()
    {
        var target = FindNearestTarget();
        if (target == null) return;

        Player!.RpcMurderPlayer(target, true);
        KillTimer = CustomOptions.FarmerKillCooldown.ScaledValue;
        CHEPlugin.Log.LogInfo($"[CHE] 佃农击杀了 {target.Data!.PlayerName}");
    }

    /// <summary>击杀结算：误杀船员阵营则转化为中立</summary>
    public override void OnMurder(PlayerControl target)
    {
        if (_faction == Faction.Neutral) return;
        if (CustomRoleManager.GetFaction(target) != Faction.Crewmate) return;

        _faction = Faction.Neutral;
        CHEPlugin.Log.LogInfo("[CHE] 佃农击杀了船员阵营玩家，已转化为中立阵营");
    }

    public override string GetStatusText()
    {
        if (HasKillAbility)
            return KillTimer > 0f ? $"击杀冷却 {KillTimer:0}s" : "[Q] 击杀就绪";
        return $"抢夺进度 {StealCount}/{CustomOptions.FarmerStealsForKill.Value}";
    }
}
