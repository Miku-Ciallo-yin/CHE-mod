using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace TAHS.Patches;

/// <summary>
/// 自定义社区服务器（参考 TONE/EHR 的 RegionInstall 做法）：
/// 向服务器列表注入社区服条目，在社区服开公开房不受官方反作弊管辖
/// （官方服开模组公开房有被 ban 风险，请改用社区服）。
/// 选用当前实测可用的 MAS/MNA/MEU（/api/games 返回 401 的真实 Impostor 匹配服）。
/// </summary>
[HarmonyPatch(typeof(ServerManager))]
public static class CustomServerPatch
{
    /// <summary>注入的社区服（名称、Ping 地址、服务器地址），与社区主流模组服一致</summary>
    private static readonly (string Name, string Ping, string Ip)[] CustomRegions =
    {
        // Niko 系列（au-*.niko233.me）已被 WAF JS 挑战拦截，游戏客户端无法通过，弃用
        ("Modded Asia (MAS)", "https://au-as.duikbo.at", "https://au-as.duikbo.at"),
        ("Modded NA (MNA)", "https://aumods.org", "https://aumods.org"),
        ("Modded EU (MEU)", "https://au-eu.duikbo.at", "https://au-eu.duikbo.at"),
    };

    [HarmonyPatch(nameof(ServerManager.Awake)), HarmonyPostfix]
    public static void AwakePostfix(ServerManager __instance) => AddRegions(__instance);

    [HarmonyPatch(nameof(ServerManager.LoadServers)), HarmonyPostfix]
    public static void LoadServersPostfix(ServerManager __instance) => AddRegions(__instance);

    private static void AddRegions(ServerManager manager)
    {
        foreach (var (name, ping, ip) in CustomRegions)
        {
            try
            {
                var server = new ServerInfo("http-1", ip, 443, false);
                var region = new StaticHttpRegionInfo(name, StringNames.NoTranslation, ping,
                    new Il2CppReferenceArray<ServerInfo>(new[] { server }), null);
                manager.AddOrUpdateRegion(region.Cast<IRegionInfo>()); // 幂等：按名称去重
                TAHSPlugin.Log.LogInfo($"[TAHS] 已注入社区服务器：{name}");
            }
            catch (System.Exception e)
            {
                TAHSPlugin.Log.LogWarning($"[TAHS] 注入社区服务器 {name} 失败: {e.Message}");
            }
        }
    }
}
