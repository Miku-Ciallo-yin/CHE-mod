using HarmonyLib;

namespace TAHS.Patches;

/// <summary>
/// Innersloth 模组协议合规：游戏内全程显示官方模组标记。
/// 协议要求：模组必须在游戏各部分显示该标记（ModManager 单例在
/// SplashScreen 时可用，Awake 之后调用 ShowModStamp 即可）。
/// </summary>
[HarmonyPatch(typeof(ModManager), nameof(ModManager.LateUpdate))]
public static class ModStampPatch
{
    private static bool _shown;

    public static void Postfix(ModManager __instance)
    {
        if (_shown) return;
        _shown = true;
        __instance.ShowModStamp();
        TAHSPlugin.Log.LogInfo("[TAHS] 已启用官方模组标记（Mod Stamp）");
    }
}
