using TAHS.Modules;
using UnityEngine;

namespace TAHS.Roles.Crewmate;

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
    public override bool IsHostileNeutral => true; // 带刀职业，转化后为敌对中立
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
    private bool _buttonGranted;

    public override void OnUpdate()
    {
        if (Player == null || Player.Data == null || Player.Data.IsDead) return;

        var dt = Time.fixedDeltaTime;

        if (HasKillAbility)
        {
            if (!_buttonGranted)
            {
                // 准则：拥有击杀能力时给予原版击杀按钮（无模组端也可用）
                _buttonGranted = true;
                CustomRoleManager.GrantVanillaButtons(Player!);
            }
            if (KillTimer > 0f) KillTimer -= dt;
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

    /// <summary>击杀结算（任意击杀路径）：误杀船员转中立 + 应用击杀 CD</summary>
    public override void OnMurder(PlayerControl target)
    {
        KillTimer = CustomOptions.FarmerKillCooldown.ScaledValue;

        if (_faction == Faction.Neutral) return;
        if (CustomRoleManager.GetFaction(target) != Faction.Crewmate) return;

        _faction = Faction.Neutral;
        TAHSPlugin.Log.LogInfo("[TAHS] 佃农击杀了船员阵营玩家，已转化为中立阵营");
        GameArchive.RecordTransition($"佃农 {Player?.Data?.PlayerName} 误杀船员，转变为中立阵营");
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
        TAHSPlugin.Log.LogInfo(
            $"[TAHS] 佃农抢夺了 {victim.Data.PlayerName} 的一个任务 " +
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
        TAHSPlugin.Log.LogInfo("[TAHS] 佃农已获得击杀能力");
    }

    public override string GetStatusText()
    {
        if (HasKillAbility)
            return KillTimer > 0f ? $"击杀冷却 {KillTimer:0}s" : "击杀已就绪（击杀按钮）";
        return $"抢夺进度 {StealCount}/{CustomOptions.FarmerStealsForKill.Value}";
    }
}
