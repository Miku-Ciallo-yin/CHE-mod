namespace TAHS.Modules;

/// <summary>游戏内聊天栏输出（本地警告样式，不会发送给其他玩家）</summary>
public static class ChatHelper
{
    /// <summary>聊天字数限制（留余量）</summary>
    private const int MaxMessageLength = 90;

    public static void Show(string message)
    {
        var hud = DestroyableSingleton<HudManager>.Instance;
        if (hud == null || hud.Chat == null) return;
        hud.Chat.AddChatWarning(message);
    }

    /// <summary>
    /// 只给指定玩家显示消息（主机调用）：
    /// 模组端走本地警告通道（RPC 223），无模组端用定向 SendChat（仅对方客户端收到）
    /// </summary>
    public static void ShowPrivate(PlayerControl player, string message)
    {
        if (player == null) return;

        if (PlayerIdManager.IsModdedClient(player))
        {
            RpcSync.SendShowMessage(player.OwnerId, message);
            return;
        }

        // 定向 SendChat：只有该玩家的客户端收到这条聊天（无模组端原生可见）
        var writer = AmongUsClient.Instance.StartRpcImmediately(
            player.NetId, (byte)RpcCalls.SendChat, Hazel.SendOption.Reliable, player.OwnerId);
        writer.Write(message);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    /// <summary>
    /// 多行合并为一条气泡显示；总长度超过聊天字数限制时按行拆分为多条。
    /// </summary>
    public static void ShowMany(IEnumerable<string> lines)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var line in lines)
        {
            // 当前行加入后会超限：先把已积累的发出去
            if (sb.Length > 0 && sb.Length + line.Length + 1 > MaxMessageLength)
            {
                Show(sb.ToString());
                sb.Clear();
            }

            if (sb.Length > 0) sb.Append('\n');
            sb.Append(line);
        }

        if (sb.Length > 0)
            Show(sb.ToString());
    }

    /// <summary>
    /// 多行私信（主机调用）：与 <see cref="ShowMany"/> 同样的拆分规则，
    /// 逐条定向发送，仅指定玩家可见（用于主机代收无模组端的信息类指令）。
    /// </summary>
    public static void ShowPrivateMany(PlayerControl player, IEnumerable<string> lines)
    {
        if (player == null) return;

        foreach (var msg in SplitLines(lines))
            ShowPrivate(player, msg);
    }

    /// <summary>
    /// 限流版多行私信（主机调用）：逐条经 RateLimiter 分帧发送，
    /// 用于开局等对多人群发的场景，防止打包成超大包被拦截（8+ 人黑屏根因）。
    /// </summary>
    public static void ShowPrivateManyThrottled(PlayerControl player, IEnumerable<string> lines)
    {
        if (player == null) return;

        foreach (var msg in SplitLines(lines))
        {
            var m = msg;
            RateLimiter.Enqueue(() => ShowPrivate(player, m));
        }
    }

    /// <summary>按聊天字数限制把多行合并/拆分为若干条消息</summary>
    private static List<string> SplitLines(IEnumerable<string> lines)
    {
        var result = new List<string>();
        var sb = new System.Text.StringBuilder();
        foreach (var line in lines)
        {
            if (sb.Length > 0 && sb.Length + line.Length + 1 > MaxMessageLength)
            {
                result.Add(sb.ToString());
                sb.Clear();
            }

            if (sb.Length > 0) sb.Append('\n');
            sb.Append(line);
        }

        if (sb.Length > 0)
            result.Add(sb.ToString());
        return result;
    }
}
