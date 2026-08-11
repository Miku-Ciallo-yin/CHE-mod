namespace TAHS.Modules;

/// <summary>游戏内聊天栏输出</summary>
public static class ChatHelper
{
    /// <summary>聊天字数限制（留余量）</summary>
    private const int MaxMessageLength = 90;

    /// <summary>本机警告样式显示（其他玩家不可见）</summary>
    public static void Show(string message)
    {
        var hud = DestroyableSingleton<HudManager>.Instance;
        if (hud == null || hud.Chat == null) return;
        hud.Chat.AddChatWarning(message);
    }

    /// <summary>官方聊天广播（主机调用）：所有玩家包括无模组客户端都能看到</summary>
    public static void Broadcast(string message)
    {
        if (PlayerControl.LocalPlayer != null)
            PlayerControl.LocalPlayer.RpcSendChat(message);
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
}
