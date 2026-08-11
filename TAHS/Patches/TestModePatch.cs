using TAHS.Modules;
using HarmonyLib;

namespace TAHS.Patches;

/// <summary>
/// 测试模式（模组设置中开启）：跳过游戏的正常结束判定，
/// 对局不会因任务/击杀/放逐等条件结束，只能用 /end 或 ALT+F4 手动强制结束。
/// 注意：手动强制结束走的是 RpcEndGame，不经过此判定，不受影响。
/// </summary>
[HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.CheckEndCriteria))]
public static class TestModePatch
{
    public static bool Prefix()
    {
        return CustomOptions.TestMode.Value != 1;
    }
}
