using TAHS.Modules;
using HarmonyLib;

namespace TAHS.Patches;

/// <summary>驱动公告计时（/s 醒目消息自动消失）</summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class AnnouncementPatch
{
    public static void Postfix() => Announcement.Tick();
}
