using HarmonyLib;

namespace CHE.Patches;

/// <summary>
/// 任意人数开始游戏：拦截 BeginGame 的人数检查（不足 4 人时原版只弹警告），
/// 主机点击开始直接 ReallyBegin 跳过倒计时开局。
/// </summary>
[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.BeginGame))]
public static class StartAnyCountPatch
{
    public static bool Prefix(GameStartManager __instance)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return true;

        CHEPlugin.Log.LogInfo("[CHE] 跳过人数检查直接开始游戏");
        __instance.ReallyBegin(false);
        return false;
    }
}
