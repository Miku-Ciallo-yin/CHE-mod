using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace TAHS.Patches;

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
        var client = AmongUsClient.Instance;
        if (client == null || client.GameState != InnerNetClient.GameStates.Started)
            return true;

        // 主机：取消退出，强制结束本局
        if (client.AmHost)
        {
            ForceEnd();
            return false;
        }

        // 协管（权限开启时）：请求主机结束，本机不退出
        var local = PlayerControl.LocalPlayer;
        if (local != null
            && Modules.ModeratorManager.IsEnabled
            && Modules.ModeratorManager.IsModerator(local)
            && Modules.CustomOptions.ModAllowEnd.Value == 1)
        {
            Modules.RpcSync.SendModCommand(2, 0);
            return false;
        }

        return true;
    }

    /// <summary>强制结束本局（/end、ALT+F4、协管请求共用）</summary>
    public static void ForceEnd()
    {
        if (GameManager.Instance == null) return;
        TAHSPlugin.Log.LogInfo("[TAHS] 强制结束游戏（/end 或 ALT+F4）");
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
                    Modules.ChatHelper.Show("[TAHS] /dump 仅房主可用");
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
            if (text.Equals("/m", System.StringComparison.OrdinalIgnoreCase))
            {
                ShowMyRole();
                return false;
            }
            if (text.Equals("/r", System.StringComparison.OrdinalIgnoreCase))
            {
                ShowEnabledRoles();
                return false;
            }
            if (text.Equals("/d", System.StringComparison.OrdinalIgnoreCase))
            {
                ShowMyKiller();
                return false;
            }
            if (text.Equals("/l", System.StringComparison.OrdinalIgnoreCase))
            {
                Modules.GameArchive.ShowLast();
                return false;
            }
            if (text.Equals("/kc", System.StringComparison.OrdinalIgnoreCase))
            {
                ShowAliveCounts();
                return false;
            }
            if (text.StartsWith("/addmod", System.StringComparison.OrdinalIgnoreCase))
                return HandleAddMod(text);
            if (text.Equals("/s", System.StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("/s ", System.StringComparison.OrdinalIgnoreCase))
                return HandleAnnounce(text);
            if (text.StartsWith("/vote", System.StringComparison.OrdinalIgnoreCase))
                return HandleVote(text);
            if (text.Equals("/ph", System.StringComparison.OrdinalIgnoreCase))
                return HandleBalance();
            if (text.StartsWith("/kill", System.StringComparison.OrdinalIgnoreCase))
                return HandleKill(text);
            if (text.StartsWith("/rn", System.StringComparison.OrdinalIgnoreCase))
                return HandleRename(text);
            if (text.StartsWith("/cor", System.StringComparison.OrdinalIgnoreCase))
                return HandleColor(text);

            // 其余以 / 开头的输入一律隐藏（不广播给其他玩家，防指令泄露）
            if (text.StartsWith('/'))
                return false;

            return true;
        }

        /// <summary>/kill id：直接击杀对应玩家（仅房主，对局中）</summary>
        private static bool HandleKill(string text)
        {
            var show = Modules.ChatHelper.Show;

            if (!IsHost())
            {
                show("[TAHS] /kill 仅房主可用");
                return false;
            }
            if (AmongUsClient.Instance == null
                || AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started)
            {
                show("[TAHS] /kill 仅对局中可用");
                return false;
            }

            var parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !int.TryParse(parts[1], out var id))
            {
                show("[TAHS] 用法：/kill <玩家ID>，先用 /id 查看");
                return false;
            }

            var target = Modules.PlayerIdManager.GetPlayerById(id);
            if (target == null || target.Data == null)
            {
                show($"[TAHS] 未找到 ID 为 {id} 的玩家");
                return false;
            }
            if (target.Data.IsDead)
            {
                show($"[TAHS] [{id}] {target.Data.PlayerName} 已经死亡");
                return false;
            }

            target.RpcMurderPlayer(target, true);
            TAHSPlugin.Log.LogInfo($"[TAHS] 房主 /kill 击杀了 [{id}] {target.Data.PlayerName}");
            show($"[TAHS] 已击杀 [{id}] {target.Data.PlayerName}");
            return false;
        }

        /// <summary>/rn 名字：修改自己的名字（参考 TONE，所有人可用）</summary>
        private static bool HandleRename(string text)
        {
            var show = Modules.ChatHelper.Show;
            if (AmongUsClient.Instance != null
                && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started)
            {
                show("[TAHS] /rn 对局中不可用");
                return false;
            }
            var parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                show("[TAHS] 用法：/rn <新名字>");
                return false;
            }

            var newName = string.Join(' ', parts.Skip(1));
            if (newName.Length > 20) newName = newName[..20];

            var local = PlayerControl.LocalPlayer;
            if (local == null) return false;

            local.RpcSetName(newName);
            show($"[TAHS] 已改名为：{newName}");
            return false;
        }

        /// <summary>/cor 颜色：修改自己的颜色（参考 TONE，所有人可用；支持中英文色名或颜色 ID）</summary>
        private static bool HandleColor(string text)
        {
            var show = Modules.ChatHelper.Show;
            if (AmongUsClient.Instance != null
                && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started)
            {
                show("[TAHS] /cor 对局中不可用");
                return false;
            }
            var parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                show("[TAHS] 用法：/cor <颜色>，如 /cor 红 或 /cor red 或 /cor 0");
                return false;
            }

            var colorId = ParseColor(parts[1]);
            if (colorId < 0)
            {
                show($"[TAHS] 未知颜色：{parts[1]}（支持 红/蓝/绿/粉/橙/黄/黑/白/紫/棕/青/柠檬/栗/玫瑰/香蕉/灰/棕褐/珊瑚 或 0~17）");
                return false;
            }

            var local = PlayerControl.LocalPlayer;
            if (local == null) return false;

            local.RpcSetColor((byte)colorId);
            show($"[TAHS] 已更换颜色：{parts[1]}（ID {colorId}）");
            return false;
        }

        /// <summary>颜色名/ID 解析，失败返回 -1</summary>
        private static int ParseColor(string input)
        {
            if (int.TryParse(input, out var id) && id >= 0 && id <= 17) return id;

            return input.ToLowerInvariant() switch
            {
                "红" or "red" => 0,
                "蓝" or "深蓝" or "blue" => 1,
                "绿" or "green" => 2,
                "粉" or "pink" => 3,
                "橙" or "orange" => 4,
                "黄" or "yellow" => 5,
                "黑" or "black" => 6,
                "白" or "white" => 7,
                "紫" or "purple" => 8,
                "棕" or "brown" => 9,
                "青" or "cyan" => 10,
                "柠檬" or "lime" => 11,
                "栗" or "maroon" => 12,
                "玫瑰" or "rose" => 13,
                "香蕉" or "banana" => 14,
                "灰" or "gray" or "grey" => 15,
                "棕褐" or "tan" => 16,
                "珊瑚" or "coral" => 17,
                _ => -1,
            };
        }

        /// <summary>/ph：平衡主义者处决超编阵营玩家（仅平衡主义者，对局中）</summary>
        private static bool HandleBalance()
        {
            var show = Modules.ChatHelper.Show;

            if (AmongUsClient.Instance == null
                || AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started)
            {
                show("[TAHS] /ph 仅对局中可用");
                return false;
            }

            var local = PlayerControl.LocalPlayer;
            if (local == null) return false;
            if (Roles.CustomRoleManager.GetRole(local) is not Roles.Crewmate.Balancer)
            {
                show("[TAHS] /ph 仅平衡主义者可用");
                return false;
            }

            if (IsHost()) Roles.Crewmate.Balancer.UseSkill(local);
            else Modules.RpcSync.SendModCommand(5, 0); // 请求主机执行
            return false;
        }

        /// <summary>/vote id：投票给对应 ID 的玩家（所有人可用，转换者正常投票通道）</summary>
        private static bool HandleVote(string text)
        {
            var show = Modules.ChatHelper.Show;

            if (MeetingHud.Instance == null)
            {
                show("[TAHS] /vote 仅会议中可用");
                return false;
            }

            var parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !int.TryParse(parts[1], out var id))
            {
                show("[TAHS] 用法：/vote <玩家ID>，先用 /id 查看");
                return false;
            }

            var target = Modules.PlayerIdManager.GetPlayerById(id);
            if (target == null)
            {
                show($"[TAHS] 未找到 ID 为 {id} 的玩家");
                return false;
            }

            var local = PlayerControl.LocalPlayer;
            if (local == null) return false;

            if (IsHost())
                MeetingHud.Instance.CastVote(local.PlayerId, target.PlayerId);
            else
                Modules.RpcSync.SendModCommand(4, id); // 请求主机代为投票

            show($"[TAHS] 已投票给 [{id}] {target.Data?.PlayerName}");
            return false;
        }

        /// <summary>/s 内容：发布醒目公告（房主/协管，参考 TONE 的主机消息）</summary>
        private static bool HandleAnnounce(string text)
        {
            var show = Modules.ChatHelper.Show;
            var content = text.Length > 2 ? text.Substring(2).Trim() : string.Empty;
            if (content.Length == 0)
            {
                show("[TAHS] 用法：/s <内容>");
                return false;
            }
            if (!CanUseHostCommands(Modules.CustomOptions.ModAllowS))
            {
                show("[TAHS] /s 仅房主或协管可用");
                return false;
            }

            if (IsHost())
                Modules.Announcement.Broadcast(true, content);
            else
                Modules.RpcSync.SendModCommandText(3, content); // 协管：请求主机广播
            return false;
        }

        /// <summary>是否房主</summary>
        private static bool IsHost()
        {
            return AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
        }

        /// <summary>是否可使用指定房主指令：房主，或协管名单开启且在名单内且对应子权限开启</summary>
        private static bool CanUseHostCommands(Modules.CustomOption permission)
        {
            if (IsHost()) return true;
            var local = PlayerControl.LocalPlayer;
            return local != null
                   && Modules.ModeratorManager.IsEnabled
                   && Modules.ModeratorManager.IsModerator(local)
                   && permission.Value == 1;
        }

        /// <summary>/addmod id：把该 ID 玩家加入协管名单（仅房主）</summary>
        private static bool HandleAddMod(string text)
        {
            var show = Modules.ChatHelper.Show;

            if (!IsHost())
            {
                show("[TAHS] /addmod 仅房主可用");
                return false;
            }

            var parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !int.TryParse(parts[1], out var id))
            {
                show("[TAHS] 用法：/addmod <玩家ID>，先用 /id 查看");
                return false;
            }

            var target = Modules.PlayerIdManager.GetPlayerById(id);
            if (target == null || target.Data == null)
            {
                show($"[TAHS] 未找到 ID 为 {id} 的玩家");
                return false;
            }

            var code = target.Data.FriendCode;
            if (string.IsNullOrEmpty(code))
            {
                show($"[TAHS] 无法获取 {target.Data.PlayerName} 的好友代码");
                return false;
            }

            if (Modules.ModeratorManager.Add(code))
                show($"[TAHS] 已将 {target.Data.PlayerName}（{code}）加入协管名单");
            else
                show($"[TAHS] {target.Data.PlayerName}（{code}）已在协管名单中");
            return false;
        }

        /// <summary>/bt id 职业：猜测某玩家的职业（参考 TONE），需有猜测权限</summary>
        private static bool HandleBet(string text)
        {
            var show = Modules.ChatHelper.Show;

            if (AmongUsClient.Instance == null
                || AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started)
            {
                show("[TAHS] /bt 仅对局中可用");
                return false;
            }

            var local = PlayerControl.LocalPlayer;
            if (local == null || !Patches.GuesserPatch.CanGuess(local))
            {
                show("[TAHS] 你没有猜测权限（需要赌怪附加职业或猜测模式放行你的阵营）");
                return false;
            }

            var parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || !int.TryParse(parts[1], out var id))
            {
                show("[TAHS] 用法：/bt <玩家ID> <职业名>，如 /bt 2 佃农");
                return false;
            }

            var roleName = string.Join(' ', parts.Skip(2));
            var entry = Patches.GuesserPatch.GetEnabledEntries(local)
                .FirstOrDefault(e => e.Name.Equals(roleName, System.StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                var valid = string.Join("、", Patches.GuesserPatch.GetEnabledEntries(local).Select(e => e.Name));
                show($"[TAHS] 未知职业：{roleName}。可猜测：{valid}");
                return false;
            }

            var target = Modules.PlayerIdManager.GetPlayerById(id);
            if (target == null)
            {
                show($"[TAHS] 未找到 ID 为 {id} 的玩家");
                return false;
            }

            show($"[TAHS] 你猜测 [{id}] {target.Data?.PlayerName} 是 {entry.Name}，结果即将揭晓…");
            Patches.GuesserPatch.RequestGuess(local, target, entry);
            return false; // 拦截命令，不发送到聊天
        }

        /// <summary>/m：查看自己本局职业介绍（所有人可用）</summary>
        private static void ShowMyRole()
        {
            var show = Modules.ChatHelper.Show;
            var local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null) return;

            var role = Roles.CustomRoleManager.GetRole(local);
            if (role == null)
            {
                var vanilla = local.Data.Role != null && local.Data.Role.IsImpostor ? "内鬼" : "船员";
                show($"[TAHS] 你是原版身份：{vanilla}（无模组职业）");
                return;
            }

            var lines = new List<string>
            {
                $"<color=#4FC3F7>===== 你的职业 =====</color>",
                $"{role.Name} / {role.NameEn}（{role.Faction}）",
            };
            if (!string.IsNullOrEmpty(role.Description))
                lines.Add(role.Description);

            // 附加职业
            foreach (var addon in Roles.CustomRoleManager.GetAddons(local))
                lines.Add($"附加：{addon.Name} / {addon.NameEn}");

            Modules.ChatHelper.ShowMany(lines);
        }

        /// <summary>/r：查看全局已开启的职业（所有人可用）</summary>
        private static void ShowEnabledRoles()
        {
            var lines = new List<string> { "<color=#4FC3F7>===== 已开启职业 =====</color>" };
            foreach (var (id, name, faction) in Roles.CustomRoleManager.GetRegisteredRoles())
            {
                if (Modules.CustomOptions.GetRoleChance(id) <= 0) continue;
                lines.Add($"{name}（{faction}）");
            }
            foreach (var (id, name) in Roles.CustomRoleManager.GetRegisteredAddons())
            {
                if (Modules.CustomOptions.GetRoleChance(id) <= 0) continue;
                lines.Add($"{name}（附加）");
            }
            Modules.ChatHelper.ShowMany(lines);
        }

        /// <summary>/d：死亡后查看击杀自己的玩家（所有人可用）</summary>
        private static void ShowMyKiller()
        {
            var show = Modules.ChatHelper.Show;
            var local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null) return;

            if (!local.Data.IsDead)
            {
                show("[TAHS] 你还活着（/d 在死亡后查看击杀者）");
                return;
            }

            var info = Modules.DeathTracker.GetKillerInfo(local.PlayerId);
            show(info != null
                ? $"[TAHS] 击杀你的是：{info}"
                : "[TAHS] 暂无击杀记录（可能死于放逐/自杀或未被记录）");
        }

        /// <summary>/kc：使徒在场（存活）时全员可查存活内鬼与中立人数</summary>
        private static void ShowAliveCounts()
        {
            var show = Modules.ChatHelper.Show;

            if (AmongUsClient.Instance == null
                || AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started)
            {
                show("[TAHS] /kc 仅对局中可用");
                return;
            }
            if (!Roles.Crewmate.Apostle.AliveApostleExists())
            {
                show("[TAHS] 场上没有存活的使徒，/kc 不可用");
                return;
            }

            var impostors = 0;
            var neutrals = 0;
            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (p == null || p.Data == null || p.Data.IsDead) continue;
                switch (Roles.CustomRoleManager.GetFaction(p))
                {
                    case Roles.Faction.Impostor: impostors++; break;
                    case Roles.Faction.Neutral: neutrals++; break;
                }
            }

            Modules.ChatHelper.ShowMany(new[]
            {
                "<color=#4FC3F7>===== 场上存活统计 =====</color>",
                $"<color=#FF5555>存活内鬼：{impostors} 人</color>",
                $"<color=#999999>存活中立：{neutrals} 人</color>",
            });
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
                "<color=#4FC3F7>===== TAHS 指令帮助 =====</color>",
                "/help — 显示本帮助",
                "/id — 显示所有玩家的名字及其对应 ID",
                "/kill <玩家ID> — 直接击杀对应玩家（仅房主/对局中）",
                "/rn <名字> — 修改自己的名字（仅大厅）",
                "/cor <颜色> — 修改自己的颜色（仅大厅，中英文色名或0~17）",
                "/m — 查看自己本局职业介绍",
                "/r — 查看本局已开启的全部职业",
                "/d — 死亡后查看击杀自己的玩家",
                "/l — 查看上一局身份转换详情及击杀记录",
                "/kc — 查看存活内鬼与中立人数（需场上有存活使徒）",
                "/bt <玩家ID> <职业名> — 猜测该玩家的职业（需猜测权限，如 /bt 2 佃农）",
                "/start [秒数] — 以指定倒计时开始游戏（默认5秒，仅房主/协管）",
                "/end — 强制结束对局返回大厅（仅房主/协管，对局中）",
                "/dump — 导出日志到桌面并显示最近日志（仅房主）",
                "/addmod <玩家ID> — 将该玩家加入协管名单（仅房主）",
                "/s <内容> — 发布醒目公告（仅房主/协管，全员可见）",
                "快捷键：ALT+F4 — 强制结束对局（仅房主/协管，对局中）",
            });
        }

        /// <summary>/end：强制结束对局（房主直接执行；协管经 RPC 由主机执行）</summary>
        private static bool HandleEnd()
        {
            if (AmongUsClient.Instance == null
                || AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started)
            {
                Modules.ChatHelper.Show("[TAHS] /end 仅对局中可用");
                return false;
            }
            if (!CanUseHostCommands(Modules.CustomOptions.ModAllowEnd))
            {
                Modules.ChatHelper.Show("[TAHS] /end 仅房主或协管可用");
                return false;
            }

            if (IsHost()) ForceEnd();
            else Modules.RpcSync.SendModCommand(2, 0); // 协管：请求主机结束
            return false;
        }

        /// <summary>/start [秒数]：以指定倒计时开始游戏（房主直接执行；协管经 RPC 由主机执行）</summary>
        private static bool HandleStart(string text)
        {
            if (!CanUseHostCommands(Modules.CustomOptions.ModAllowStart))
            {
                Modules.ChatHelper.Show("[TAHS] /start 仅房主或协管可用");
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
                    TAHSPlugin.Log.LogWarning("[TAHS] /start 仅在大厅中可用");
                    return false;
                }
                TAHSPlugin.Log.LogInfo($"[TAHS] /start：{sec} 秒倒计时开始游戏");
                StartAnyCountPatch.StartCountdown(manager, sec);
            }
            else
            {
                // 协管：请求主机开始倒计时
                Modules.RpcSync.SendModCommand(1, sec);
                Modules.ChatHelper.Show($"[TAHS] 已请求主机开始游戏（{sec} 秒倒计时）");
            }
            return false;
        }
    }
}
