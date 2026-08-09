using HarmonyLib;

namespace CHE.Patches;

/// <summary>
/// 取消开局人数下限（保留倒计时）：
/// - MinPlayers 压为 1，大厅不再显示"正在等待玩家"，倒计时不会被自动取消
/// - BeginGame / /start 通过设置 startState=Countdown + countDownTimer 启动倒计时
///   （参考 EHR：SetStartCounter 只是客户端同步显示用的 RPC 处理器，不能用来启动倒计时；
///   原版 Update 检测到 Countdown 状态会逐秒倒数并同步给所有客户端，归零后 FinallyBegin）
/// </summary>
[HarmonyPatch(typeof(GameStartManager))]
public static class StartAnyCountPatch
{
    /// <summary>默认倒计时秒数（与原版一致）</summary>
    public const int DefaultCountdown = 5;

    /// <summary>启动倒计时（仅主机）：seconds 秒后开局</summary>
    public static void StartCountdown(GameStartManager manager, int seconds)
    {
        manager.countDownTimer = seconds + 0.0001f;
        manager.startState = GameStartManager.StartingStates.Countdown;
    }

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

        // 倒计时中再点 = 取消（保持原版交互）
        if (__instance.startState == GameStartManager.StartingStates.Countdown)
        {
            __instance.ResetStartState();
            return false;
        }

        CHEPlugin.Log.LogInfo($"[CHE] 跳过人数检查，{DefaultCountdown} 秒倒计时开始");
        StartCountdown(__instance, DefaultCountdown);
        return false;
    }
}
