using HarmonyLib;

namespace TAHS.Patches;

/// <summary>
/// 突破原版内鬼数量上限（3）：把大厅设置中内鬼数量选项的取值范围改为 1~15。
/// </summary>
[HarmonyPatch(typeof(GameOptionsMenu), nameof(GameOptionsMenu.CreateSettings))]
public static class ImpostorLimitPatch
{
    private const int MaxImpostors = 15;

    public static void Postfix(GameOptionsMenu __instance)
    {
        if (__instance.Children == null) return;

        for (var i = 0; i < __instance.Children.Count; i++)
        {
            if (__instance.Children[i] is not NumberOption num) continue;
            if (num.intOptionName != AmongUs.GameOptions.Int32OptionNames.NumImpostors) continue;
            if (num.Data is not IntGameSetting setting) continue;

            setting.ValidRange = new IntRange(1, MaxImpostors);
        }
    }
}
