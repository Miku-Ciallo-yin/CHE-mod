using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace TAHS.Patches;

/// <summary>
/// 自定义社区服务器（参考 TONE/EHR 的 RegionInstall 做法）：
/// 向服务器列表注入社区服条目，在社区服开公开房不受官方反作弊管辖
/// （官方服开模组公开房有被 ban 风险，请改用社区服）。
/// 与 TONE 同名注入（Niko-AS / Niko-CN），两模组共存时不会重复出现。
/// </summary>
[HarmonyPatch(typeof(ServerManager))]
public static class CustomServerPatch
{
    /// <summary>注入的社区服（名称、Ping 地址、服务器地址），与 TONE/EHR 保持一致</summary>
    private static readonly (string Name, string Ping, string Ip)[] CustomRegions =
    {
        ("Niko-AS", "https://au-as.niko233.me", "https://au-as.niko233.me"),
        ("Niko-CN", "play.simpfun.cn", "https://au-cn.niko233.me"),
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
