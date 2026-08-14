using TAHS.Modules;
using UnityEngine;

namespace TAHS.Roles.Crewmate;

/// <summary>
/// 风水师（船员阵营）：
/// - 每完成一个任务，随机一名玩家暴毙（首刀保护护盾与自身除外，死因"风水不好"）
/// - 拥有变形按钮：对一名玩家使用即令其死亡（死因"风水不好"）
/// - 若导致两名船员阵营玩家死亡，则堕落为算命师
/// - 未完成任何任务时被票出，则随机带走一名玩家（散场后结算）
/// </summary>
public class FengshuiMaster : RoleBase
{
    /// <summary>注册 ID（与 RoleRegistry 一致）</summary>
    public const byte RoleId = 18;

    public override string Name => "风水师";
    public override string NameEn => "FengshuiMaster";
    public override Faction Faction => Faction.Crewmate;
    public override Color Color => new(0.3f, 0.75f, 0.6f); // 青玉
    public override bool UsesShapeshiftButton => true; // 变形按钮点杀
    public override string Description =>
        "做任务会随机带走旁人（护盾与自身除外）；变形按钮可点杀一人。害死两名船员则堕落为算命师；未做任务被票出则随机带走一人。";

    /// <summary>因风水师死亡的船员阵营人数（达到 2 堕落为算命师）</summary>
    public int CrewDeathsCaused { get; private set; }

    private bool _taskCompleted;
    private bool _pendingRevenge;

    public override void OnAssign(PlayerControl player)
    {
        base.OnAssign(player);
        // 准则：技能职业给予原版变形按钮（无模组端也可用）
        CustomRoleManager.GrantVanillaButtons(player);
    }

    /// <summary>主机驱动（Host Only）</summary>
    public override void OnUpdate()
    {
        // 被票出的报复：散场后结算（会议/放逐期间 OnUpdate 被全局暂停，此时杀人是安全的）
        if (_pendingRevenge)
        {
            _pendingRevenge = false;
            var target = Impostor.FortuneTeller.RandomAliveAny(Player, excludeProtected: false);
            if (target != null)
            {
                TAHSPlugin.Log.LogInfo($"[TAHS] 风水师被票出，带走了 {target.Data?.PlayerName}");
                DeathTracker.KillWithCause(target, "风水不好");
            }
            return;
        }

        if (Player == null || Player.Data == null || Player.Data.IsDead) return;
    }

    /// <summary>主机：完成任务（ApostlePatch.TaskGrant 调用）——随机暴毙一名玩家</summary>
    public void OnTaskComplete()
    {
        _taskCompleted = true;
        var target = Impostor.FortuneTeller.RandomAliveAny(Player, excludeProtected: true);
        if (target == null) return;

        TAHSPlugin.Log.LogInfo($"[TAHS] 风水师完成任务，{target.Data?.PlayerName} 因风水不好暴毙");
        KillAndCount(target);
    }

    /// <summary>主机：变形按钮点杀（Shapeshift 劫持调用）</summary>
    public void KillByButton(PlayerControl target)
    {
        if (target == null || target.Data == null || target.Data.IsDead) return;
        TAHSPlugin.Log.LogInfo($"[TAHS] 风水师点杀了 {target.Data.PlayerName}");
        KillAndCount(target);
    }

    /// <summary>被票出：未完成任何任务则散场后随机带走一名玩家</summary>
    public override void OnExile()
    {
        if (_taskCompleted) return;
        _pendingRevenge = true;
    }

    /// <summary>处死并计数：被害者为船员阵营时累计，满 2 人堕落为算命师</summary>
    private void KillAndCount(PlayerControl victim)
    {
        DeathTracker.KillWithCause(victim, "风水不好");

        if (CustomRoleManager.GetFaction(victim) != Faction.Crewmate) return;
        CrewDeathsCaused++;
        if (CrewDeathsCaused >= 2 && Player != null && !Player.Data!.IsDead)
        {
            TAHSPlugin.Log.LogInfo($"[TAHS] 风水师已导致 {CrewDeathsCaused} 名船员死亡，堕落为算命师");
            GameArchive.RecordTransition($"风水师 {Player.Data?.PlayerName} 堕落为算命师");
            CustomRoleManager.TransformToRole(Player, new Impostor.FortuneTeller());
        }
    }

    public override string GetStatusText()
    {
        return $"船员命案 {CrewDeathsCaused}/2";
    }
}
