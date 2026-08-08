using CHE.Roles.Crewmate;
using CHE.Roles.Neutral;

namespace CHE.Roles;

/// <summary>
/// 职业管理器：注册职业、分配职业、查询玩家职业。
/// 注意：当前骨架仅做本机分配，多人同步需要通过 RPC（TODO）。
/// </summary>
public static class CustomRoleManager
{
    /// <summary>
    /// 已注册的职业工厂。新增职业在这里加一行即可参与随机分配。
    /// </summary>
    private static readonly List<Func<RoleBase>> RoleFactories = new()
    {
        () => new Sheriff(), // 船员阵营示例
        () => new Farmer(),  // 船员阵营：佃农
        () => new Jester(),  // 中立阵营示例
    };

    /// <summary>PlayerId -> 职业实例</summary>
    private static readonly Dictionary<byte, RoleBase> PlayerRoles = new();

    /// <summary>本局已分配的全部职业</summary>
    public static IReadOnlyCollection<RoleBase> ActiveRoles => PlayerRoles.Values;

    /// <summary>本局是否已完成分配</summary>
    public static bool Assigned { get; private set; }

    /// <summary>
    /// 通过自定义条件获胜的玩家（如小丑被投出）。非 null 时结算画面只显示该玩家。
    /// </summary>
    public static PlayerControl? CustomWinner { get; set; }

    /// <summary>
    /// 随机分配职业（每种职业最多一名玩家）。
    /// TODO: 参考 TONE 增加职业数量配置、按阵营配比、RPC 广播分配结果。
    /// </summary>
    public static void AssignRoles()
    {
        Reset();

        var players = PlayerControl.AllPlayerControls.ToArray()
            .Where(p => p != null && p.Data != null)
            .ToList();
        if (players.Count == 0) return;

        var rng = new Random();
        foreach (var factory in RoleFactories)
        {
            var candidates = players.Where(p => !PlayerRoles.ContainsKey(p.PlayerId)).ToList();
            if (candidates.Count == 0) break;

            var pick = candidates[rng.Next(candidates.Count)];
            var role = factory();
            role.OnAssign(pick);
            PlayerRoles[pick.PlayerId] = role;

            CHEPlugin.Log.LogInfo($"[CHE] {pick.Data.PlayerName} -> {role.Name} ({role.Faction})");
        }

        Assigned = true;
        foreach (var role in PlayerRoles.Values)
            role.OnGameStart();
    }

    /// <summary>获取玩家职业，无职业返回 null</summary>
    public static RoleBase? GetRole(PlayerControl player)
    {
        return PlayerRoles.TryGetValue(player.PlayerId, out var role) ? role : null;
    }

    /// <summary>
    /// 获取玩家阵营：有自定义职业按职业算；无职业时按原版身份（内鬼 / 船员）。
    /// </summary>
    public static Faction GetFaction(PlayerControl player)
    {
        var role = GetRole(player);
        if (role != null) return role.Faction;

        if (player.Data != null && player.Data.Role != null && player.Data.Role.IsImpostor)
            return Faction.Impostor;
        return Faction.Crewmate;
    }

    /// <summary>清空分配（游戏结束 / 返回大厅时调用）</summary>
    public static void Reset()
    {
        foreach (var role in PlayerRoles.Values)
            role.OnReset();
        PlayerRoles.Clear();
        Assigned = false;
        CustomWinner = null;
    }
}
