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
            if (text.Equals("/dump", System.StringComparison.OrdinalIgnoreCase))
            {
                if (!IsHost())
                {
                    Modules.ChatHelper.Show("[CHE] /dump 仅房主可用");
                    return false;
                }
                Modules.LogDumper.Dump();
                return false; // 拦截命令，不发送到聊天
            }
            if (text.Equals("/help", System.StringComparison.OrdinalIgnoreCase))
            {
                ShowHelp();
                return false;
            }
            if (text.StartsWith("/bt", System.StringComparison.OrdinalIgnoreCase))
                return HandleBet(text);

            return true;
        }

        /// <summary>/bt id 职业：猜测某玩家的职业（参考 TONE），需有猜测权限</summary>
        private static bool HandleBet(string text)
        {
            var show = Modules.ChatHelper.Show;

            if (AmongUsClient.Instance == null
                || AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started)
            {
                show("[CHE] /bt 仅对局中可用");
                return false;
            }

            var local = PlayerControl.LocalPlayer;
            if (local == null || !Patches.GuesserPatch.CanGuess(local))
            {
                show("[CHE] 你没有猜测权限（需要赌怪附加职业或猜测模式放行你的阵营）");
                return false;
            }

            var parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || !int.TryParse(parts[1], out var id))
            {
                show("[CHE] 用法：/bt <玩家ID> <职业名>，如 /bt 2 佃农");
                return false;
            }

            var roleName = string.Join(' ', parts.Skip(2));
            var entry = Patches.GuesserPatch.GetEnabledEntries()
                .FirstOrDefault(e => e.Name.Equals(roleName, System.StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                var valid = string.Join("、", Patches.GuesserPatch.GetEnabledEntries().Select(e => e.Name));
                show($"[CHE] 未知职业：{roleName}。可猜测：{valid}");
                return false;
            }

            var target = Modules.PlayerIdManager.GetPlayerById(id);
            if (target == null)
            {
                show($"[CHE] 未找到 ID 为 {id} 的玩家");
                return false;
            }

            show($"[CHE] 你猜测 [{id}] {target.Data?.PlayerName} 是 {entry.Name}，结果即将揭晓…");
            Patches.GuesserPatch.RequestGuess(local, target, entry);
            return false; // 拦截命令，不发送到聊天
        }

        /// <summary>是否房主</summary>
        private static bool IsHost()
        {
            return AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
        }

        /// <summary>/help：输出全部指令及功能（合并为一条气泡，超长自动拆分）</summary>
        private static void ShowHelp()
        {
            Modules.ChatHelper.ShowMany(new[]
            {
                "<color=#4FC3F7>===== CHE 指令帮助 =====</color>",
                "/help — 显示本帮助",
                "/bt <玩家ID> <职业名> — 猜测该玩家的职业（需猜测权限，如 /bt 2 佃农）",
                "/start [秒数] — 以指定倒计时开始游戏（默认5秒，仅房主）",
                "/end — 强制结束对局返回大厅（仅房主/对局中）",
                "/dump — 导出日志到桌面并显示最近日志（仅房主）",
                "快捷键：ALT+F4 — 强制结束对局（仅房主/对局中）",
            });
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
