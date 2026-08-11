using TAHS.Modules;
using TAHS.Roles.Addons;
using TAHS.Roles.Crewmate;
using TAHS.Roles.Impostor;
using TAHS.Roles.Neutral;

namespace TAHS.Roles;

/// <summary>
/// 职业管理器：注册职业/附加职业、分配、查询。
/// 联机时由主机随机分配并通过 RPC 广播，客户端收到后本地应用（见 <see cref="RpcSync"/>）。
/// </summary>
public static class CustomRoleManager
{
    /// <summary>
    /// 已注册的职业表：稳定 ID -> 工厂。ID 用于 RPC 同步，新增职业时请勿改动已有 ID。
    /// </summary>
    private static readonly (byte Id, Func<RoleBase> Factory)[] RoleRegistry =
    {
        (2, () => new Farmer()),  // 船员阵营：佃农
        (3, () => new Jester()),  // 中立阵营：小丑
        (5, () => new Coward()),  // 中立阵营（敌对）：懦弱者
        (6, () => new Cop()),     // 船员阵营：美警
        (7, () => new Repenter()), // 内鬼阵营：忏悔者
        (8, () => new Minister()), // 船员阵营：内阁
    };

    /// <summary>
    /// 已注册的附加职业表（ID 与职业同空间，从 4 起）。附加职业可与主职业叠加。
    /// </summary>
    private static readonly (byte Id, Func<AddonBase> Factory)[] AddonRegistry =
    {
        (Guesser.AddonId, () => new Guesser()), // 附加：赌怪
    };

    /// <summary>PlayerId -> 职业实例</summary>
    private static readonly Dictionary<byte, RoleBase> PlayerRoles = new();

    /// <summary>PlayerId -> 附加职业列表</summary>
    private static readonly Dictionary<byte, List<AddonBase>> PlayerAddons = new();

    /// <summary>本局已分配的全部职业</summary>
    public static IReadOnlyCollection<RoleBase> ActiveRoles => PlayerRoles.Values;

    /// <summary>本局是否已完成分配</summary>
    public static bool Assigned { get; private set; }

    /// <summary>
    /// 通过自定义条件获胜的玩家列表（如小丑被投出、懦弱者链接共同胜利）。
    /// 非空时结算画面只显示这些玩家。
    /// </summary>
    public static readonly List<PlayerControl> CustomWinners = new();

    /// <summary>设置自定义胜利者（含懦弱者链接的共同胜利伙伴）</summary>
    public static void SetCustomWinner(PlayerControl? winner)
    {
        CustomWinners.Clear();
        if (winner == null) return;

        CustomWinners.Add(winner);

        // 懦弱者链接：链接有效且伙伴是胜利者时共同胜利
        foreach (var role in PlayerRoles.Values)
        {
            if (role is Coward { LinkActive: true } coward
                && coward.LinkedPlayer == winner
                && coward.Player != null
                && coward.Player != winner)
                CustomWinners.Add(coward.Player);
        }
    }

    /// <summary>
    /// 主机随机分配职业和附加职业（每种最多一名玩家），并广播给所有客户端。
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

        // 附加职业：与主职业独立，可叠加在任意玩家身上
        var addonAssignments = new List<(byte PlayerId, byte AddonId)>();
        foreach (var (addonId, _) in AddonRegistry)
        {
            if (rng.Next(100) >= CustomOptions.GetRoleChance(addonId)) continue;

            var pick = players[rng.Next(players.Count)];
            addonAssignments.Add((pick.PlayerId, addonId));
        }

        ApplyRoleAssignments(assignments, addonAssignments);
        RpcSync.BroadcastOptions();
        RpcSync.BroadcastRoleAssignments(assignments, addonAssignments);
    }

    /// <summary>
    /// 应用一份分配结果（主机本地应用 / 客户端收到 RPC 后应用）。
    /// </summary>
    public static void ApplyRoleAssignments(
        IReadOnlyList<(byte PlayerId, byte RoleId)> assignments,
        IReadOnlyList<(byte PlayerId, byte AddonId)> addonAssignments)
    {
        Reset();

        foreach (var (playerId, roleId) in assignments)
        {
            var player = PlayerControl.AllPlayerControls.ToArray()
                .FirstOrDefault(p => p != null && p.PlayerId == playerId);
            var factory = RoleRegistry.FirstOrDefault(r => r.Id == roleId).Factory;
            if (player == null || player.Data == null || factory == null) continue;

            var role = factory();
            role.Id = roleId;
            role.OnAssign(player);
            PlayerRoles[playerId] = role;

            TAHSPlugin.Log.LogInfo($"[TAHS] {player.Data.PlayerName} -> {role.Name} ({role.Faction})");
            GameArchive.RecordAssignment($"{player.Data.PlayerName} → {role.Name}");
        }

        foreach (var (playerId, addonId) in addonAssignments)
        {
            var player = PlayerControl.AllPlayerControls.ToArray()
                .FirstOrDefault(p => p != null && p.PlayerId == playerId);
            var factory = AddonRegistry.FirstOrDefault(a => a.Id == addonId).Factory;
            if (player == null || player.Data == null || factory == null) continue;

            var addon = factory();
            addon.Id = addonId;
            addon.OnAssign(player);
            if (!PlayerAddons.TryGetValue(playerId, out var list))
                PlayerAddons[playerId] = list = new List<AddonBase>();
            list.Add(addon);

            TAHSPlugin.Log.LogInfo($"[TAHS] {player.Data.PlayerName} -> 附加:{addon.Name}");
            GameArchive.RecordAssignment($"{player.Data.PlayerName} → 附加:{addon.Name}");
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

    /// <summary>已注册附加职业（ID、名称）</summary>
    public static IEnumerable<(byte Id, string Name)> GetRegisteredAddons()
    {
        foreach (var (id, factory) in AddonRegistry)
            yield return (id, factory().Name);
    }

    /// <summary>把玩家转变为指定职业实例（替换原有职业，如凶手变内阁）</summary>
    public static void TransformToRole(PlayerControl player, RoleBase newRole)
    {
        // ID 取新职业在注册表中的 ID（猜测/判定依赖职业 ID）
        newRole.Id = RoleRegistry.FirstOrDefault(r => r.Factory().GetType() == newRole.GetType()).Id;
        newRole.OnAssign(player);
        PlayerRoles[player.PlayerId] = newRole;
    }

    /// <summary>获取玩家职业，无职业返回 null</summary>
    public static RoleBase? GetRole(PlayerControl player)
    {
        return PlayerRoles.TryGetValue(player.PlayerId, out var role) ? role : null;
    }

    /// <summary>获取玩家的附加职业列表（无则空列表）</summary>
    public static IReadOnlyList<AddonBase> GetAddons(PlayerControl player)
    {
        return PlayerAddons.TryGetValue(player.PlayerId, out var list)
            ? list
            : System.Array.Empty<AddonBase>();
    }

    /// <summary>玩家是否拥有指定附加职业</summary>
    public static bool HasAddon(PlayerControl player, byte addonId)
    {
        return PlayerAddons.TryGetValue(player.PlayerId, out var list)
               && list.Any(a => a.Id == addonId);
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
        PlayerAddons.Clear();
        Assigned = false;
        CustomWinners.Clear();
        DeathTracker.Clear();
        GameArchive.ArchiveAndReset();
    }
}
