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

    /// <summary>强制结束本局（/end、ALT+F4、协管请求共用）</summary>
    public static void ForceEnd()
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
            if (text.Equals("/id", System.StringComparison.OrdinalIgnoreCase))
            {
                ShowPlayerIds();
                return false;
            }
            if (text.StartsWith("/addmod", System.StringComparison.OrdinalIgnoreCase))
                return HandleAddMod(text);

            // 其余以 / 开头的输入一律隐藏（不广播给其他玩家，防指令泄露）
            if (text.StartsWith('/'))
                return false;

            return true;
        }

        /// <summary>是否房主</summary>
        private static bool IsHost()
        {
            return AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
        }

        /// <summary>是否可使用房主指令：房主，或协管名单开启且在名单内</summary>
        private static bool CanUseHostCommands()
        {
            if (IsHost()) return true;
            var local = PlayerControl.LocalPlayer;
            return local != null
                   && Modules.ModeratorManager.IsEnabled
                   && Modules.ModeratorManager.IsModerator(local);
        }

        /// <summary>/addmod id：把该 ID 玩家加入协管名单（仅房主）</summary>
        private static bool HandleAddMod(string text)
        {
            var show = Modules.ChatHelper.Show;

            if (!IsHost())
            {
                show("[CHE] /addmod 仅房主可用");
                return false;
            }

            var parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !int.TryParse(parts[1], out var id))
            {
                show("[CHE] 用法：/addmod <玩家ID>，先用 /id 查看");
                return false;
            }

            var target = Modules.PlayerIdManager.GetPlayerById(id);
            if (target == null || target.Data == null)
            {
                show($"[CHE] 未找到 ID 为 {id} 的玩家");
                return false;
            }

            var code = target.Data.FriendCode;
            if (string.IsNullOrEmpty(code))
            {
                show($"[CHE] 无法获取 {target.Data.PlayerName} 的好友代码");
                return false;
            }

            if (Modules.ModeratorManager.Add(code))
                show($"[CHE] 已将 {target.Data.PlayerName}（{code}）加入协管名单");
            else
                show($"[CHE] {target.Data.PlayerName}（{code}）已在协管名单中");
            return false;
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

        /// <summary>/id：输出所有玩家的名字及对应 ID（仅本机可见）</summary>
        private static void ShowPlayerIds()
        {
            var lines = new List<string> { "<color=#4FC3F7>===== 玩家 ID 列表 =====</color>" };
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null) continue;
                var id = Modules.PlayerIdManager.GetId(player);
                var idText = id.HasValue ? id.Value.ToString() : "?";
                var self = player.AmOwner ? "（你）" : string.Empty;
                lines.Add($"[{idText}] {player.Data.PlayerName}{self}");
            }
            Modules.ChatHelper.ShowMany(lines);
        }

        /// <summary>/help：输出全部指令及功能（合并为一条气泡，超长自动拆分）</summary>
        private static void ShowHelp()
        {
            Modules.ChatHelper.ShowMany(new[]
            {
                "<color=#4FC3F7>===== CHE 指令帮助 =====</color>",
                "/help — 显示本帮助",
                "/id — 显示所有玩家的名字及其对应 ID",
                "/bt <玩家ID> <职业名> — 猜测该玩家的职业（需猜测权限，如 /bt 2 佃农）",
                "/start [秒数] — 以指定倒计时开始游戏（默认5秒，仅房主/协管）",
                "/end — 强制结束对局返回大厅（仅房主/协管，对局中）",
                "/dump — 导出日志到桌面并显示最近日志（仅房主）",
                "/addmod <玩家ID> — 将该玩家加入协管名单（仅房主）",
                "快捷键：ALT+F4 — 强制结束对局（仅房主/对局中）",
            });
        }

        /// <summary>/end：强制结束对局（房主直接执行；协管经 RPC 由主机执行）</summary>
        private static bool HandleEnd()
        {
            if (AmongUsClient.Instance == null
                || AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started)
            {
                Modules.ChatHelper.Show("[CHE] /end 仅对局中可用");
                return false;
            }
            if (!CanUseHostCommands())
            {
                Modules.ChatHelper.Show("[CHE] /end 仅房主或协管可用");
                return false;
            }

            if (IsHost()) ForceEnd();
            else Modules.RpcSync.SendModCommand(2, 0); // 协管：请求主机结束
            return false;
        }

        /// <summary>/start [秒数]：以指定倒计时开始游戏（房主直接执行；协管经 RPC 由主机执行）</summary>
        private static bool HandleStart(string text)
        {
            if (!CanUseHostCommands())
            {
                Modules.ChatHelper.Show("[CHE] /start 仅房主或协管可用");
                return false;
            }

            var sec = StartAnyCountPatch.DefaultCountdown;
            var parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
                int.TryParse(parts[1], out sec);
            sec = UnityEngine.Mathf.Clamp(sec, 0, 99);

            if (IsHost())
            {
                var manager = GameStartManager.Instance;
                if (manager == null)
                {
                    CHEPlugin.Log.LogWarning("[CHE] /start 仅在大厅中可用");
                    return false;
                }
                CHEPlugin.Log.LogInfo($"[CHE] /start：{sec} 秒倒计时开始游戏");
                StartAnyCountPatch.StartCountdown(manager, sec);
            }
            else
            {
                // 协管：请求主机开始倒计时
                Modules.RpcSync.SendModCommand(1, sec);
                Modules.ChatHelper.Show($"[CHE] 已请求主机开始游戏（{sec} 秒倒计时）");
            }
            return false;
        }
    }
}
