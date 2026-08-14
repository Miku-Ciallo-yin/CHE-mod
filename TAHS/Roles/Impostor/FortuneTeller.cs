using TAHS.Modules;
using UnityEngine;

namespace TAHS.Roles.Impostor;

/// <summary>
/// 算命师（内鬼阵营）：
/// - 会议中用 /btd &lt;ID&gt; 预言一名玩家将于下轮死亡，被预言者收到提示
/// - 预言成真（该玩家在下一次会议前死亡）：随机一名船员阵营玩家死亡（死因"算命"）
/// - 若被预言的是内鬼且成真：算命师转变为风水师，且有中立存活时随机一名中立死亡
/// 预言在下一次会议开始时失效。死亡结算见 <see cref="OnDeath"/>（MurderPatch 调用）。
/// </summary>
public class FortuneTeller : RoleBase
{
    /// <summary>注册 ID（与 RoleRegistry 一致）</summary>
    public const byte RoleId = 17;

    public override string Name => "算命师";
    public override string NameEn => "FortuneTeller";
    public override Faction Faction => Faction.Impostor;
    public override Color Color => new(0.55f, 0.3f, 0.7f); // 暗紫
    public override string Description =>
        "会议中 /btd <ID> 预言下轮死者；成真则随机暴毙一名船员。预言内鬼成真则化身风水师并带走一名中立。";

    /// <summary>本轮预言的目标 PlayerId（null = 未预言）</summary>
    public byte? PredictedId { get; private set; }

    /// <summary>主机：执行预言（含校验与双方提示）</summary>
    public static void Predict(PlayerControl teller, PlayerControl? target)
    {
        System.Action<string> tell = msg => ChatHelper.ShowPrivate(teller, msg);

        if (MeetingHud.Instance == null)
        {
            tell("[TAHS] /btd 仅在会议中可用");
            return;
        }
        if (teller.Data == null || teller.Data.IsDead) return;
        if (target == null || target.Data == null || target.Data.IsDead)
        {
            tell("[TAHS] 目标不存在或已死亡");
            return;
        }
        if (target == teller)
        {
            tell("[TAHS] 不能预言自己");
            return;
        }

        var self = GetRole(teller) as FortuneTeller;
        if (self == null) return;
        self.PredictedId = target.PlayerId;

        tell($"[TAHS] 你预言 [{target.PlayerId}] {target.Data.PlayerName} 将于下轮死亡");
        ChatHelper.ShowPrivate(target, "[TAHS] 算命师预言你将于下轮死亡…");
        TAHSPlugin.Log.LogInfo($"[TAHS] 算命师 {teller.Data.PlayerName} 预言 {target.Data.PlayerName} 下轮死亡");
        GameArchive.RecordTransition($"算命师 {teller.Data.PlayerName} 预言 {target.Data.PlayerName} 下轮死亡");
    }

    /// <summary>主机：死亡结算（预言成真判定），每次击杀成功后调用</summary>
    public static void OnDeath(PlayerControl victim)
    {
        if (victim == null || victim.Data == null) return;

        foreach (var role in CustomRoleManager.ActiveRoles)
        {
            if (role is not FortuneTeller teller) continue;
            if (teller.Player == null || teller.Player.Data == null || teller.Player.Data.IsDead) continue;
            if (teller.PredictedId != victim.PlayerId) continue;

            teller.PredictedId = null;

            if (CustomRoleManager.GetFaction(victim) == Faction.Impostor)
            {
                // 预言内鬼成真：算命师化身风水师，并带走一名中立
                TAHSPlugin.Log.LogInfo($"[TAHS] 算命师预言内鬼 {victim.Data.PlayerName} 成真，转变为风水师");
                GameArchive.RecordTransition($"算命师 {teller.Player.Data.PlayerName} 预言成真，转变为风水师");
                CustomRoleManager.TransformToRole(teller.Player, new Crewmate.FengshuiMaster());

                var neutral = RandomAlive(Faction.Neutral, exclude: null, excludeProtected: false);
                if (neutral != null)
                    DeathTracker.KillWithCause(neutral, "算命");
            }
            else
            {
                // 预言成真：随机暴毙一名船员
                var crew = RandomAlive(Faction.Crewmate, exclude: null, excludeProtected: false);
                if (crew != null)
                    DeathTracker.KillWithCause(crew, "算命");
            }
        }
    }

    /// <summary>会议开始：上一轮预言失效</summary>
    public static void OnMeetingStart()
    {
        foreach (var role in CustomRoleManager.ActiveRoles)
            if (role is FortuneTeller teller)
                teller.PredictedId = null;
    }

    /// <summary>随机一名存活的指定阵营玩家（excludeProtected 时排除首刀保护护盾对象）</summary>
    public static PlayerControl? RandomAlive(Faction faction, PlayerControl? exclude, bool excludeProtected)
    {
        var candidates = new System.Collections.Generic.List<PlayerControl>();
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p == null || p.Data == null || p.Data.IsDead) continue;
            if (exclude != null && p == exclude) continue;
            if (excludeProtected && FirstKillProtection.IsProtected(p)) continue;
            if (CustomRoleManager.GetFaction(p) != faction) continue;
            candidates.Add(p);
        }
        if (candidates.Count == 0) return null;
        return candidates[new System.Random().Next(candidates.Count)];
    }

    /// <summary>随机一名存活玩家（不限阵营；excludeProtected 时排除护盾对象）</summary>
    public static PlayerControl? RandomAliveAny(PlayerControl? exclude, bool excludeProtected)
    {
        var candidates = new System.Collections.Generic.List<PlayerControl>();
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p == null || p.Data == null || p.Data.IsDead) continue;
            if (exclude != null && p == exclude) continue;
            if (excludeProtected && FirstKillProtection.IsProtected(p)) continue;
            candidates.Add(p);
        }
        if (candidates.Count == 0) return null;
        return candidates[new System.Random().Next(candidates.Count)];
    }

    private static RoleBase? GetRole(PlayerControl player) => CustomRoleManager.GetRole(player);
}
