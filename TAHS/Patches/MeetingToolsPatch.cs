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
/// Shift+M+回车 强制召开/结束会议（仅房主）：
/// 对局中无会议时强制召开紧急会议（RpcStartMeeting，无尸体）；
/// 会议中立即关闭会议（RpcClose，不走投票流程）。
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class ForceMeetingPatch
{
    public static void Postfix()
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
        if (AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started) return;
        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)) return;
        if (!Input.GetKey(KeyCode.M)) return;
        if (!Input.GetKeyDown(KeyCode.Return)) return;
        if (ExileController.Instance != null) return; // 放逐动画中不处理

        var meeting = MeetingHud.Instance;
        if (meeting != null)
        {
            meeting.RpcClose();
            ChatHelper.Show("[TAHS] 已强制结束会议");
            TAHSPlugin.Log.LogInfo("[TAHS] 房主使用 Shift+M+回车 强制结束会议");
            return;
        }

        var local = PlayerControl.LocalPlayer;
        if (local == null || local.Data == null) return;

        // 参考 TONE 的 NoCheckStartMeeting：先分配会议室座位再开会
        MeetingRoomManager.Instance.AssignSelf(local, null);
        HudManager.Instance.OpenMeetingRoom(local);
        local.RpcStartMeeting(null); // 紧急会议（无尸体）
        ChatHelper.Show("[TAHS] 已强制召开会议");
        TAHSPlugin.Log.LogInfo("[TAHS] 房主使用 Shift+M+回车 强制召开会议");
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
