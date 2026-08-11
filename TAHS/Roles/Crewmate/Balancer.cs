using TAHS.Modules;
using UnityEngine;

namespace TAHS.Roles.Crewmate;

/// <summary>
/// 平衡主义者（船员阵营）：
/// - 当场上某阵营人数超过该阵营开局人数时得知该信息（但不知道哪个阵营）
/// - 可用 /ph 指令随机处决超编阵营的一名随机玩家
/// 配置：技能可使用次数
/// </summary>
public class Balancer : RoleBase
{
    public override string Name => "平衡主义者";
    public override string NameEn => "Balancer";
    public override Faction Faction => Faction.Crewmate;
    public override Color Color => new(0.4f, 0.7f, 1f); // 天平蓝
    public override string Description => "任何阵营的膨胀都逃不过天平。用 /ph 拨回平衡。";

    /// <summary>剩余技能次数</summary>
    public int UsesLeft { get; private set; }

    /// <summary>开局各阵营人数基线</summary>
    private static readonly Dictionary<Faction, int> _initialCounts = new();

    /// <summary>当前已超编的阵营集合（用于检测新的超编事件）</summary>
    private static readonly HashSet<Faction> _exceeding = new();

    private float _checkTimer;

    public override void OnAssign(PlayerControl player)
    {
        base.OnAssign(player);
        UsesLeft = CustomOptions.BalancerSkillUses.Value;
    }

    /// <summary>对局重置时清空静态状态（由 CustomRoleManager.Reset 调用）</summary>
    public static void ResetStatics()
    {
        _initialCounts.Clear();
        _exceeding.Clear();
    }

    /// <summary>主机：分配完成后记录开局阵营人数基线</summary>
    public static void RecordInitialCounts()
    {
        _initialCounts.Clear();
        _exceeding.Clear();
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p == null || p.Data == null) continue;
            var faction = CustomRoleManager.GetFaction(p);
            _initialCounts[faction] = _initialCounts.GetValueOrDefault(faction) + 1;
        }
    }

    /// <summary>主机驱动：每秒检测阵营超编并通知存活平衡主义者</summary>
    public override void OnUpdate()
    {
        if (Player == null || Player.Data == null) return;

        _checkTimer -= Time.fixedDeltaTime;
        if (_checkTimer > 0f) return;
        _checkTimer = 1f;

        if (_initialCounts.Count == 0) return;

        // 当前各阵营存活人数
        var current = new Dictionary<Faction, int>();
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p == null || p.Data == null || p.Data.IsDead) continue;
            var faction = CustomRoleManager.GetFaction(p);
            current[faction] = current.GetValueOrDefault(faction) + 1;
        }

        // 出现新的超编阵营 → 通知（不透露是哪个阵营）
        foreach (var (faction, count) in current)
        {
            var initial = _initialCounts.GetValueOrDefault(faction);
            if (count > initial && _exceeding.Add(faction))
                NotifyBalancers("[TAHS] 警告：场上某阵营人数已超过其开局人数！可用 /ph 拨回平衡");
        }
    }

    /// <summary>通知所有存活平衡主义者（模组端私聊 / 无模组端定向聊天）</summary>
    private static void NotifyBalancers(string text)
    {
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p == null || p.Data == null || p.Data.IsDead) continue;
            if (CustomRoleManager.GetRole(p) is Balancer)
                ChatHelper.ShowPrivate(p, text);
        }
    }

    /// <summary>主机：使用技能——随机处决一名超编阵营的随机存活玩家</summary>
    public static void UseSkill(PlayerControl user)
    {
        var role = CustomRoleManager.GetRole(user);
        if (role is not Balancer balancer) return;
        if (balancer.UsesLeft <= 0)
        {
            ChatHelper.ShowPrivate(user, "[TAHS] 技能次数已用尽");
            return;
        }

        // 找出超编阵营
        var current = new Dictionary<Faction, List<PlayerControl>>();
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p == null || p.Data == null || p.Data.IsDead) continue;
            var faction = CustomRoleManager.GetFaction(p);
            if (!current.TryGetValue(faction, out var list))
                current[faction] = list = new List<PlayerControl>();
            list.Add(p);
        }

        var exceeding = current
            .Where(kv => kv.Value.Count > _initialCounts.GetValueOrDefault(kv.Key))
            .ToList();

        if (exceeding.Count == 0)
        {
            ChatHelper.ShowPrivate(user, "[TAHS] 当前没有超编阵营");
            return;
        }

        var rng = new System.Random();
        var (_, targets) = exceeding[rng.Next(exceeding.Count)];
        var victim = targets[rng.Next(targets.Count)];

        balancer.UsesLeft--;
        victim.RpcMurderPlayer(victim, true);

        TAHSPlugin.Log.LogInfo($"[TAHS] 平衡主义者 {user.Data?.PlayerName} 处决了 {victim.Data?.PlayerName}");
        GameArchive.RecordKill($"平衡主义者 {user.Data?.PlayerName} 处决了 {victim.Data?.PlayerName}");
        ChatHelper.ShowPrivate(user, $"[TAHS] 已处决一名超编阵营玩家（剩余次数 {balancer.UsesLeft}）");
    }

    public override string GetStatusText()
    {
        return $"平衡次数 {UsesLeft}/{CustomOptions.BalancerSkillUses.Value}";
    }
}
