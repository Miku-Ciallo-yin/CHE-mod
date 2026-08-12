using Hazel;
using InnerNet;
using HarmonyLib;

namespace TAHS.Patches;

/// <summary>官方服判定与网络防护（参考 TONE 的 GameStates.IsVanillaServer / 大包拦截）</summary>
public static class OfficialServerGuard
{
    /// <summary>
    /// 当前是否连接官方服务器。
    /// 官方区域是 StaticHttpRegionInfo 且 Ping/服务器地址均为官方 among.us 域名；
    /// 社区服（Niko-AS 等）域名不同，旧式 UDP 自定义区域不是 HTTP 区域。
    /// </summary>
    public static bool IsOfficialServer()
    {
        var region = ServerManager.Instance?.CurrentRegion;
        if (region == null) return false;

        var http = region.TryCast<StaticHttpRegionInfo>();
        if (http == null) return false;
        if (!http.PingServer.EndsWith("among.us", System.StringComparison.Ordinal)) return false;

        var servers = http.Servers;
        for (var i = 0; i < servers.Count; i++)
            if (!servers[i].Ip.EndsWith("among.us", System.StringComparison.Ordinal))
                return false;
        return true;
    }

    /// <summary>在线对局/大厅中</summary>
    public static bool IsOnline =>
        AmongUsClient.Instance != null
        && AmongUsClient.Instance.NetworkMode == NetworkModes.OnlineGame;
}

/// <summary>
/// 官方服版本隔离（参考 TONE 的 ServerUpdatePatch）：
/// 官方服上把广播版本号抬升到 +25 频段——官方匹配按版本分池，
/// 模组房（含公开房）不会出现在原版房间列表，只有模组端能看到和加入；
/// 原版端也无法通过房间代码加入官方服模组房（版本不匹配，混玩请用社区服）。
/// 社区服不抬升，保持与原版互通（Host Only 混玩依赖此）。
/// </summary>
[HarmonyPatch(typeof(Constants), nameof(Constants.GetBroadcastVersion))]
public static class BroadcastVersionPatch
{
    public static void Postfix(ref int __result)
    {
        if (!OfficialServerGuard.IsOnline || !OfficialServerGuard.IsOfficialServer()) return;
        if (__result % 50 < 25)
            __result += 25;
    }
}

/// <summary>
/// 自报模组身份（参考 TONE 的 IsVersionModdedPatch）：
/// Constants.IsVersionModded 恒为 true，官方匹配/反作弊据此将会话识别为模组会话，
/// 模组房内的非常规 RPC（自定义职业等）不按作弊处理。
/// </summary>
[HarmonyPatch(typeof(Constants), nameof(Constants.IsVersionModded))]
public static class VersionModdedPatch
{
    public static bool Prefix(ref bool __result)
    {
        __result = true;
        return false;
    }
}

/// <summary>
/// 大包拦截（参考 TONE 的 PreventLargePacketKickPatch）：
/// 官方服会踢出发送超大包（>1200 字节）的客户端，拦截不发送。
/// </summary>
[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SendOrDisconnect))]
public static class LargePacketGuardPatch
{
    public static bool Prefix(MessageWriter msg)
    {
        if (msg.Length <= 1200) return true;
        if (OfficialServerGuard.IsOnline && OfficialServerGuard.IsOfficialServer())
        {
            TAHSPlugin.Log.LogWarning($"[TAHS] 官方服：拦截超大网络包（{msg.Length} 字节），避免被官方踢出/封禁");
            return false;
        }
        return true;
    }
}
