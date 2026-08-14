using TAHS.Roles;
using Hazel;
using InnerNet;
using UnityEngine;

namespace TAHS.Modules;

/// <summary>
/// TAHS 自定义 RPC：通过 PlayerControl 的 NetObject 通道收发。
/// 目前只有职业分配同步；放逐、击杀等事件游戏本身会在每个客户端执行，
/// 因此小丑获胜、佃农转阵营等状态无需额外同步（前提是各端职业一致）。
/// </summary>
public static class RpcSync
{
    /// <summary>职业分配广播（主机 -> 全员）。自定义 CallId 取不易冲突的大值。</summary>
    public const byte SyncRolesCallId = 217;

    /// <summary>选项值广播（主机 -> 全员）。</summary>
    public const byte SyncOptionsCallId = 218;

    /// <summary>玩家 ID 映射广播（主机 -> 全员）。</summary>
    public const byte SyncPlayerIdsCallId = 220;

    /// <summary>猜测请求（非主机模组端 -> 主机）。</summary>
    public const byte GuessRequestCallId = 221;

    /// <summary>向指定客户端显示聊天栏消息（主机 -> 指定模组端）。</summary>
    public const byte ShowMessageCallId = 223;

    /// <summary>协管指令请求（模组端 -> 主机）：kind 1=/start(value 秒) 2=/end。</summary>
    public const byte ModCommandCallId = 224;

    /// <summary>公告广播（主机 -> 全模组端）：/s 醒目消息。</summary>
    public const byte AnnouncementCallId = 225;

    /// <summary>附加职业赐予（主机 -> 全员）：使徒完成任务时。</summary>
    public const byte AddonGrantCallId = 226;

    /// <summary>模组握手（模组端 -> 主机）：进房时告知主机自己装有模组。</summary>
    public const byte HandshakeCallId = 227;

    /// <summary>地雷同步（主机 -> 全模组端）：kind 1=放置 2=移除。</summary>
    public const byte MineSyncCallId = 228;

    /// <summary>死因同步（主机 -> 全模组端）：算命/风水不好等自定义死因。</summary>
    public const byte DeathCauseCallId = 229;

    /// <summary>主机：广播自定义死因。</summary>
    public static void BroadcastDeathCause(byte victimId, string cause)
    {
        var client = AmongUsClient.Instance;
        if (client == null || client.allClients.Count <= 1) return;

        var writer = client.StartRpcImmediately(
            PlayerControl.LocalPlayer.NetId, DeathCauseCallId, SendOption.Reliable, -1);
        writer.Write(victimId);
        writer.Write(cause);
        client.FinishRpcImmediately(writer);
    }

    /// <summary>主机：广播地雷放置/移除。</summary>
    public static void SendMineSync(byte kind, int index, Vector2 pos, float range, float visibleSeconds)
    {
        var client = AmongUsClient.Instance;
        if (client == null || client.allClients.Count <= 1) return;

        var writer = client.StartRpcImmediately(
            PlayerControl.LocalPlayer.NetId, MineSyncCallId, SendOption.Reliable, -1);
        writer.Write(kind);
        writer.Write(index);
        writer.Write(pos.x);
        writer.Write(pos.y);
        writer.Write(range);
        writer.Write(visibleSeconds);
        client.FinishRpcImmediately(writer);
    }

    /// <summary>模组端进房时向主机发送握手。</summary>
    public static void SendHandshake()
    {
        var client = AmongUsClient.Instance;
        if (client == null || client.AmHost || PlayerControl.LocalPlayer == null) return;

        var writer = client.StartRpcImmediately(
            PlayerControl.LocalPlayer.NetId, HandshakeCallId, SendOption.Reliable, client.HostId);
        client.FinishRpcImmediately(writer);
    }

    /// <summary>主机：广播附加职业赐予。</summary>
    public static void BroadcastAddonGrant(byte playerId, byte addonId)
    {
        var client = AmongUsClient.Instance;
        if (client == null || client.allClients.Count <= 1) return;

        var writer = client.StartRpcImmediately(
            PlayerControl.LocalPlayer.NetId, AddonGrantCallId, SendOption.Reliable, -1);
        writer.Write(playerId);
        writer.Write(addonId);
        client.FinishRpcImmediately(writer);
    }

    /// <summary>主机：向全模组端广播公告（label + 内容）。</summary>
    public static void SendAnnouncement(string label, string content)
    {
        var client = AmongUsClient.Instance;
        if (client == null || client.allClients.Count <= 1) return;

        var writer = client.StartRpcImmediately(
            PlayerControl.LocalPlayer.NetId, AnnouncementCallId, SendOption.Reliable, -1);
        writer.Write(label);
        writer.Write(content);
        client.FinishRpcImmediately(writer);
    }

    /// <summary>非主机协管端：向主机发送带文本的指令请求（kind 3 = /s）。</summary>
    public static void SendModCommandText(byte kind, string text)
    {
        var client = AmongUsClient.Instance;
        if (client == null || client.AmHost || client.allClients.Count <= 1) return;

        var writer = client.StartRpcImmediately(
            PlayerControl.LocalPlayer.NetId, ModCommandCallId, SendOption.Reliable, client.HostId);
        writer.Write(kind);
        writer.Write(text);
        client.FinishRpcImmediately(writer);
    }

    /// <summary>非主机协管端：向主机发送指令请求。</summary>
    public static void SendModCommand(byte kind, int value)
    {
        var client = AmongUsClient.Instance;
        if (client == null || client.AmHost || client.allClients.Count <= 1) return;

        var writer = client.StartRpcImmediately(
            PlayerControl.LocalPlayer.NetId, ModCommandCallId, SendOption.Reliable, client.HostId);
        writer.Write(kind);
        writer.Write(value);
        client.FinishRpcImmediately(writer);
    }

    /// <summary>主机：向指定客户端发送聊天栏消息（仅对方本机可见）。</summary>
    public static void SendShowMessage(int targetClientId, string text)
    {
        var client = AmongUsClient.Instance;
        if (client == null || client.allClients.Count <= 1) return;

        var writer = client.StartRpcImmediately(
            PlayerControl.LocalPlayer.NetId, ShowMessageCallId, SendOption.Reliable, targetClientId);
        writer.Write(text);
        client.FinishRpcImmediately(writer);
    }

    /// <summary>非主机模组端：向主机发送猜测请求。</summary>
    public static void SendGuessRequest(byte targetId, bool isAddon, byte guessId)
    {
        var client = AmongUsClient.Instance;
        if (client == null || client.AmHost || client.allClients.Count <= 1) return;

        var writer = client.StartRpcImmediately(
            PlayerControl.LocalPlayer.NetId, GuessRequestCallId, SendOption.Reliable, client.HostId);
        writer.Write(targetId);
        writer.Write(isAddon);
        writer.Write(guessId);
        client.FinishRpcImmediately(writer);
    }

    /// <summary>主机：广播玩家 ID 映射表（含协管标记）。</summary>
    public static void BroadcastPlayerIds(IReadOnlyDictionary<int, int> ids)
    {
        var client = AmongUsClient.Instance;
        if (client == null || !client.AmHost || client.allClients.Count <= 1) return;

        var writer = client.StartRpcImmediately(
            PlayerControl.LocalPlayer.NetId, SyncPlayerIdsCallId, SendOption.Reliable, -1);
        writer.Write((byte)ids.Count);
        foreach (var (clientId, id) in ids)
        {
            writer.Write(clientId);
            writer.Write(id);
            var player = PlayerControl.AllPlayerControls.ToArray()
                .FirstOrDefault(p => p != null && p.OwnerId == clientId);
            writer.Write(player != null && ModeratorManager.IsModerator(player));
        }
        client.FinishRpcImmediately(writer);
    }

    /// <summary>主机：把全部选项值广播给所有客户端。</summary>
    public static void BroadcastOptions()
    {
        var client = AmongUsClient.Instance;
        if (client == null || !client.AmHost || client.allClients.Count <= 1) return;

        var writer = client.StartRpcImmediately(
            PlayerControl.LocalPlayer.NetId, SyncOptionsCallId, SendOption.Reliable, -1);
        writer.Write((byte)CustomOption.All.Count);
        foreach (var opt in CustomOption.All)
        {
            writer.Write(opt.Id);
            writer.Write(opt.Value);
        }
        client.FinishRpcImmediately(writer);
    }

    /// <summary>主机：把分配结果广播给所有客户端（单人 / 离线局不发送）。</summary>
    public static void BroadcastRoleAssignments(
        IReadOnlyList<(byte PlayerId, byte RoleId)> assignments,
        IReadOnlyList<(byte PlayerId, byte AddonId)> addonAssignments)
    {
        var client = AmongUsClient.Instance;
        if (client == null || client.allClients.Count <= 1) return;

        var writer = client.StartRpcImmediately(
            PlayerControl.LocalPlayer.NetId, SyncRolesCallId, SendOption.Reliable, -1);
        writer.Write((byte)assignments.Count);
        foreach (var (playerId, roleId) in assignments)
        {
            writer.Write(playerId);
            writer.Write(roleId);
        }
        writer.Write((byte)addonAssignments.Count);
        foreach (var (playerId, addonId) in addonAssignments)
        {
            writer.Write(playerId);
            writer.Write(addonId);
        }
        client.FinishRpcImmediately(writer);
    }

    /// <summary>
    /// 处理收到的自定义 RPC。返回 true 表示已处理（应跳过游戏原始处理逻辑）。
    /// sender 为携带该 RPC 的 PlayerControl（用于识别请求者职业）。
    /// </summary>
    public static bool Handle(byte callId, MessageReader reader, PlayerControl sender)
    {
        if (callId == SyncRolesCallId)
        {
            var count = reader.ReadByte();
            var assignments = new List<(byte PlayerId, byte RoleId)>(count);
            for (var i = 0; i < count; i++)
                assignments.Add((reader.ReadByte(), reader.ReadByte()));

            var addonCount = reader.ReadByte();
            var addonAssignments = new List<(byte PlayerId, byte AddonId)>(addonCount);
            for (var i = 0; i < addonCount; i++)
                addonAssignments.Add((reader.ReadByte(), reader.ReadByte()));

            CustomRoleManager.ApplyRoleAssignments(assignments, addonAssignments);
            return true;
        }

        if (callId == SyncPlayerIdsCallId)
        {
            var count = reader.ReadByte();
            for (var i = 0; i < count; i++)
            {
                var clientId = reader.ReadInt32();
                var id = reader.ReadInt32();
                PlayerIdManager.Set(clientId, id);
                PlayerIdManager.SetModerator(clientId, reader.ReadBoolean());
            }
            return true;
        }

        if (callId == GuessRequestCallId)
        {
            // 只有主机处理猜测请求，ExecuteGuess 内含权限与状态校验
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return true;

            var targetId = reader.ReadByte();
            var isAddon = reader.ReadBoolean();
            var guessId = reader.ReadByte();
            var target = PlayerControl.AllPlayerControls.ToArray()
                .FirstOrDefault(p => p != null && p.PlayerId == targetId);

            Patches.GuesserPatch.ExecuteGuess(sender, target,
                new Patches.GuesserPatch.GuessEntry { IsAddon = isAddon, Id = guessId });
            return true;
        }

        if (callId == ShowMessageCallId)
        {
            ChatHelper.Show(reader.ReadString());
            return true;
        }

        if (callId == ModCommandCallId)
        {
            // 只有主机处理
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return true;

            var kind = reader.ReadByte();
            if (kind == 4)
            {
                // /vote 请求：所有玩家可用（转换者正常投票的唯一通道）
                var voteTarget = reader.ReadInt32();
                if (MeetingHud.Instance != null)
                {
                    MeetingHud.Instance.CastVote(sender.PlayerId, (byte)voteTarget);
                    TAHSPlugin.Log.LogInfo($"[TAHS] {sender.Data?.PlayerName} 通过 /vote 投票给 {voteTarget}");
                }
                return true;
            }

            if (kind == 5)
            {
                // /ph 请求：平衡主义者技能（所有人可发，职业校验在 UseSkill 内）
                if (CustomRoleManager.GetRole(sender) is Roles.Crewmate.Balancer)
                    Roles.Crewmate.Balancer.UseSkill(sender);
                return true;
            }

            if (kind == 6)
            {
                // /btd 请求：算命师预言（所有人可发，校验在 Predict 内）
                var predictedId = reader.ReadInt32();
                if (CustomRoleManager.GetRole(sender) is Roles.Impostor.FortuneTeller)
                    Roles.Impostor.FortuneTeller.Predict(sender, PlayerIdManager.GetPlayerById(predictedId));
                return true;
            }

            if (!ModeratorManager.IsEnabled || !ModeratorManager.IsModerator(sender)) return true;

            if (kind == 1)
            {
                var sec = reader.ReadInt32();
                if (CustomOptions.ModAllowStart.Value != 1) return true;
                var manager = GameStartManager.Instance;
                if (manager != null)
                {
                    TAHSPlugin.Log.LogInfo($"[TAHS] 协管 {sender.Data?.PlayerName} 请求 /start {sec}");
                    Patches.StartAnyCountPatch.StartCountdown(manager, sec);
                }
            }
            else if (kind == 2)
            {
                if (CustomOptions.ModAllowEnd.Value != 1) return true;
                TAHSPlugin.Log.LogInfo($"[TAHS] 协管 {sender.Data?.PlayerName} 请求 /end");
                Patches.ForceEndPatch.ForceEnd();
            }
            else if (kind == 3)
            {
                if (CustomOptions.ModAllowS.Value != 1) return true;
                var content = reader.ReadString();
                TAHSPlugin.Log.LogInfo($"[TAHS] 协管 {sender.Data?.PlayerName} 请求 /s：{content}");
                Announcement.Broadcast(false, content);
            }
            return true;
        }

        if (callId == AnnouncementCallId)
        {
            var label = reader.ReadString();
            var content = reader.ReadString();
            Announcement.Show(label, content);
            return true;
        }

        if (callId == AddonGrantCallId)
        {
            var playerId = reader.ReadByte();
            var addonId = reader.ReadByte();
            var player = PlayerControl.AllPlayerControls.ToArray()
                .FirstOrDefault(p => p != null && p.PlayerId == playerId);
            if (player != null)
                CustomRoleManager.GrantAddon(player, addonId);
            return true;
        }

        if (callId == HandshakeCallId)
        {
            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                PlayerIdManager.MarkModded(sender.OwnerId);
            return true;
        }

        if (callId == MineSyncCallId)
        {
            var kind = reader.ReadByte();
            var index = reader.ReadInt32();
            var x = reader.ReadSingle();
            var y = reader.ReadSingle();
            var range = reader.ReadSingle();
            var visible = reader.ReadSingle();

            if (kind == 1) MineVisuals.OnPlace(index, new Vector2(x, y), range, visible);
            else MineVisuals.Remove(index);
            return true;
        }

        if (callId == DeathCauseCallId)
        {
            DeathTracker.SetCause(reader.ReadByte(), reader.ReadString());
            return true;
        }

        if (callId == SyncOptionsCallId)
        {
            var count = reader.ReadByte();
            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadByte();
                var value = reader.ReadInt32();
                var opt = CustomOption.Get(id);
                if (opt != null) opt.Value = value;
            }
            return true;
        }

        return false;
    }
}
