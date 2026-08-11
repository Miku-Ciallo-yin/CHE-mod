using TAHS.Modules;
using TAHS.Roles;
using TAHS.Roles.Crewmate;
using HarmonyLib;

namespace TAHS.Patches;

/// <summary>
/// 转换者投票拦截（仅主机执行，无模组客户端的点击同样经 CmdCastVote 到达主机）：
/// - 技能可用时：投票键点击玩家 = 技能选人，拦截不投票
/// - 弃票：本轮锁定技能后正常弃票
/// - 白板/锁定/次数用尽：放行正常投票
/// 会议开始时重置各转换者的选人状态。
/// </summary>
public static class ConverterPatch
{
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CmdCastVote))]
    public static class VoteIntercept
    {
        public static bool Prefix(byte playerId, byte suspectIdx)
        {
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return true;

            var voter = PlayerControl.AllPlayerControls.ToArray()
                .FirstOrDefault(p => p != null && p.PlayerId == playerId);
            if (voter == null) return true;
            if (CustomRoleManager.GetRole(voter) is not Converter converter) return true;
            if (!converter.CanUseSkill) return true; // 白板/锁定：正常投票

            // 弃票（suspectIdx >= 250 为跳过/无效）
            if (suspectIdx >= 250)
            {
                converter.OnSkip();
                return true;
            }

            var target = PlayerControl.AllPlayerControls.ToArray()
                .FirstOrDefault(p => p != null && p.PlayerId == suspectIdx);
            if (target == null) return true;

            converter.OnSkillSelect(voter, target);
            return false; // 拦截：技能选人，不投票
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    public static class MeetingReset
    {
        public static void Postfix()
        {
            foreach (var role in CustomRoleManager.ActiveRoles)
                if (role is Converter converter)
                    converter.OnMeetingStart();
        }
    }

    /// <summary>使徒私有标签：死者名牌下显示阵营·死因（定向改名，无模组端使徒也可见）</summary>
    public static class ApostleTags
    {
        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start)), HarmonyPostfix]
        public static void OnMeetingStart()
        {
            foreach (var p in PlayerControl.AllPlayerControls)
                if (p != null && p.Data != null && p.Data.IsDead)
                    TagForApostles(p);
        }

        /// <summary>主机：给所有使徒的客户端打上该死者的阵营标签</summary>
        public static void TagForApostles(PlayerControl dead)
        {
            if (dead == null || dead.Data == null) return;

            var tag = FactionCauseText(dead);
            foreach (var p in PlayerControl.AllPlayerControls)
                if (p != null && CustomRoleManager.GetRole(p) is Apostle)
                    PrivateTag.SetTag(p.OwnerId, dead, tag);
        }

        /// <summary>阵营·死因富文本（红内鬼/灰中立/青船员）</summary>
        public static string FactionCauseText(PlayerControl player)
        {
            var faction = CustomRoleManager.GetFaction(player);
            var (color, name) = faction switch
            {
                Faction.Impostor => ("#FF5555", "内鬼"),
                Faction.Neutral => ("#999999", "中立"),
                _ => ("#66E6FF", "船员"),
            };
            var cause = DeathTracker.GetCause(player.PlayerId) ?? "击杀";
            return $"<color={color}>{name}·{cause}</color>";
        }
    }
}
