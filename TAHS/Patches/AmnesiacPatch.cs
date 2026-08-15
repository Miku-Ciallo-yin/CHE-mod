using TAHS.Roles;
using HarmonyLib;

namespace TAHS.Patches;

/// <summary>
/// 失忆者报告拦截：失忆者报告尸体时不召开会议，改为"记起"死者身份。
/// 挂在主机 ShipStatus.StartMeeting（召开会议的主机端入口），在广播前拦截，
/// 无模组端失忆者报告同样不触发会议（各端一致）。
/// </summary>
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.StartMeeting))]
public static class AmnesiacPatch
{
    public static bool Prefix(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (reporter == null || target == null) return true; // 紧急会议（无尸体）放行
        if (CustomRoleManager.GetRole(reporter) is not Roles.Neutral.Amnesiac) return true;

        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            Roles.Neutral.Amnesiac.Remember(reporter, target.Object);
        return false; // 不召开会议
    }
}
