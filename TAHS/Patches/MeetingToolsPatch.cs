using System.Reflection;
using TAHS.Modules;
using HarmonyLib;
using UnityEngine;

namespace TAHS.Patches;

/// <summary>
/// Shift+M 跳过会议（仅房主）：强制全员弃票，会议按原版流程立即结束。
/// </summary>
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
public static class SkipMeetingPatch
{
    public static void Postfix(MeetingHud __instance)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)) return;
        if (!Input.GetKeyDown(KeyCode.M)) return;

        __instance.ForceSkipAll();
        ChatHelper.Show("[TAHS] 已跳过本次会议（全员弃票）");
        TAHSPlugin.Log.LogInfo("[TAHS] 房主使用 Shift+M 跳过会议");
    }
}

/// <summary>
/// 会议中不播放击杀动画（赌怪赌杀等在会议内的击杀）：
/// 击杀闪屏动画会把会议界面顶掉，导致散会后黑屏/卡死。
/// </summary>
[HarmonyPatch]
public static class MeetingKillFlashBlock
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        return typeof(KillOverlay).GetMethods()
            .Where(m => m.Name == nameof(KillOverlay.ShowKillAnimation))
            .Cast<MethodBase>();
    }

    public static bool Prefix()
    {
        return MeetingHud.Instance == null; // 仅在会议中时拦截
    }
}
