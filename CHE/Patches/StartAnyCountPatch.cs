using HarmonyLib;

namespace CHE.Patches;

/// <summary>
/// 取消开局人数下限：
/// - 把 GameStartManager.MinPlayers 压为 1，原版"正在等待玩家"提示和
///   人数不足自动取消倒计时的逻辑都不会触发
/// - BeginGame 的人数检查直接走原版倒计时流程（SetStartCounter）
/// - /start [秒数] 命令见 ForceEndPatch.ChatCommandPatch
/// </summary>
[HarmonyPatch(typeof(GameStartManager))]
public static class StartAnyCountPatch
{
    /// <summary>默认倒计时秒数（与原版一致）</summary>
    public const int DefaultCountdown = 5;

    [HarmonyPatch(nameof(GameStartManager.Start)), HarmonyPostfix]
    public static void StartPostfix(GameStartManager __instance)
    {
        __instance.MinPlayers = 1;
    }

    /// <summary>持续压制，防止原版逻辑把 MinPlayers 改回去</summary>
    [HarmonyPatch(nameof(GameStartManager.Update)), HarmonyPostfix]
    public static void UpdatePostfix(GameStartManager __instance)
    {
        if (__instance.MinPlayers > 1)
            __instance.MinPlayers = 1;
    }

    [HarmonyPatch(nameof(GameStartManager.BeginGame)), HarmonyPrefix]
    public static bool BeginGamePrefix(GameStartManager __instance)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return true;

        CHEPlugin.Log.LogInfo("[CHE] 跳过人数检查，开始倒计时");
        __instance.SetStartCounter((sbyte)DefaultCountdown);
        return false;
    }
}
