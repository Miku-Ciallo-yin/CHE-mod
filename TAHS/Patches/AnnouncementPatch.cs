using TAHS.Modules;
using HarmonyLib;

namespace TAHS.Patches;

/// <summary>驱动公告计时（/s 醒目消息自动消失）、私有名牌刷新、叛徒互认红名与自动返回大厅</summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class AnnouncementPatch
{
    public static void Postfix()
    {
        Announcement.Tick();
        PrivateTag.Tick();
        TraitorNameColors.Tick();
        AutoReturnLobby.Tick();
        MineVisuals.Tick();
    }
}
