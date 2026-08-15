using TAHS.Modules;
using TAHS.Roles.Impostor;
using HarmonyLib;

namespace TAHS.Patches;

/// <summary>
/// 摄梦人放逐保护：处于摄梦免疫中的玩家被投票时，该票不计入（主机在 CastVote
/// 执行处拦截——不注册不广播，投票结果由各端一致），并消耗一次免疫。
/// </summary>
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CastVote))]
public static class DreamEaterPatch
{
    public static bool Prefix(byte srcPlayerId, byte suspectPlayerId)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return true;

        var suspect = PlayerControl.AllPlayerControls.ToArray()
            .FirstOrDefault(p => p != null && p.PlayerId == suspectPlayerId);
        if (suspect == null || !DreamEater.IsImmune(suspect)) return true;

        DreamEater.TryConsumeImmunity(suspect); // 消耗免疫，抵消本次放逐
        var voter = PlayerControl.AllPlayerControls.ToArray()
            .FirstOrDefault(p => p != null && p.PlayerId == srcPlayerId);
        if (voter != null)
            ChatHelper.ShowPrivate(voter, "[TAHS] 该玩家处于摄梦保护中，放逐被抵消");
        TAHSPlugin.Log.LogInfo($"[TAHS] 对 {suspect.Data?.PlayerName} 的投票被摄梦免疫拦截");
        return false;
    }
}
