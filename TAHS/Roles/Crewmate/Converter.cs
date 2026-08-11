using TAHS.Modules;
using UnityEngine;

namespace TAHS.Roles.Crewmate;

/// <summary>
/// 转换者（船员阵营）：
/// - 会议中用投票键选择两名玩家：第一名玩家转变为第二名玩家的职业，
///   转换者得知第一名的原职业
/// - 正常投票需使用 /vote 指令（投票键被技能占用）
/// - 未使用技能选择弃票：本轮无法再使用技能，本轮可正常投票
/// - 技能次数用尽视为白板：可直接投票
/// 配置：技能可使用次数
/// </summary>
public class Converter : RoleBase
{
    public override string Name => "转换者";
    public override string NameEn => "Converter";
    public override Faction Faction => Faction.Crewmate;
    public override Color Color => new(0.2f, 0.8f, 0.5f); // 青绿
    public override string Description => "投票键选人：前者变成后者的职业。正常投票请用 /vote。";

    /// <summary>剩余技能次数</summary>
    public int UsesLeft { get; private set; }

    /// <summary>本轮是否已锁定技能（弃票触发）</summary>
    public bool SkillLockedThisRound { get; private set; }

    /// <summary>是否可拦截投票键使用技能</summary>
    public bool CanUseSkill => UsesLeft > 0 && !SkillLockedThisRound;

    private PlayerControl? _first;

    public override void OnAssign(PlayerControl player)
    {
        base.OnAssign(player);
        UsesLeft = CustomOptions.ConverterSkillUses.Value;
    }

    /// <summary>会议开始：重置选人状态与锁定</summary>
    public void OnMeetingStart()
    {
        _first = null;
        SkillLockedThisRound = false;
    }

    /// <summary>未使用技能选择弃票：本轮锁定技能，可正常投票</summary>
    public void OnSkip()
    {
        SkillLockedThisRound = true;
        TAHSPlugin.Log.LogInfo("[TAHS] 转换者弃票，本轮技能锁定，可正常投票");
    }

    /// <summary>主机：投票键选人（第一次选前者，第二次选后者并执行转换）</summary>
    public void OnSkillSelect(PlayerControl voter, PlayerControl target)
    {
        if (target == null || target.Data == null) return;

        if (_first == null)
        {
            _first = target;
            Feedback(voter, $"[TAHS] 已选择第一名玩家：{target.Data.PlayerName}（再选第二名）");
            return;
        }

        if (target == _first)
        {
            Feedback(voter, "[TAHS] 不能选择同一名玩家");
            return;
        }

        var first = _first;
        _first = null;
        UsesLeft--;

        var originalRole = CustomRoleManager.GetRole(first);
        var sourceRole = CustomRoleManager.GetRole(target);

        if (sourceRole != null)
        {
            var newRole = CustomRoleManager.CreateRoleOfType(sourceRole);
            if (newRole != null)
                CustomRoleManager.TransformToRole(first, newRole);
        }
        else
        {
            // 后者是原版身份：前者变回白板
            CustomRoleManager.RemoveRole(first);
        }

        var originalName = originalRole?.Name
            ?? (first.Data!.Role != null && first.Data.Role.IsImpostor ? "内鬼" : "船员");

        TAHSPlugin.Log.LogInfo(
            $"[TAHS] 转换者 {voter.Data?.PlayerName}：{first.Data?.PlayerName}（{originalName}）→ {target.Data?.PlayerName} 的职业（{sourceRole?.Name ?? "白板"}）");
        GameArchive.RecordTransition(
            $"转换者 {voter.Data?.PlayerName} 将 {first.Data?.PlayerName} 从 {originalName} 转换为 {sourceRole?.Name ?? "白板"}");
        Feedback(voter, $"[TAHS] 转换完成！{first.Data?.PlayerName} 的原职业是：{originalName}");
    }

    /// <summary>反馈给转换者（模组端聊天栏，主机直达/远程经 RPC）</summary>
    private static void Feedback(PlayerControl voter, string text)
    {
        if (voter.AmOwner) ChatHelper.Show(text);
        else RpcSync.SendShowMessage(voter.OwnerId, text);
    }

    public override string GetStatusText()
    {
        if (UsesLeft <= 0) return "白板（可直接投票）";
        if (SkillLockedThisRound) return "本轮技能已锁定";
        var status = $"转换次数 {UsesLeft}/{CustomOptions.ConverterSkillUses.Value}";
        if (_first != null) status += "（已选前者）";
        return status;
    }
}
