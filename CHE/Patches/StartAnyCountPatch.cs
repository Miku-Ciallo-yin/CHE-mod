using HarmonyLib;

namespace CHE.Patches;

/// <summary>
/// 取消开局人数下限但保留倒计时：拦截 BeginGame 的人数检查，
/// 直接走原版倒计时流程（SetStartCounter 5 秒）。
/// </summary>
[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.BeginGame))]
public static class StartAnyCountPatch
{
    /// <summary>默认倒计时秒数（与原版一致）</summary>
    public const int DefaultCountdown = 5;

    public static bool Prefix(GameStartManager __instance)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return true;

        CHEPlugin.Log.LogInfo("[CHE] 跳过人数检查，开始倒计时");
        __instance.SetStartCounter((sbyte)DefaultCountdown);
        return false;
    }
}
