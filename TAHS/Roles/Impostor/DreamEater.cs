using TAHS.Modules;
using UnityEngine;

namespace TAHS.Roles.Impostor;

/// <summary>
/// 摄梦人（内鬼阵营）：
/// - 会议中用 /sm &lt;ID&gt; 摄梦一名玩家：其免疫一次任意死亡（含会议中的赌杀/投票放逐等）
/// - 第二次摄梦同一玩家：免疫失效，该玩家立即在会议上死亡（死因"摄梦"）
/// - 误杀内鬼阵营玩家时，摄梦人变为失忆者
/// </summary>
public class DreamEater : RoleBase
{
    /// <summary>注册 ID（与 RoleRegistry 一致）</summary>
    public const byte RoleId = 20;

    public override string Name => "摄梦人";
    public override string NameEn => "DreamEater";
    public override Faction Faction => Faction.Impostor;
    public override Color Color => new(0.4f, 0.35f, 0.75f); // 梦蓝紫
    public override string Description =>
        "会议中 /sm <ID> 摄梦：目标免疫一次死亡；再次摄梦同一人则其当场死亡。误杀内鬼将变为失忆者。";

    /// <summary>本摄梦人已摄梦的玩家</summary>
    public byte? MarkedId { get; private set; }

    /// <summary>本次会议是否已摄梦（每次会议限一次）</summary>
    private bool _usedThisMeeting;

    /// <summary>拥有一次免疫的玩家集合（主机权威；模组端空集合，判定都经主机）</summary>
    private static readonly HashSet<byte> ImmunePlayers = new();

    public override void OnAssign(PlayerControl player)
    {
        base.OnAssign(player);
        // 误杀内鬼机制的 targeting 前提：内鬼系身份可被选中（参考 TONE 的 CanBeKilled）
        foreach (var p in PlayerControl.AllPlayerControls)
            if (p != null && p.Data != null && p.Data.Role != null && p.Data.Role.IsImpostor)
                p.Data.Role.CanBeKilled = true;
    }

    /// <summary>误杀内鬼：变为失忆者（MurderPatch 各端调用，主机负责身份 RPC）</summary>
    public override void OnMurder(PlayerControl target)
    {
        if (CustomRoleManager.GetFaction(target) != Faction.Impostor) return;
        if (Player == null || Player.Data == null || Player.Data.IsDead) return;

        TAHSPlugin.Log.LogInfo($"[TAHS] 摄梦人 {Player.Data.PlayerName} 误杀内鬼，变为失忆者");
        GameArchive.RecordTransition($"摄梦人 {Player.Data.PlayerName} 误杀内鬼，变为失忆者");
        ChatHelper.ShowPrivate(Player, "[TAHS] 你误杀了内鬼，记忆崩解——变为失忆者");
        CustomRoleManager.TransformToRole(Player, new Neutral.Amnesiac());
    }

    /// <summary>目标是否有摄梦免疫；有则消耗并返回 true（主机调用，各端判定经主机）</summary>
    public static bool TryConsumeImmunity(PlayerControl? target)
    {
        if (target == null) return false;
        if (!ImmunePlayers.Remove(target.PlayerId)) return false;

        TAHSPlugin.Log.LogInfo($"[TAHS] {target.Data?.PlayerName} 的摄梦免疫抵消了一次死亡");
        ChatHelper.ShowPrivate(target, "[TAHS] 摄梦保护抵消了一次死亡");
        return true;
    }

    /// <summary>目标是否处于摄梦免疫中</summary>
    public static bool IsImmune(PlayerControl? target)
    {
        return target != null && ImmunePlayers.Contains(target.PlayerId);
    }

    /// <summary>主机：摄梦（含校验与双方提示）</summary>
    public static void Dream(PlayerControl teller, PlayerControl? target)
    {
        System.Action<string> tell = msg => ChatHelper.ShowPrivate(teller, msg);

        if (MeetingHud.Instance == null)
        {
            tell("[TAHS] /sm 仅在会议中可用");
            return;
        }
        if (teller.Data == null || teller.Data.IsDead) return;
        if (CustomRoleManager.GetRole(teller) is not DreamEater self) return;
        if (self._usedThisMeeting)
        {
            tell("[TAHS] 本次会议已经摄梦过了");
            return;
        }
        if (target == null || target.Data == null || target.Data.IsDead)
        {
            tell("[TAHS] 目标不存在或已死亡");
            return;
        }
        if (target == teller)
        {
            tell("[TAHS] 不能摄梦自己");
            return;
        }

        self._usedThisMeeting = true;

        if (self.MarkedId == target.PlayerId)
        {
            // 第二次摄梦同一玩家：免疫失效，当场死亡
            self.MarkedId = null;
            ImmunePlayers.Remove(target.PlayerId);
            tell($"[TAHS] 你收回了 {target.Data.PlayerName} 的梦，其当场死亡");
            TAHSPlugin.Log.LogInfo($"[TAHS] 摄梦人 {teller.Data.PlayerName} 收回 {target.Data.PlayerName} 的梦，其当场死亡");
            GameArchive.RecordTransition($"摄梦人 {teller.Data.PlayerName} 收回 {target.Data.PlayerName} 的梦（当场死亡）");
            DeathTracker.KillWithCause(target, "摄梦");
            return;
        }

        self.MarkedId = target.PlayerId;
        ImmunePlayers.Add(target.PlayerId);
        tell($"[TAHS] 你摄梦了 [{target.PlayerId}] {target.Data.PlayerName}，其将免疫一次死亡");
        ChatHelper.ShowPrivate(target, "[TAHS] 你被摄梦了：将免疫一次死亡（任意死因）");
        TAHSPlugin.Log.LogInfo($"[TAHS] 摄梦人 {teller.Data.PlayerName} 摄梦了 {target.Data.PlayerName}");
        GameArchive.RecordTransition($"摄梦人 {teller.Data.PlayerName} 摄梦了 {target.Data.PlayerName}");
    }

    /// <summary>会议开始：重置每会一次的使用限制</summary>
    public static void OnMeetingStart()
    {
        foreach (var role in CustomRoleManager.ActiveRoles)
            if (role is DreamEater eater)
                eater._usedThisMeeting = false;
    }

    /// <summary>对局重置（CustomRoleManager.Reset 调用）</summary>
    public static void ResetStatics() => ImmunePlayers.Clear();
}
