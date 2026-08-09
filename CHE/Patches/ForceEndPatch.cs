using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace CHE.Patches;

/// <summary>
/// 强制结束游戏（参考 TOHE 的 /end 命令）：
/// - 对局中聊天栏输入 /end（仅主机）
/// - 对局中按 ALT+F4（仅主机）：通过 Application.wantsToQuit 拦截退出请求，改为结束本局
/// </summary>
public static class ForceEndPatch
{
    /// <summary>在 Plugin.Load 中注册退出拦截</summary>
    public static void Init()
    {
        // IL2CPP 委托需要经 System.Func 中转（Il2CppInterop 官方写法）
        Application.wantsToQuit += (Il2CppSystem.Func<bool>)(System.Func<bool>)OnWantsToQuit;
    }

    private static bool OnWantsToQuit()
    {
        // 对局中且是主机：取消退出，强制结束本局
        if (AmongUsClient.Instance != null
            && AmongUsClient.Instance.AmHost
            && AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Started)
        {
            ForceEnd();
            return false;
        }
        return true;
    }

    private static void ForceEnd()
    {
        if (GameManager.Instance == null) return;
        CHEPlugin.Log.LogInfo("[CHE] 强制结束游戏（/end 或 ALT+F4）");
        GameManager.Instance.RpcEndGame(GameOverReason.ImpostorDisconnect, false);
    }

    /// <summary>聊天命令：/end 强制结束（仅主机、仅对局中）</summary>
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
    public static class ChatCommandPatch
    {
        public static bool Prefix(PlayerControl __instance, string chatText)
        {
            if (string.IsNullOrEmpty(chatText)) return true;
            if (!__instance.AmOwner) return true;

            var text = chatText.Trim();
            if (text.Equals("/end", System.StringComparison.OrdinalIgnoreCase))
                return HandleEnd();
            if (text.StartsWith("/start", System.StringComparison.OrdinalIgnoreCase))
                return HandleStart(text);

            return true;
        }

        /// <summary>/end：强制结束对局（仅主机、对局中）</summary>
        private static bool HandleEnd()
        {
            if (AmongUsClient.Instance == null
                || !AmongUsClient.Instance.AmHost
                || AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started)
            {
                CHEPlugin.Log.LogWarning("[CHE] /end 仅主机在对局中可用");
                return false; // 拦截命令，不发送到聊天
            }

            ForceEnd();
            return false;
        }

        /// <summary>/start [秒数]：以指定倒计时开始游戏（仅主机、大厅中）</summary>
        private static bool HandleStart(string text)
        {
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            {
                CHEPlugin.Log.LogWarning("[CHE] /start 仅主机可用");
                return false;
            }

            var sec = StartAnyCountPatch.DefaultCountdown;
            var parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
                int.TryParse(parts[1], out sec);
            sec = UnityEngine.Mathf.Clamp(sec, 0, 99);

            var manager = GameStartManager.Instance;
            if (manager == null)
            {
                CHEPlugin.Log.LogWarning("[CHE] /start 仅在大厅中可用");
                return false;
            }

            CHEPlugin.Log.LogInfo($"[CHE] /start：{sec} 秒倒计时开始游戏");
            StartAnyCountPatch.StartCountdown(manager, sec);
            return false; // 拦截命令，不发送到聊天
        }
    }
}
