using CHE.Modules;
using CHE.Roles.Crewmate;
using CHE.Roles.Neutral;

namespace CHE.Roles;

/// <summary>
/// 职业管理器：注册职业、分配职业、查询玩家职业。
/// 联机时由主机随机分配并通过 RPC 广播，客户端收到后本地应用（见 <see cref="RpcSync"/>）。
/// </summary>
public static class CustomRoleManager
{
    /// <summary>
    /// 已注册的职业表：稳定 ID -> 工厂。ID 用于 RPC 同步，新增职业时请勿改动已有 ID。
    /// </summary>
    private static readonly (byte Id, Func<RoleBase> Factory)[] RoleRegistry =
    {
        (1, () => new Sheriff()), // 船员阵营示例
        (2, () => new Farmer()),  // 船员阵营：佃农
        (3, () => new Jester()),  // 中立阵营：小丑
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
    /// 主机随机分配职业（每种职业最多一名玩家），并广播给所有客户端。
    /// TODO: 参考 TONE 增加职业数量配置、按阵营配比。
    /// </summary>
    public static void AssignRoles()
    {
        var players = PlayerControl.AllPlayerControls.ToArray()
            .Where(p => p != null && p.Data != null)
            .ToList();
        if (players.Count == 0) return;

        var rng = new Random();
        var assignments = new List<(byte PlayerId, byte RoleId)>();
        var taken = new HashSet<byte>();

        foreach (var (id, _) in RoleRegistry)
        {
            // 按大厅设置中的生成概率决定该职业是否出场
            if (rng.Next(100) >= CustomOptions.GetRoleChance(id)) continue;

            var candidates = players.Where(p => !taken.Contains(p.PlayerId)).ToList();
            if (candidates.Count == 0) break;

            var pick = candidates[rng.Next(candidates.Count)];
            taken.Add(pick.PlayerId);
            assignments.Add((pick.PlayerId, id));
        }

        ApplyRoleAssignments(assignments);
        RpcSync.BroadcastOptions();
        RpcSync.BroadcastRoleAssignments(assignments);
    }

    /// <summary>
    /// 应用一份分配结果（主机本地应用 / 客户端收到 RPC 后应用）。
    /// </summary>
    public static void ApplyRoleAssignments(IReadOnlyList<(byte PlayerId, byte RoleId)> assignments)
    {
        Reset();

        foreach (var (playerId, roleId) in assignments)
        {
            var player = PlayerControl.AllPlayerControls.ToArray()
                .FirstOrDefault(p => p != null && p.PlayerId == playerId);
            var factory = RoleRegistry.FirstOrDefault(r => r.Id == roleId).Factory;
            if (player == null || player.Data == null || factory == null) continue;

            var role = factory();
            role.OnAssign(player);
            PlayerRoles[playerId] = role;

            CHEPlugin.Log.LogInfo($"[CHE] {player.Data.PlayerName} -> {role.Name} ({role.Faction})");
        }

        Assigned = true;
        foreach (var role in PlayerRoles.Values)
            role.OnGameStart();
    }

    /// <summary>已注册职业（ID、名称、阵营），供选项系统生成设置项和分类列表</summary>
    public static IEnumerable<(byte Id, string Name, Faction Faction)> GetRegisteredRoles()
    {
        foreach (var (id, factory) in RoleRegistry)
        {
            var sample = factory();
            yield return (id, sample.Name, sample.Faction);
        }
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
