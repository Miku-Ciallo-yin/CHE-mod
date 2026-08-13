using TAHS.Modules;
using HarmonyLib;

namespace TAHS.Patches;

/// <summary>
/// 测试模式（模组设置中开启）：拦截"非会议期间"的自动结束判定，
/// 对局不会因任务/击杀/放逐等条件在中途自动结束，只能手动强制结束（/end、ALT+F4）。
///
/// 限制：会议/散场期间必须放行该判定——原版散场流程以它收尾
/// （胜利条件已满足时会议后游戏会正常结束），拦截会导致散场后黑屏卡死。
/// 手动强制结束走的是 RpcEndGame，不经过此判定，不受影响。
/// </summary>
[HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.CheckEndCriteria))]
public static class TestModePatch
{
    public static bool Prefix()
    {
        if (CustomOptions.TestMode.Value != 1) return true;

        // 会议/放逐散场期间放行，让原版流程收尾（胜利条件满足时会正常结束游戏）
        if (MeetingHud.Instance != null || ExileController.Instance != null) return true;

        return false;
    }
}
