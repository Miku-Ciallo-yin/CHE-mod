namespace TAHS.Modules;

/// <summary>
/// 选秀分配（参考 TONE 的 Draft 模式）：大厅中房主/协管用 /ds 开始选秀——
/// 每名玩家 privately 收到一个同阵营的职业池（默认 3 个），用 /ds &lt;序号&gt; 选择；
/// 开局时按选择分配职业（未选择的随机取池内一个），取代随机分配。
/// 职业池经定向私信下发，无模组端同样可见可选（主机代收指令）。
/// </summary>
public static class DraftManager
{
    /// <summary>每池职业数</summary>
    public const int PoolSize = 3;

    /// <summary>playerId -> 职业池（角色 ID 列表）</summary>
    private static readonly Dictionary<byte, List<byte>> _pools = new();

    /// <summary>playerId -> 已选职业 ID</summary>
    private static readonly Dictionary<byte, byte> _picks = new();

    /// <summary>选秀是否已开始（有池子即为进行中）</summary>
    public static bool Active => _pools.Count > 0;

    /// <summary>主机：开始选秀（大厅中）——构建并私发职业池</summary>
    public static void Start()
    {
        var players = PlayerControl.AllPlayerControls.ToArray()
            .Where(p => p != null && p.Data != null)
            .OrderBy(_ => Guid.NewGuid())
            .ToList();
        if (players.Count == 0) return;

        // 职业池底：所有生成概率 > 0 的职业，按"人数"展开
        var deck = new List<byte>();
        foreach (var (id, name, _) in Roles.CustomRoleManager.GetRegisteredRoles())
        {
            var chance = CustomOptions.GetRoleChance(id);
            if (chance <= 0) continue;
            for (var i = 0; i < CustomOptions.GetRoleCount(id); i++)
                deck.Add(id);
        }
        if (deck.Count < players.Count)
        {
            Announcement.Broadcast(true, "选秀失败：可用职业数量不足");
            return;
        }

        _pools.Clear();
        _picks.Clear();
        var rng = new Random();

        // 保证至少 NumImpostors 名玩家的池子全是内鬼职业（否则可能零内鬼开局直接结束）
        var impostorSlots = GameOptionsManager.Instance?.CurrentGameOptions?.GetInt(
            AmongUs.GameOptions.Int32OptionNames.NumImpostors) ?? 1;

        for (var i = 0; i < players.Count; i++)
        {
            // 本池阵营：前 impostorSlots 名强制内鬼（参考 TONE 同阵营池）
            var forceImpostor = i < impostorSlots
                                && deck.Any(r => IsFaction(r, Roles.Faction.Impostor));

            // 非强制内鬼时随机选一个底牌中存在的阵营
            Roles.Faction? targetFaction = null;
            if (forceImpostor)
            {
                targetFaction = Roles.Faction.Impostor;
            }
            else
            {
                var factions = deck
                    .Select(r => Roles.CustomRoleManager.GetRoleSamples().FirstOrDefault(s => s.Id == r).Sample?.Faction)
                    .Where(f => f.HasValue)
                    .Select(f => f!.Value)
                    .Distinct()
                    .ToList();
                if (factions.Count > 0)
                    targetFaction = factions[rng.Next(factions.Count)];
            }

            var pool = new List<byte>();
            if (targetFaction.HasValue)
            {
                var inFaction = deck.Where(r => IsFaction(r, targetFaction.Value)).ToList();
                Shuffle(inFaction);
                foreach (var id in inFaction)
                {
                    if (pool.Count >= PoolSize) break;
                    if (pool.Contains(id)) continue;
                    pool.Add(id);
                    deck.Remove(id);
                }
            }

            // 不足则从剩余底牌任意补齐
            while (pool.Count < PoolSize && deck.Count > 0)
            {
                var pick = deck[rng.Next(deck.Count)];
                deck.Remove(pick);
                if (!pool.Contains(pick)) pool.Add(pick);
            }

            if (pool.Count > 0)
            {
                _pools[players[i].PlayerId] = pool;
                ShowPool(players[i]);
            }
        }

        Announcement.Broadcast(true, "选秀开始：职业池已私聊下发，用 /ds <序号> 选择你的职业");
        TAHSPlugin.Log.LogInfo($"[TAHS] 选秀开始：{players.Count} 名玩家，每池 {PoolSize} 个职业");
    }

    /// <summary>私聊展示玩家的职业池</summary>
    public static void ShowPool(PlayerControl player)
    {
        if (!_pools.TryGetValue(player.PlayerId, out var pool)) return;

        var lines = new List<string> { "<color=#4FC3F7>===== 你的选秀池 =====</color>" };
        for (var i = 0; i < pool.Count; i++)
        {
            var sample = Roles.CustomRoleManager.GetRoleSamples()
                .FirstOrDefault(s => s.Id == pool[i]).Sample;
            if (sample == null) continue;
            lines.Add($"{i + 1}. {sample.Name}（{sample.Faction}）— {sample.Description}");
        }
        lines.Add("输入 /ds <序号> 选择（如 /ds 1）");
        ChatHelper.ShowPrivateMany(player, lines);
    }

    /// <summary>玩家选择池内职业（index 从 1 起）</summary>
    public static void Pick(PlayerControl player, int index)
    {
        System.Action<string> tell = msg => ChatHelper.ShowPrivate(player, msg);

        if (!Active || !_pools.TryGetValue(player.PlayerId, out var pool))
        {
            tell("[TAHS] 当前没有进行中的选秀");
            return;
        }
        if (index < 1 || index > pool.Count)
        {
            tell($"[TAHS] 序号超出范围（1~{pool.Count}）");
            return;
        }

        _picks[player.PlayerId] = pool[index - 1];
        var sample = Roles.CustomRoleManager.GetRoleSamples()
            .FirstOrDefault(s => s.Id == pool[index - 1]).Sample;
        tell($"[TAHS] 你选择了：{sample?.Name ?? "?"}");
        TAHSPlugin.Log.LogInfo($"[TAHS] {player.Data?.PlayerName} 选秀选择了 {sample?.Name}");
    }

    /// <summary>
    /// 开局应用选秀结果（AssignRoles 调用）：已选的按选择分配，未选的随机取池内一个，
    /// 选秀后加入的玩家随机取任意已启用职业。返回 false 表示无选秀（走随机分配）。
    /// </summary>
    public static bool TryApply(List<PlayerControl> players, List<(byte PlayerId, byte RoleId)> assignments)
    {
        if (!Active) return false;

        var rng = new Random();
        foreach (var p in players)
        {
            byte roleId;
            if (!_picks.TryGetValue(p.PlayerId, out roleId))
            {
                if (_pools.TryGetValue(p.PlayerId, out var pool) && pool.Count > 0)
                    roleId = pool[rng.Next(pool.Count)];
                else
                {
                    // 选秀后加入：随机取一个已启用职业
                    var enabled = Roles.CustomRoleManager.GetRegisteredRoles()
                        .Where(r => CustomOptions.GetRoleChance(r.Id) > 0)
                        .Select(r => r.Id).ToList();
                    if (enabled.Count == 0) continue;
                    roleId = enabled[rng.Next(enabled.Count)];
                }
            }
            assignments.Add((p.PlayerId, roleId));
        }

        TAHSPlugin.Log.LogInfo($"[TAHS] 已按选秀结果分配 {assignments.Count} 个职业");
        Clear();
        return true;
    }

    public static void Clear()
    {
        _pools.Clear();
        _picks.Clear();
    }

    private static bool IsFaction(byte roleId, Roles.Faction faction)
    {
        var sample = Roles.CustomRoleManager.GetRoleSamples().FirstOrDefault(s => s.Id == roleId).Sample;
        return sample != null && sample.Faction == faction;
    }

    private static void Shuffle(List<byte> list)
    {
        var rng = new Random();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
