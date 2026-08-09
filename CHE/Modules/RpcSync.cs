using CHE.Roles;
using Hazel;
using InnerNet;

namespace CHE.Modules;

/// <summary>
/// CHE 自定义 RPC：通过 PlayerControl 的 NetObject 通道收发。
/// 目前只有职业分配同步；放逐、击杀等事件游戏本身会在每个客户端执行，
/// 因此小丑获胜、佃农转阵营等状态无需额外同步（前提是各端职业一致）。
/// </summary>
public static class RpcSync
{
    /// <summary>职业分配广播（主机 -> 全员）。自定义 CallId 取不易冲突的大值。</summary>
    public const byte SyncRolesCallId = 217;

    /// <summary>选项值广播（主机 -> 全员）。</summary>
    public const byte SyncOptionsCallId = 218;

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
    /// </summary>
    public static bool Handle(byte callId, MessageReader reader)
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
