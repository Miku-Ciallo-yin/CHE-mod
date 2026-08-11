using TAHS.Modules;
using HarmonyLib;
using InnerNet;

namespace TAHS.Patches;

/// <summary>
/// 玩家 ID 分配钩子：主机在玩家进房时分配（房主为 0，按进房顺序递增），
/// 返回主菜单时清空等待下一局。
/// </summary>
public static class PlayerIdPatch
{
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerJoined)), HarmonyPostfix]
    public static void OnPlayerJoinedPostfix(ClientData client)
    {
        if (client == null) return;
        PlayerIdManager.OnPlayerJoined(client.Id);
    }

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start)), HarmonyPostfix]
    public static void MainMenuStartPostfix()
    {
        PlayerIdManager.Clear();
    }
}
