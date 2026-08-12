using Object = UnityEngine.Object;
using TAHS.Modules;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace TAHS.Patches;

/// <summary>
/// 自动开始游戏（参考 TOHE/EHR，仅主机生效）：
/// 大厅中人数达到「自动开始最少人数」时自动进入倒计时（秒数可配）。
/// </summary>
[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
public static class AutoStartPatch
{
    private static float _checkTimer;

    public static void Postfix(GameStartManager __instance)
    {
        if (CustomOptions.AutoStart.Value != 1) return;
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
        if (AmongUsClient.Instance.NetworkMode != NetworkModes.LocalGame && GameData.Instance == null) return;
        if (__instance.startState == GameStartManager.StartingStates.Countdown) return; // 已在倒计时

        _checkTimer += Time.deltaTime;
        if (_checkTimer < 1f) return;
        _checkTimer = 0f;

        var players = GameData.Instance != null ? GameData.Instance.PlayerCount : 1;
        if (players < CustomOptions.AutoStartMinPlayers.Value) return;

        TAHSPlugin.Log.LogInfo($"[TAHS] 自动开始：人数 {players} 达标，{CustomOptions.AutoStartCountdown.Value} 秒倒计时");
        StartAnyCountPatch.StartCountdown(__instance, CustomOptions.AutoStartCountdown.Value);
    }
}

/// <summary>
/// 自动返回等待大厅（参考 TONE，仅主机生效，各端跟随）：
/// 对局结束进入结算画面后，等待「返回等待时间」秒，自动触发原版的"再来一局"
/// （EndGameNavigation.NextGame），房间保留、全员回到大厅。
/// </summary>
public static class AutoReturnLobby
{
    private static float _timer;
    private static bool _triggered;

    /// <summary>每帧驱动（AnnouncementPatch 调用）</summary>
    public static void Tick()
    {
        if (CustomOptions.AutoReturnLobby.Value != 1
            || AmongUsClient.Instance == null
            || !AmongUsClient.Instance.AmHost
            || AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Ended)
        {
            _timer = 0f;
            _triggered = false;
            return;
        }

        if (_triggered) return;

        _timer += Time.deltaTime;
        if (_timer < CustomOptions.AutoReturnDelay.Value) return;

        var nav = Object.FindFirstObjectByType<EndGameNavigation>();
        if (nav == null) return; // 结算画面未就绪，继续等

        _triggered = true;
        TAHSPlugin.Log.LogInfo("[TAHS] 自动返回等待大厅");
        nav.NextGame();
    }
}
