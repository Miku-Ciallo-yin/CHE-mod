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
            if (text.StartsWith("/bt", System.StringComparison.OrdinalIgnoreCase)
                && !text.StartsWith("/btd", System.StringComparison.OrdinalIgnoreCase))
                return HandleBet(text);
            if (text.StartsWith("/btd", System.StringComparison.OrdinalIgnoreCase))
                return HandleFortune(text);
            if (text.StartsWith("/sm", System.StringComparison.OrdinalIgnoreCase))
                return HandleDream(text);
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
            if (text.StartsWith("/r ", System.StringComparison.OrdinalIgnoreCase))
            {
                ShowRoleInfo(text.Substring(3).Trim());
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
            if (text.Equals("/tpout", System.StringComparison.OrdinalIgnoreCase))
                return HandleTpOut();
            if (text.Equals("/tpin", System.StringComparison.OrdinalIgnoreCase))
                return HandleTpIn();

            // 其余以 / 开头的输入一律隐藏（不广播给其他玩家，防指令泄露）
            if (text.StartsWith('/'))
                return false;

            return true;
        }

        /// <summary>/btd id：算命师预言该玩家下轮死亡（仅算命师、会议中）</summary>
        private static bool HandleFortune(string text)
        {
            var show = Modules.ChatHelper.Show;

            if (AmongUsClient.Instance == null
                || AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started)
            {
                show("[TAHS] /btd 仅对局中可用");
                return false;
            }
            if (MeetingHud.Instance == null)
            {
                show("[TAHS] /btd 仅会议中可用");
                return false;
            }

            var local = PlayerControl.LocalPlayer;
            if (local == null) return false;
            if (Roles.CustomRoleManager.GetRole(local) is not Roles.Impostor.FortuneTeller)
            {
                show("[TAHS] /btd 仅算命师可用");
                return false;
            }

            var parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !int.TryParse(parts[1], out var id))
            {
                show("[TAHS] 用法：/btd <玩家ID>，如 /btd 2");
                return false;
            }

            var target = Modules.PlayerIdManager.GetPlayerById(id);
            if (target == null)
            {
                show($"[TAHS] 未找到 ID 为 {id} 的玩家");
                return false;
            }

            if (IsHost())
                Roles.Impostor.FortuneTeller.Predict(local, target);
            else
                Modules.RpcSync.SendModCommand(6, id); // 请求主机执行
            return false;
        }

        /// <summary>/sm id：摄梦人摄梦该玩家（仅摄梦人、会议中）</summary>
        private static bool HandleDream(string text)
        {
            var show = Modules.ChatHelper.Show;

            if (AmongUsClient.Instance == null
                || AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started)
            {
                show("[TAHS] /sm 仅对局中可用");
                return false;
            }
            if (MeetingHud.Instance == null)
            {
                show("[TAHS] /sm 仅会议中可用");
                return false;
            }

            var local = PlayerControl.LocalPlayer;
            if (local == null) return false;
            if (Roles.CustomRoleManager.GetRole(local) is not Roles.Impostor.DreamEater)
            {
                show("[TAHS] /sm 仅摄梦人可用");
                return false;
            }

            var parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !int.TryParse(parts[1], out var id))
            {
                show("[TAHS] 用法：/sm <玩家ID>，如 /sm 2");
                return false;
            }

            var target = Modules.PlayerIdManager.GetPlayerById(id);
            if (target == null)
            {
                show($"[TAHS] 未找到 ID 为 {id} 的玩家");
                return false;
            }

            if (IsHost())
                Roles.Impostor.DreamEater.Dream(local, target);
            else
                Modules.RpcSync.SendModCommand(7, id); // 请求主机执行
            return false;
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
            if (Roles.Impostor.DreamEater.TryConsumeImmunity(target))
            {
                show($"[TAHS] [{id}] {target.Data.PlayerName} 处于摄梦保护中，击杀被抵消");
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
            if (Modules.CustomOptions.RenameEnabled.Value != 1)
            {
                show("[TAHS] /rn 已被房主关闭");
                return false;
            }
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
            if (Modules.CustomOptions.ColorEnabled.Value != 1)
            {
                show("[TAHS] /cor 已被房主关闭");
                return false;
            }
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

        /// <summary>/tpout 前的飞船内位置（/tpin 返回用）</summary>
        private static Vector2? _tpReturnPos;

        /// <summary>/tpout：传送到飞船外面（参考 TONE，等待大厅或对局死亡后可用）</summary>
        private static bool HandleTpOut()
        {
            var show = Modules.ChatHelper.Show;
            if (Modules.CustomOptions.TpCommands.Value != 1)
            {
                show("[TAHS] /tpout 已被房主关闭");
                return false;
            }

            var local = PlayerControl.LocalPlayer;
            if (local == null) return false;
            if (!LobbyMovePatch.InLobby && !(local.Data != null && local.Data.IsDead))
            {
                show("[TAHS] /tpout 仅在等待大厅或对局死亡后可用");
                return false;
            }

            _tpReturnPos = local.transform.position;
            local.NetTransform.SnapTo((Vector2)local.transform.position + Vector2.down * 8f);
            show("[TAHS] 已传送到飞船外面（/tpin 返回）");
            return false;
        }

        /// <summary>/tpin：传送回飞船内（返回 /tpout 前的位置，未记录则回出生点）</summary>
        private static bool HandleTpIn()
        {
            var show = Modules.ChatHelper.Show;
            if (Modules.CustomOptions.TpCommands.Value != 1)
            {
                show("[TAHS] /tpin 已被房主关闭");
                return false;
            }

            var local = PlayerControl.LocalPlayer;
            if (local == null) return false;
            if (!LobbyMovePatch.InLobby && !(local.Data != null && local.Data.IsDead))
            {
                show("[TAHS] /tpin 仅在等待大厅或对局死亡后可用");
                return false;
            }

            local.NetTransform.SnapTo(_tpReturnPos ?? new Vector2(0f, 0.5f));
            _tpReturnPos = null;
            show("[TAHS] 已返回飞船内");
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
            var local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null) return;
            Modules.ChatHelper.ShowMany(BuildRoleLines(local));
        }

        /// <summary>/m 内容：指定玩家的职业介绍（本地显示与主机私信共用）</summary>
        public static List<string> BuildRoleLines(PlayerControl player)
        {
            var role = Roles.CustomRoleManager.GetRole(player);
            if (role == null)
            {
                var vanilla = player.Data != null && player.Data.Role != null && player.Data.Role.IsImpostor
                    ? "内鬼" : "船员";
                return new List<string> { $"[TAHS] 你是原版身份：{vanilla}（无模组职业）" };
            }

            var lines = new List<string>
            {
                $"<color=#4FC3F7>===== 你的职业 =====</color>",
                $"{role.Name} / {role.NameEn}（{role.Faction}）",
            };
            if (!string.IsNullOrEmpty(role.Description))
                lines.Add(role.Description);

            // 附加职业
            foreach (var addon in Roles.CustomRoleManager.GetAddons(player))
            {
                lines.Add($"附加：{addon.Name} / {addon.NameEn}");
                if (!string.IsNullOrEmpty(addon.Description))
                    lines.Add(addon.Description);
            }
            return lines;
        }

        /// <summary>/r：查看全局已开启的职业（所有人可用）</summary>
        private static void ShowEnabledRoles()
        {
            Modules.ChatHelper.ShowMany(BuildEnabledRolesLines());
        }

        /// <summary>/r <职业名>：查看指定职业介绍（参考 TONE）</summary>
        private static void ShowRoleInfo(string name)
        {
            Modules.ChatHelper.ShowMany(BuildRoleInfoLines(name));
        }

        /// <summary>/r <职业名> 内容（本地显示与主机私信共用）：按中/英文名精确匹配职业或附加职业</summary>
        public static List<string> BuildRoleInfoLines(string name)
        {
            foreach (var (_, sample) in Roles.CustomRoleManager.GetRoleSamples())
                if (NameMatches(sample.Name, sample.NameEn, name))
                    return BuildDetail(sample.Name, sample.NameEn, sample.Faction.ToString(), sample.Description);

            foreach (var (_, sample) in Roles.CustomRoleManager.GetAddonSamples())
                if (NameMatches(sample.Name, sample.NameEn, name))
                    return BuildDetail(sample.Name, sample.NameEn, "附加职业", sample.Description);

            return new List<string> { $"[TAHS] 未找到职业：{name}（/r 查看本局已开启职业）" };

            static bool NameMatches(string cn, string en, string input)
            {
                return cn.Equals(input, System.StringComparison.OrdinalIgnoreCase)
                       || en.Equals(input, System.StringComparison.OrdinalIgnoreCase);
            }

            static List<string> BuildDetail(string cn, string en, string faction, string description)
            {
                var lines = new List<string>
                {
                    "<color=#4FC3F7>===== 职业介绍 =====</color>",
                    $"{cn} / {en}（{faction}）",
                };
                if (!string.IsNullOrEmpty(description))
                    lines.Add(description);
                return lines;
            }
        }

        /// <summary>/r 内容（本地显示与主机代收无模组端指令时私信共用）：按船员/内鬼/中立分节</summary>
        public static List<string> BuildEnabledRolesLines()
        {
            var lines = new List<string> { "<color=#4FC3F7>===== 已开启职业 =====</color>" };

            // 三个阵营分节（有明显的间隔标识，空节不显示）
            AppendRoleSection(lines, Roles.Faction.Crewmate, "#66E6FF", "船员职业");
            AppendRoleSection(lines, Roles.Faction.Impostor, "#FF5555", "内鬼职业");
            AppendRoleSection(lines, Roles.Faction.Neutral, "#999999", "中立职业");

            var addons = new List<string>();
            foreach (var (id, name, _) in Roles.CustomRoleManager.GetRegisteredAddons())
            {
                if (Modules.CustomOptions.GetRoleChance(id) <= 0) continue;
                addons.Add(name);
            }
            if (addons.Count > 0)
            {
                lines.Add("<color=#FFB84D>—— 附加职业 ——</color>");
                lines.AddRange(addons);
            }
            return lines;
        }

        /// <summary>追加一个阵营分节（无启用职业时不显示该节）</summary>
        private static void AppendRoleSection(List<string> lines, Roles.Faction faction, string color, string title)
        {
            var names = new List<string>();
            foreach (var (id, name, f) in Roles.CustomRoleManager.GetRegisteredRoles())
            {
                if (f != faction) continue;
                if (Modules.CustomOptions.GetRoleChance(id) <= 0) continue;
                names.Add(name);
            }
            if (names.Count == 0) return;

            lines.Add($"<color={color}>—— {title} ——</color>");
            lines.AddRange(names);
        }

        /// <summary>/d：死亡后查看击杀自己的玩家（所有人可用）</summary>
        private static void ShowMyKiller()
        {
            var local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null) return;
            Modules.ChatHelper.Show(BuildKillerText(local));
        }

        /// <summary>/d 内容：指定玩家的击杀者信息（本地显示与主机私信共用）</summary>
        public static string BuildKillerText(PlayerControl player)
        {
            if (player.Data == null || !player.Data.IsDead)
                return "[TAHS] 你还活着（/d 在死亡后查看击杀者）";

            var info = Modules.DeathTracker.GetKillerInfo(player.PlayerId);
            return info != null
                ? $"[TAHS] 击杀你的是：{info}"
                : "[TAHS] 暂无击杀记录（可能死于放逐/自杀或未被记录）";
        }

        /// <summary>/kc：使徒在场（存活）时全员可查存活内鬼与中立人数</summary>
        private static void ShowAliveCounts()
        {
            Modules.ChatHelper.ShowMany(BuildAliveCountLines());
        }

        /// <summary>/kc 内容（本地显示与主机私信共用）</summary>
        public static List<string> BuildAliveCountLines()
        {
            if (AmongUsClient.Instance == null
                || AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started)
                return new List<string> { "[TAHS] /kc 仅对局中可用" };
            if (!Roles.Crewmate.Apostle.AliveApostleExists())
                return new List<string> { "[TAHS] 场上没有存活的使徒，/kc 不可用" };

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

            return new List<string>
            {
                "<color=#4FC3F7>===== 场上存活统计 =====</color>",
                $"<color=#FF5555>存活内鬼：{impostors} 人</color>",
                $"<color=#999999>存活中立：{neutrals} 人</color>",
            };
        }

        /// <summary>/id：输出所有玩家的名字及对应 ID（仅本机可见）</summary>
        private static void ShowPlayerIds()
        {
            Modules.ChatHelper.ShowMany(BuildPlayerIdLines(PlayerControl.LocalPlayer));
        }

        /// <summary>/id 内容（requester 标注"（你）"，本地显示与主机私信共用）</summary>
        public static List<string> BuildPlayerIdLines(PlayerControl? requester)
        {
            var lines = new List<string> { "<color=#4FC3F7>===== 玩家 ID 列表 =====</color>" };
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null) continue;
                var id = Modules.PlayerIdManager.GetId(player);
                var idText = id.HasValue ? id.Value.ToString() : "?";
                var self = player == requester ? "（你）" : string.Empty;
                lines.Add($"[{idText}] {player.Data.PlayerName}{self}");
            }
            return lines;
        }

        /// <summary>/help：输出全部指令及功能（合并为一条气泡，超长自动拆分）</summary>
        private static void ShowHelp()
        {
            Modules.ChatHelper.ShowMany(BuildHelpLines());
        }

        /// <summary>/help 内容（本地显示与主机代收无模组端指令时私信共用）</summary>
        public static string[] BuildHelpLines() => new[]
        {
            "<color=#4FC3F7>===== TAHS 指令帮助 =====</color>",
            "/help — 显示本帮助",
            "/id — 显示所有玩家的名字及其对应 ID",
            "/kill <玩家ID> — 直接击杀对应玩家（仅房主/对局中）",
            "/rn <名字> — 修改自己的名字（仅大厅）",
            "/cor <颜色> — 修改自己的颜色（仅大厅，中英文色名或0~17）",
            "/tpout — 传送到飞船外面（大厅/死亡后）",
            "/tpin — 传送回飞船内（大厅/死亡后）",
            "/m — 查看自己本局职业介绍",
            "/r [职业名] — 查看本局已开启职业 / 指定职业介绍",
            "/d — 死亡后查看击杀自己的玩家",
            "/l — 查看上一局身份转换详情及击杀记录",
            "/kc — 查看存活内鬼与中立人数（需场上有存活使徒）",
            "/bt <玩家ID> <职业名> — 猜测该玩家的职业（需猜测权限，如 /bt 2 佃农）",
            "/btd <玩家ID> — 算命师预言该玩家下轮死亡（仅算命师/会议中）",
            "/sm <玩家ID> — 摄梦人摄梦该玩家（仅摄梦人/会议中）",
            "/start [秒数] — 以指定倒计时开始游戏（默认5秒，仅房主/协管）",
            "/end — 强制结束对局返回大厅（仅房主/协管，对局中）",
            "/dump — 导出日志到桌面并显示最近日志（仅房主）",
            "/addmod <玩家ID> — 将该玩家加入协管名单（仅房主）",
            "/s <内容> — 发布醒目公告（仅房主/协管，全员可见）",
            "快捷键：ALT+F4 — 强制结束对局（仅房主/协管，对局中）",
        };

        /// <summary>
        /// 主机代收无模组端玩家发来的指令（模组端指令在本地处理且不会广播）：
        /// 信息类指令私信回复（仅发起者可见），操作类指令由主机验证后代为执行。
        /// 新增指令约定：内容提取为 Build* 构建器，此处一并接入——本地显示与主机私信共用。
        /// </summary>
        public static void HandleHostCommand(PlayerControl source, string text)
        {
            if (source == null || source.Data == null) return;
            System.Action<string> tell = msg => Modules.ChatHelper.ShowPrivate(source, msg);

            // 信息类（所有人可用，私信回复）
            if (text.Equals("/help", System.StringComparison.OrdinalIgnoreCase))
            { Modules.ChatHelper.ShowPrivateMany(source, BuildHelpLines()); return; }
            if (text.Equals("/r", System.StringComparison.OrdinalIgnoreCase))
            { Modules.ChatHelper.ShowPrivateMany(source, BuildEnabledRolesLines()); return; }
            if (text.StartsWith("/r ", System.StringComparison.OrdinalIgnoreCase))
            { Modules.ChatHelper.ShowPrivateMany(source, BuildRoleInfoLines(text.Substring(3).Trim())); return; }
            if (text.Equals("/id", System.StringComparison.OrdinalIgnoreCase))
            { Modules.ChatHelper.ShowPrivateMany(source, BuildPlayerIdLines(source)); return; }
            if (text.Equals("/m", System.StringComparison.OrdinalIgnoreCase))
            { Modules.ChatHelper.ShowPrivateMany(source, BuildRoleLines(source)); return; }
            if (text.Equals("/d", System.StringComparison.OrdinalIgnoreCase))
            { tell(BuildKillerText(source)); return; }
            if (text.Equals("/l", System.StringComparison.OrdinalIgnoreCase))
            { Modules.ChatHelper.ShowPrivateMany(source, Modules.GameArchive.BuildLastLines()); return; }
            if (text.Equals("/kc", System.StringComparison.OrdinalIgnoreCase))
            { Modules.ChatHelper.ShowPrivateMany(source, BuildAliveCountLines()); return; }

            // 猜测（主机验证资格并执行）
            if (text.StartsWith("/bt", System.StringComparison.OrdinalIgnoreCase)
                && !text.StartsWith("/btd", System.StringComparison.OrdinalIgnoreCase))
            { HostBet(source, text, tell); return; }

            // 算命师预言（主机验证职业并执行）
            if (text.StartsWith("/btd", System.StringComparison.OrdinalIgnoreCase))
            { HostFortune(source, text, tell); return; }

            // 摄梦人摄梦（主机验证职业并执行）
            if (text.StartsWith("/sm", System.StringComparison.OrdinalIgnoreCase))
            { HostDream(source, text, tell); return; }

            // 平衡主义者处决（主机验证职业并执行）
            if (text.Equals("/ph", System.StringComparison.OrdinalIgnoreCase))
            { HostBalance(source, tell); return; }

            // 大厅自我服务（主机代为改名/换色）
            if (text.StartsWith("/rn", System.StringComparison.OrdinalIgnoreCase))
            { HostRename(source, text, tell); return; }
            if (text.StartsWith("/cor", System.StringComparison.OrdinalIgnoreCase))
            { HostColor(source, text, tell); return; }

            // 传送
            if (text.StartsWith("/tpout", System.StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("/tpin", System.StringComparison.OrdinalIgnoreCase))
            { LobbyMovePatch.HandleHostCommand(source, text); return; }

            // 协管指令（无模组协管按好友代码识别）
            if (text.StartsWith("/start", System.StringComparison.OrdinalIgnoreCase))
            { HostStart(source, text, tell); return; }
            if (text.Equals("/end", System.StringComparison.OrdinalIgnoreCase))
            { HostEnd(source, tell); return; }
            if (text.Equals("/s", System.StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("/s ", System.StringComparison.OrdinalIgnoreCase))
            { HostAnnounce(source, text, tell); return; }

            // 房主专属
            if (text.StartsWith("/kill", System.StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("/dump", System.StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("/addmod", System.StringComparison.OrdinalIgnoreCase))
            { tell("[TAHS] 该指令仅房主可用"); return; }

            // 需要模组端本地交互
            if (text.StartsWith("/vote", System.StringComparison.OrdinalIgnoreCase))
            { tell("[TAHS] 该指令需要安装模组端使用"); return; }
        }

        /// <summary>主机代收 /ph：验证职业后执行平衡主义者处决（与 /bt 同模式）</summary>
        private static void HostBalance(PlayerControl source, System.Action<string> tell)
        {
            if (AmongUsClient.Instance == null
                || AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started)
            {
                tell("[TAHS] /ph 仅对局中可用");
                return;
            }
            if (Roles.CustomRoleManager.GetRole(source) is not Roles.Crewmate.Balancer)
            {
                tell("[TAHS] /ph 仅平衡主义者可用");
                return;
            }

            Roles.Crewmate.Balancer.UseSkill(source); // 主机权威执行，反馈走定向私信
        }

        /// <summary>无模组协管校验（按好友代码识别，权限项需开启）</summary>
        private static bool IsCoModWith(PlayerControl source, Modules.CustomOption permission)
        {
            return Modules.ModeratorManager.IsEnabled
                   && Modules.ModeratorManager.IsModerator(source)
                   && permission.Value == 1;
        }

        /// <summary>主机代收 /bt：验证资格后执行猜测</summary>
        private static void HostBet(PlayerControl source, string text, System.Action<string> tell)
        {
            if (!Patches.GuesserPatch.CanGuess(source))
            {
                tell("[TAHS] 你没有猜测权限（需赌怪附加或猜测模式放行）");
                return;
            }

            var parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || !int.TryParse(parts[1], out var id))
            {
                tell("[TAHS] 用法：/bt <玩家ID> <职业名>，如 /bt 2 佃农");
                return;
            }

            var roleName = string.Join(' ', parts.Skip(2));
            var entry = Patches.GuesserPatch.GetEnabledEntries(source)
                .FirstOrDefault(e => e.Name.Equals(roleName, System.StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                var valid = string.Join("、", Patches.GuesserPatch.GetEnabledEntries(source).Select(e => e.Name));
                tell($"[TAHS] 未知职业：{roleName}。可猜测：{valid}");
                return;
            }

            var target = Modules.PlayerIdManager.GetPlayerById(id);
            if (target == null)
            {
                tell($"[TAHS] 未找到 ID 为 {id} 的玩家");
                return;
            }

            tell($"[TAHS] 你猜测 [{id}] {target.Data?.PlayerName} 是 {entry.Name}，结果即将揭晓…");
            Patches.GuesserPatch.ExecuteGuess(source, target, entry);
        }

        /// <summary>主机代收 /btd：验证职业后执行算命师预言（与 /bt 同模式）</summary>
        private static void HostFortune(PlayerControl source, string text, System.Action<string> tell)
        {
            if (AmongUsClient.Instance == null
                || AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started)
            {
                tell("[TAHS] /btd 仅对局中可用");
                return;
            }
            if (Roles.CustomRoleManager.GetRole(source) is not Roles.Impostor.FortuneTeller)
            {
                tell("[TAHS] /btd 仅算命师可用");
                return;
            }

            var parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !int.TryParse(parts[1], out var id))
            {
                tell("[TAHS] 用法：/btd <玩家ID>，如 /btd 2");
                return;
            }

            var target = Modules.PlayerIdManager.GetPlayerById(id);
            if (target == null)
            {
                tell($"[TAHS] 未找到 ID 为 {id} 的玩家");
                return;
            }

            Roles.Impostor.FortuneTeller.Predict(source, target); // 会议校验在 Predict 内
        }

        /// <summary>主机代收 /sm：验证职业后执行摄梦（与 /btd 同模式）</summary>
        private static void HostDream(PlayerControl source, string text, System.Action<string> tell)
        {
            if (AmongUsClient.Instance == null
                || AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started)
            {
                tell("[TAHS] /sm 仅对局中可用");
                return;
            }
            if (Roles.CustomRoleManager.GetRole(source) is not Roles.Impostor.DreamEater)
            {
                tell("[TAHS] /sm 仅摄梦人可用");
                return;
            }

            var parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !int.TryParse(parts[1], out var id))
            {
                tell("[TAHS] 用法：/sm <玩家ID>，如 /sm 2");
                return;
            }

            var target = Modules.PlayerIdManager.GetPlayerById(id);
            if (target == null)
            {
                tell($"[TAHS] 未找到 ID 为 {id} 的玩家");
                return;
            }

            Roles.Impostor.DreamEater.Dream(source, target); // 会议校验在 Dream 内
        }

        /// <summary>主机代收 /rn：代为广播改名（受开关与大厅限制）</summary>
        private static void HostRename(PlayerControl source, string text, System.Action<string> tell)
        {
            if (Modules.CustomOptions.RenameEnabled.Value != 1)
            {
                tell("[TAHS] /rn 已被房主关闭");
                return;
            }
            if (AmongUsClient.Instance != null
                && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started)
            {
                tell("[TAHS] /rn 对局中不可用");
                return;
            }

            var parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                tell("[TAHS] 用法：/rn <新名字>");
                return;
            }
            var newName = string.Join(' ', parts.Skip(1));
            if (newName.Length > 20) newName = newName[..20];

            // 主机权威广播改名（本版本游戏数据消息通道）+ 主机本地应用
            Modules.PrivateTag.SendNameMessage(source, newName, -1);
            source.SetName(newName);
            tell($"[TAHS] 已改名为：{newName}");
        }

        /// <summary>主机代收 /cor：代为换色（受开关与大厅限制）</summary>
        private static void HostColor(PlayerControl source, string text, System.Action<string> tell)
        {
            if (Modules.CustomOptions.ColorEnabled.Value != 1)
            {
                tell("[TAHS] /cor 已被房主关闭");
                return;
            }
            if (AmongUsClient.Instance != null
                && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started)
            {
                tell("[TAHS] /cor 对局中不可用");
                return;
            }

            var parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                tell("[TAHS] 用法：/cor <颜色>，如 /cor 红 或 /cor red 或 /cor 0");
                return;
            }
            var colorId = ParseColor(parts[1]);
            if (colorId < 0)
            {
                tell($"[TAHS] 未知颜色：{parts[1]}（支持中英文色名或 0~17）");
                return;
            }

            source.RpcSetColor((byte)colorId);
            tell($"[TAHS] 已更换颜色：{parts[1]}（ID {colorId}）");
        }

        /// <summary>主机代收 /start：无模组协管请求开始倒计时</summary>
        private static void HostStart(PlayerControl source, string text, System.Action<string> tell)
        {
            if (!IsCoModWith(source, Modules.CustomOptions.ModAllowStart))
            {
                tell("[TAHS] /start 仅房主或协管可用");
                return;
            }
            var manager = GameStartManager.Instance;
            if (manager == null)
            {
                tell("[TAHS] /start 仅在大厅中可用");
                return;
            }

            var sec = StartAnyCountPatch.DefaultCountdown;
            var parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
                int.TryParse(parts[1], out sec);
            sec = UnityEngine.Mathf.Clamp(sec, 0, 99);

            TAHSPlugin.Log.LogInfo($"[TAHS] 无模组协管 {source.Data?.PlayerName} 请求 /start {sec}s");
            StartAnyCountPatch.StartCountdown(manager, sec);
            tell($"[TAHS] 已开始 {sec} 秒倒计时");
        }

        /// <summary>主机代收 /end：无模组协管请求强制结束</summary>
        private static void HostEnd(PlayerControl source, System.Action<string> tell)
        {
            if (AmongUsClient.Instance == null
                || AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started)
            {
                tell("[TAHS] /end 仅对局中可用");
                return;
            }
            if (!IsCoModWith(source, Modules.CustomOptions.ModAllowEnd))
            {
                tell("[TAHS] /end 仅房主或协管可用");
                return;
            }

            TAHSPlugin.Log.LogInfo($"[TAHS] 无模组协管 {source.Data?.PlayerName} 请求 /end");
            ForceEnd();
        }

        /// <summary>主机代收 /s：无模组协管发布公告</summary>
        private static void HostAnnounce(PlayerControl source, string text, System.Action<string> tell)
        {
            var content = text.Length > 2 ? text.Substring(2).Trim() : string.Empty;
            if (content.Length == 0)
            {
                tell("[TAHS] 用法：/s <内容>");
                return;
            }
            if (!IsCoModWith(source, Modules.CustomOptions.ModAllowS))
            {
                tell("[TAHS] /s 仅房主或协管可用");
                return;
            }

            Modules.Announcement.Broadcast(false, content);
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
