namespace CHE.Modules;

/// <summary>游戏内聊天栏输出（本地警告样式，不会发送给其他玩家）</summary>
public static class ChatHelper
{
    public static void Show(string message)
    {
        var hud = DestroyableSingleton<HudManager>.Instance;
        if (hud == null || hud.Chat == null) return;
        hud.Chat.AddChatWarning(message);
    }
}
