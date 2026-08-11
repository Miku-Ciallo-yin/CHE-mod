namespace TAHS.Modules;

/// <summary>
/// 玩家 ID 分配（主机权威）：房主 ID 为 0，其余按进入房间顺序从 1 递增。
/// 分配结果经 RPC（CallId 220）广播给模组端，用于名牌 [id] 前缀显示。
/// 无模组客户端不显示 ID（固有降级，不影响游玩）。
/// </summary>
public static class PlayerIdManager
{
    /// <summary>ClientId -> 玩家 ID</summary>
    private static readonly Dictionary<int, int> _ids = new();

    /// <summary>协管 ClientId 集合（主机随 ID 映射广播）</summary>
    private static readonly HashSet<int> _mods = new();

    /// <summary>主机：有玩家进房时调用（含房主自己的兜底分配）</summary>
    public static void OnPlayerJoined(int clientId)
    {
        var client = AmongUsClient.Instance;
        if (client == null || !client.AmHost) return;

        // 房主固定为 0（兜底：任何人进房时先确保房主已分配）
        if (!_ids.ContainsKey(client.ClientId))
            _ids[client.ClientId] = 0;

        if (!_ids.ContainsKey(clientId))
        {
            _ids[clientId] = NextId();
            TAHSPlugin.Log.LogInfo($"[TAHS] 玩家 {clientId} 分配 ID {_ids[clientId]}");
        }

        RpcSync.BroadcastPlayerIds(_ids);
    }

    /// <summary>取最小未占用的 ID（从 1 起）</summary>
    private static int NextId()
    {
        var id = 1;
        while (_ids.ContainsValue(id)) id++;
        return id;
    }

    /// <summary>客户端：应用主机广播的 ID 映射</summary>
    public static void Set(int clientId, int id) => _ids[clientId] = id;

    /// <summary>查询玩家 ID，未分配返回 null</summary>
    public static int? GetId(PlayerControl player)
    {
        if (player == null) return null;
        return _ids.TryGetValue(player.OwnerId, out var id) ? id : null;
    }

    /// <summary>按 ID 查找玩家（用于 /bt 等指令），未找到返回 null</summary>
    public static PlayerControl? GetPlayerById(int id)
    {
        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (player == null) continue;
            if (GetId(player) == id) return player;
        }
        return null;
    }

    /// <summary>客户端：标记协管身份（主机广播）</summary>
    public static void SetModerator(int clientId, bool isMod)
    {
        if (isMod) _mods.Add(clientId);
        else _mods.Remove(clientId);
    }

    /// <summary>玩家是否是协管（主机查名单 / 模组端查广播标记）</summary>
    public static bool IsModerator(PlayerControl player)
    {
        if (player == null) return false;
        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            return ModeratorManager.IsModerator(player);
        return _mods.Contains(player.OwnerId);
    }

    /// <summary>清空（返回主菜单时调用，下一局重新分配）</summary>
    public static void Clear()
    {
        _ids.Clear();
        _mods.Clear();
    }
}
