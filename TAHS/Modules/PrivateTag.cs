using Hazel;
using InnerNet;
using UnityEngine;

namespace TAHS.Modules;

/// <summary>
/// 私有名牌（参考 TONE 的定向改名技巧）：
/// 通过只发给指定客户端的 SetName RPC，让"只有某个玩家"看到某玩家的
/// 自定义名字颜色 / 名字下方标签——无模组客户端原生渲染，其他玩家完全看不到。
/// 全部操作仅主机执行，每 2 秒刷新一次防止被游戏同步重置。
/// </summary>
public static class PrivateTag
{
    /// <summary>（观看者 ClientId, 目标 PlayerId） -> 标签内容（含富文本）</summary>
    private static readonly Dictionary<(int Viewer, byte Player), string> _tags = new();

    /// <summary>（观看者 ClientId, 目标 PlayerId） -> 名字颜色（如 #FF1919）</summary>
    private static readonly Dictionary<(int Viewer, byte Player), string> _colors = new();

    private static float _refreshTimer;
    private const float RefreshInterval = 2f;

    private static bool IsHost => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;

    /// <summary>主机：让 viewer（客户端）看到 player 名字下方的标签</summary>
    public static void SetTag(int viewerClientId, PlayerControl player, string tagLine)
    {
        if (!IsHost || player == null) return;
        _tags[(viewerClientId, player.PlayerId)] = tagLine;
        Apply(viewerClientId, player);
    }

    /// <summary>主机：移除指定观看者对某玩家的标签（恢复原名）</summary>
    public static void RemoveTag(int viewerClientId, PlayerControl player)
    {
        if (!IsHost || player == null) return;
        _tags.Remove((viewerClientId, player.PlayerId));
        Apply(viewerClientId, player);
    }

    /// <summary>主机：让 viewer（客户端）看到 player 的名字变为指定颜色</summary>
    public static void SetColor(int viewerClientId, PlayerControl player, string colorHex)
    {
        if (!IsHost || player == null) return;
        _colors[(viewerClientId, player.PlayerId)] = colorHex;
        Apply(viewerClientId, player);
    }

    /// <summary>主机：移除指定观看者对某玩家的名字颜色（恢复原名色）</summary>
    public static void RemoveColor(int viewerClientId, PlayerControl player)
    {
        if (!IsHost || player == null) return;
        _colors.Remove((viewerClientId, player.PlayerId));
        Apply(viewerClientId, player);
    }

    /// <summary>查看某观看者当前对某玩家应用的名字颜色（无则 null）</summary>
    public static string? GetColor(int viewerClientId, byte playerId)
    {
        return _colors.TryGetValue((viewerClientId, playerId), out var color) ? color : null;
    }

    /// <summary>当前全部已应用的名字颜色对（供驱动方做差量移除）</summary>
    public static IEnumerable<(int Viewer, byte Player)> ColorPairs => _colors.Keys;

    /// <summary>清空全部标签/颜色并恢复所有名字（对局重置时调用）</summary>
    public static void ClearAll()
    {
        if (!IsHost) { _tags.Clear(); _colors.Clear(); return; }

        var keys = new HashSet<(int, byte)>(_tags.Keys);
        keys.UnionWith(_colors.Keys);
        foreach (var (viewer, playerId) in keys)
        {
            var player = FindPlayer(playerId);
            if (player != null)
            {
                _tags.Remove((viewer, playerId));
                _colors.Remove((viewer, playerId));
                Apply(viewer, player);
            }
        }
        _tags.Clear();
        _colors.Clear();
    }

    /// <summary>每帧驱动：定期刷新标签/颜色防止被游戏同步覆盖</summary>
    public static void Tick()
    {
        if (!IsHost || (_tags.Count == 0 && _colors.Count == 0)) return;

        _refreshTimer -= Time.deltaTime;
        if (_refreshTimer > 0f) return;
        _refreshTimer = RefreshInterval;

        var keys = new HashSet<(int, byte)>(_tags.Keys);
        keys.UnionWith(_colors.Keys);
        foreach (var (viewer, playerId) in keys)
        {
            var player = FindPlayer(playerId);
            if (player != null) Apply(viewer, player);
        }
    }

    /// <summary>定向改名：按当前登记的颜色与标签合成名字</summary>
    private static void Apply(int viewerClientId, PlayerControl player)
    {
        var baseName = player.Data?.PlayerName;
        if (string.IsNullOrEmpty(baseName)) return;
        // 防止嵌套叠加：剥离已有标签行、颜色标记与首刀保护的十字前缀
        var clean = StripColor(baseName.Split('\n')[0]).TrimStart('＋');

        var name = _colors.TryGetValue((viewerClientId, player.PlayerId), out var color)
            ? $"<color={color}>{clean}</color>"
            : clean;
        // 首刀保护的蓝色十字前缀（全员可见的一部分，定向合成时保留）
        name = (FirstKillProtection.NamePrefixFor(player.PlayerId) ?? string.Empty) + name;
        if (_tags.TryGetValue((viewerClientId, player.PlayerId), out var tag))
            name = $"{name}\n<size=60%>{tag}</size>";

        var writer = AmongUsClient.Instance.StartRpcImmediately(
            player.NetId, (byte)RpcCalls.SetName, SendOption.Reliable, viewerClientId);
        writer.Write(name);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    private static string StripColor(string text)
    {
        return System.Text.RegularExpressions.Regex.Replace(text, "</?color[^>]*>", string.Empty);
    }

    private static PlayerControl? FindPlayer(byte playerId)
    {
        return PlayerControl.AllPlayerControls.ToArray()
            .FirstOrDefault(p => p != null && p.PlayerId == playerId);
    }
}
