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
        (9, () => new Apostle()),  // 船员阵营：使徒
        (10, () => new MoonRunner()), // 中立阵营（友好）：月跑入机
        (11, () => new Pilot()),   // 内鬼阵营：中东机长
        (12, () => new Converter()), // 船员阵营：转换者
        (13, () => new Balancer()),  // 船员阵营：平衡主义者
        (14, () => new Miner()),   // 内鬼阵营：埋雷兵
        (16, () => new FrenchArmy()), // 船员阵营：法军
    };

    /// <summary>
    /// 已注册的附加职业表（ID 与职业同空间，从 4 起）。附加职业可与主职业叠加。
    /// </summary>
    private static readonly (byte Id, Func<AddonBase> Factory)[] AddonRegistry =
    {
        (Guesser.AddonId, () => new Guesser()), // 附加：赌怪
        (Traitor.AddonId, () => new Traitor()), // 附加：叛徒
    };

    /// <summary>PlayerId -> 职业实例</summary>
    private static readonly Dictionary<byte, RoleBase> PlayerRoles = new();

    /// <summary>PlayerId -> 附加职业列表</summary>
    private static readonly Dictionary<byte, List<AddonBase>> PlayerAddons = new();

    /// <summary>本局已分配的全部职业</summary>
    public static IReadOnlyCollection<RoleBase> ActiveRoles => PlayerRoles.Values;

    /// <summary>
    /// 临时获得内鬼系身份（变形者）以拥有原版按钮的非内鬼阵营玩家。
    /// 用于对内鬼隐藏他们的红名。
    /// </summary>
    public static readonly HashSet<byte> FakeImpostors = new();

    /// <summary>主机：赋予原版内鬼按钮（变形者身份），并登记红名隐藏</summary>
    public static void GrantVanillaButtons(PlayerControl player)
    {
        if (player == null) return;
        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            player.RpcSetRole(AmongUs.GameOptions.RoleTypes.Shapeshifter);
        FakeImpostors.Add(player.PlayerId);
    }

    /// <summary>主机：回收原版按钮，恢复原本身份</summary>
    public static void RevokeVanillaButtons(PlayerControl player, bool toImpostor = false)
    {
        if (player == null) return;
        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            player.RpcSetRole(toImpostor
                ? AmongUs.GameOptions.RoleTypes.Impostor
                : AmongUs.GameOptions.RoleTypes.Crewmate);
        FakeImpostors.Remove(player.PlayerId);
    }

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

    /// <summary>设置多个自定义胜利者（月跑入机直接胜利等）</summary>
    public static void SetCustomWinners(IEnumerable<PlayerControl> winners)
    {
        CustomWinners.Clear();
        foreach (var winner in winners)
            if (winner != null && !CustomWinners.Contains(winner))
                CustomWinners.Add(winner);
    }

    /// <summary>
    /// 主机随机分配职业和附加职业，并广播给所有客户端。
    /// 船员/内鬼职业每种最多一名玩家（按生成概率）；
    /// 中立职业按"带刀中立数量 / 无刀中立数量"配置分配（同职业可分配给多人）。
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

        // 船员/内鬼职业：每种职业按"人数 × 生成概率"独立判定
        foreach (var (id, factory) in RoleRegistry)
        {
            if (factory().Faction == Faction.Neutral) continue; // 中立走类别预算
            AssignByCount(id, int.MaxValue);
        }

        // 中立职业：人数 × 概率判定，且受带刀/无刀类别数量预算限制
        var knifeBudget = CustomOptions.NeutralKnifeCount.Value;
        var noKnifeBudget = CustomOptions.NeutralNoKnifeCount.Value;
        foreach (var (id, factory) in RoleRegistry.OrderBy(_ => rng.Next()))
        {
            var sample = factory();
            if (sample.Faction != Faction.Neutral) continue;

            var assigned = AssignByCount(id, sample.IsHostileNeutral ? knifeBudget : noKnifeBudget);
            if (sample.IsHostileNeutral) knifeBudget -= assigned;
            else noKnifeBudget -= assigned;
        }

        // 附加职业：与主职业独立，按"人数 × 概率"判定，可叠加在任意玩家身上
        // （受玩家附加职业数量上限限制）
        var addonAssignments = new List<(byte PlayerId, byte AddonId)>();
        foreach (var (addonId, _) in AddonRegistry)
        {
            for (var i = 0; i < CustomOptions.GetRoleCount(addonId); i++)
            {
                if (rng.Next(100) >= CustomOptions.GetRoleChance(addonId)) continue;

                var maxAddons = CustomOptions.MaxAddonsPerPlayer.Value;
                var available = players
                    .Where(p => addonAssignments.Count(a => a.PlayerId == p.PlayerId) < maxAddons)
                    // 赌怪按阵营资格过滤（用待分配的职业 ID 判定，此时职业尚未应用）
                    .Where(p => addonId != Guesser.AddonId || GuesserEligibleFor(
                        assignments.FirstOrDefault(a => a.PlayerId == p.PlayerId).RoleId, p))
                    // 叛徒不分配给内鬼阵营（跟随内鬼胜利对内鬼无意义）
                    .Where(p => addonId != Traitor.AddonId || !IsImpostorFactionFor(
                        assignments.FirstOrDefault(a => a.PlayerId == p.PlayerId).RoleId, p))
                    // 叛徒不分配给法军（法军自带被内鬼击杀后缴械成叛徒的机制）
                    .Where(p => addonId != Traitor.AddonId
                        || assignments.FirstOrDefault(a => a.PlayerId == p.PlayerId).RoleId != FrenchArmy.RoleId)
                    .ToList();
                if (available.Count == 0) break;

                var pick = available[rng.Next(available.Count)];
                if (addonAssignments.Any(a => a.PlayerId == pick.PlayerId && a.AddonId == addonId)) continue;
                addonAssignments.Add((pick.PlayerId, addonId));
            }
        }

        ApplyRoleAssignments(assignments, addonAssignments);
        Balancer.RecordInitialCounts(); // 平衡主义者：记录开局阵营人数基线
        RpcSync.BroadcastOptions();
        RpcSync.BroadcastRoleAssignments(assignments, addonAssignments);

        // 每种职业按人数上限逐个判定概率并分配，返回实际分配数
        int AssignByCount(byte roleId, int maxCount)
        {
            var assigned = 0;
            var count = CustomOptions.GetRoleCount(roleId);
            for (var i = 0; i < count && assigned < maxCount; i++)
            {
                if (rng.Next(100) >= CustomOptions.GetRoleChance(roleId)) continue;

                var available = players.Where(p => !taken.Contains(p.PlayerId)).ToList();
                if (available.Count == 0) return assigned;

                var pick = available[rng.Next(available.Count)];
                taken.Add(pick.PlayerId);
                assignments.Add((pick.PlayerId, roleId));
                assigned++;
            }
            return assigned;
        }
    }

    /// <summary>
    /// 应用一份分配结果（主机本地应用 / 客户端收到 RPC 后应用）。
    /// </summary>
    public static void ApplyRoleAssignments(
        IReadOnlyList<(byte PlayerId, byte RoleId)> assignments,
        IReadOnlyList<(byte PlayerId, byte AddonId)> addonAssignments)
    {
        Reset();
        FirstKillProtection.OnNewGame(); // 首刀保护：结转上一局首死者

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

    /// <summary>已注册附加职业（ID、名称、类别）</summary>
    public static IEnumerable<(byte Id, string Name, AddonType Type)> GetRegisteredAddons()
    {
        foreach (var (id, factory) in AddonRegistry)
        {
            var sample = factory();
            yield return (id, sample.Name, sample.Type);
        }
    }

    /// <summary>已注册职业样本（ID + 实例，可取描述等完整信息，如 TAB 职业介绍）</summary>
    public static IEnumerable<(byte Id, RoleBase Sample)> GetRoleSamples()
    {
        foreach (var (id, factory) in RoleRegistry)
        {
            var sample = factory();
            sample.Id = id;
            yield return (id, sample);
        }
    }

    /// <summary>已注册附加职业样本（ID + 实例，可取描述等完整信息）</summary>
    public static IEnumerable<(byte Id, AddonBase Sample)> GetAddonSamples()
    {
        foreach (var (id, factory) in AddonRegistry)
        {
            var sample = factory();
            sample.Id = id;
            yield return (id, sample);
        }
    }

    /// <summary>把玩家转变为指定职业实例（替换原有职业，如凶手变内阁）</summary>
    public static void TransformToRole(PlayerControl player, RoleBase newRole)
    {
        // 转变前回收旧职业的原版按钮
        if (FakeImpostors.Contains(player.PlayerId))
            RevokeVanillaButtons(player);

        // ID 取新职业在注册表中的 ID（猜测/判定依赖职业 ID）
        newRole.Id = RoleRegistry.FirstOrDefault(r => r.Factory().GetType() == newRole.GetType()).Id;
        newRole.OnAssign(player);
        PlayerRoles[player.PlayerId] = newRole;
    }

    /// <summary>按已有职业实例的类型创建新实例（转换者复制职业用）</summary>
    public static RoleBase? CreateRoleOfType(RoleBase sample)
    {
        var factory = RoleRegistry.FirstOrDefault(r => r.Factory().GetType() == sample.GetType()).Factory;
        return factory?.Invoke();
    }

    /// <summary>移除玩家的职业（变回原版身份）</summary>
    public static void RemoveRole(PlayerControl player)
    {
        if (FakeImpostors.Contains(player.PlayerId))
            RevokeVanillaButtons(player);
        PlayerRoles.Remove(player.PlayerId);
    }

    /// <summary>赐予玩家一个附加职业（使徒完成任务时，主机调用并广播）</summary>
    public static void GrantAddon(PlayerControl player, byte addonId)
    {
        var factory = AddonRegistry.FirstOrDefault(a => a.Id == addonId).Factory;
        if (factory == null || player == null || player.Data == null) return;

        var addon = factory();
        addon.Id = addonId;
        addon.OnAssign(player);
        if (!PlayerAddons.TryGetValue(player.PlayerId, out var list))
            PlayerAddons[player.PlayerId] = list = new List<AddonBase>();
        list.Add(addon);

        TAHSPlugin.Log.LogInfo($"[TAHS] {player.Data.PlayerName} 获得附加职业「{addon.Name}」");
        GameArchive.RecordTransition($"{player.Data.PlayerName} 获得良性附加职业「{addon.Name}」（使徒赐予）");
    }

    /// <summary>使徒完成任务：随机赐予一名船员阵营玩家一个良性附加职业（仅主机调用）</summary>
    public static void GrantRandomBenignAddon()
    {
        var rng = new Random();
        var benign = AddonRegistry
            .Where(a => a.Factory().IsBenign)
            .Where(a => a.Factory().ApostleGrantable) // 叛徒等不可赐予的除外
            .ToList();
        if (benign.Count == 0) return;

        var (addonId, _) = benign[rng.Next(benign.Count)];
        var candidates = PlayerControl.AllPlayerControls.ToArray()
            .Where(p => p != null && p.Data != null && !p.Data.IsDead)
            .Where(p => GetFaction(p) == Faction.Crewmate)
            .Where(p => GetAddons(p).Count < CustomOptions.MaxAddonsPerPlayer.Value) // 附加职业数量上限
            .Where(p => !GetAddons(p).Any(a => a.Id == addonId))
            .Where(p => addonId != Guesser.AddonId || GuesserEligible(p)) // 赌怪按阵营资格过滤
            .ToList();
        if (candidates.Count == 0) return;

        var pick = candidates[rng.Next(candidates.Count)];
        GrantAddon(pick, addonId);
        RpcSync.BroadcastAddonGrant(pick.PlayerId, addonId);
    }

    /// <summary>玩家是否符合赌怪分配资格（按阵营勾选）</summary>
    public static bool GuesserEligible(PlayerControl player)
    {
        var role = GetRole(player);
        var faction = GetFaction(player);
        return GuesserEligibleFor(faction, role?.IsHostileNeutral ?? false);
    }

    /// <summary>按阵营信息判定赌怪资格（分配阶段职业未应用时用重载）</summary>
    public static bool GuesserEligibleFor(byte roleId, PlayerControl player)
    {
        Faction faction;
        var hostile = false;

        var sample = RoleRegistry.FirstOrDefault(r => r.Id == roleId).Factory?.Invoke();
        if (sample != null)
        {
            faction = sample.Faction;
            hostile = sample.IsHostileNeutral;
        }
        else
        {
            faction = player.Data != null && player.Data.Role != null && player.Data.Role.IsImpostor
                ? Faction.Impostor
                : Faction.Crewmate;
        }

        return GuesserEligibleFor(faction, hostile);
    }

    private static bool GuesserEligibleFor(Faction faction, bool hostileNeutral)
    {
        return faction switch
        {
            Faction.Crewmate => CustomOptions.GuesserCrewmate.Value == 1,
            Faction.Impostor => CustomOptions.GuesserImpostor.Value == 1,
            Faction.Neutral => hostileNeutral
                ? CustomOptions.GuesserKnifeNeutral.Value == 1
                : CustomOptions.GuesserNoKnifeNeutral.Value == 1,
            _ => false,
        };
    }

    /// <summary>按待分配职业 ID 判定是否内鬼阵营（分配阶段职业未应用，供叛徒过滤）</summary>
    private static bool IsImpostorFactionFor(byte roleId, PlayerControl player)
    {
        var sample = RoleRegistry.FirstOrDefault(r => r.Id == roleId).Factory?.Invoke();
        if (sample != null) return sample.Faction == Faction.Impostor;
        return player.Data != null && player.Data.Role != null && player.Data.Role.IsImpostor;
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
        FakeImpostors.Clear();
        DeathTracker.Clear();
        GameArchive.ArchiveAndReset();
        MoonRunner.ResetStatics();
        Balancer.ResetStatics();
        PrivateTag.ClearAll();
        MineVisuals.Clear();
    }
}
