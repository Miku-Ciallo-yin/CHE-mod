using Hazel;
using InnerNet;
using UnityEngine;

namespace TAHS.Modules;

/// <summary>
/// 私有名牌（参考 TONE 的定向改名技巧）：
/// 通过只发给指定客户端的 SetName RPC，让"只有某个玩家"看到某玩家名字下方的
/// 自定义标签——无模组客户端原生渲染，其他玩家完全看不到。
/// 全部操作仅主机执行，每 2 秒刷新一次防止被游戏同步重置。
/// </summary>
public static class PrivateTag
{
    /// <summary>（观看者 ClientId, 目标 PlayerId） -> 标签内容（含富文本）</summary>
    private static readonly Dictionary<(int Viewer, byte Player), string> _tags = new();
    private static float _refreshTimer;
    private const float RefreshInterval = 2f;

    private static bool IsHost => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;

    /// <summary>主机：让 viewer（客户端）看到 player 名字下方的标签</summary>
    public static void SetTag(int viewerClientId, PlayerControl player, string tagLine)
    {
        if (!IsHost || player == null) return;
        _tags[(viewerClientId, player.PlayerId)] = tagLine;
        Apply(viewerClientId, player, tagLine);
    }

    /// <summary>主机：移除指定观看者对某玩家的标签（恢复原名）</summary>
    public static void RemoveTag(int viewerClientId, PlayerControl player)
    {
        if (!IsHost || player == null) return;
        _tags.Remove((viewerClientId, player.PlayerId));
        Apply(viewerClientId, player, null);
    }

    /// <summary>清空全部标签并恢复所有名字（对局重置时调用）</summary>
    public static void ClearAll()
    {
        if (!IsHost) { _tags.Clear(); return; }
        foreach (var ((viewer, playerId), _) in _tags)
        {
            var player = FindPlayer(playerId);
            if (player != null) Apply(viewer, player, null);
        }
        _tags.Clear();
    }

    /// <summary>每帧驱动：定期刷新标签防止被游戏同步覆盖</summary>
    public static void Tick()
    {
        if (!IsHost || _tags.Count == 0) return;

        _refreshTimer -= Time.deltaTime;
        if (_refreshTimer > 0f) return;
        _refreshTimer = RefreshInterval;

        foreach (var ((viewer, playerId), tag) in _tags)
        {
            var player = FindPlayer(playerId);
            if (player != null) Apply(viewer, player, tag);
        }
    }

    /// <summary>定向改名：tag 为 null 时恢复原名</summary>
    private static void Apply(int viewerClientId, PlayerControl player, string? tagLine)
    {
        var baseName = player.Data?.PlayerName;
        if (string.IsNullOrEmpty(baseName)) return;
        // 防止标签嵌套叠加：剥离已有标签行
        var clean = baseName.Split('\n')[0];

        var name = tagLine == null
            ? clean
            : $"{clean}\n<size=60%>{tagLine}</size>";

        var writer = AmongUsClient.Instance.StartRpcImmediately(
            player.NetId, (byte)RpcCalls.SetName, SendOption.Reliable, viewerClientId);
        writer.Write(name);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    private static PlayerControl? FindPlayer(byte playerId)
    {
        return PlayerControl.AllPlayerControls.ToArray()
            .FirstOrDefault(p => p != null && p.PlayerId == playerId);
    }
}
