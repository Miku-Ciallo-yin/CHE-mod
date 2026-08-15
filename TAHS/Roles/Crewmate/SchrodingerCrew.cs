using TAHS.Modules;
using UnityEngine;

namespace TAHS.Roles.Crewmate;

/// <summary>
/// 薛定谔的船员（船员阵营，带刀）：
/// - 每一轮（两次会议之间）必须击杀一名玩家，否则在下一次会议开始时自杀
/// - 死亡后跟随内鬼胜利（存活时按船员结算）；击杀 CD 跟随全局设置
/// 结算调整见 Patches/EndGamePatch，轮次判定见 Patches/SchrodingerPatch。
/// </summary>
public class SchrodingerCrew : RoleBase
{
    /// <summary>注册 ID（与 RoleRegistry 一致）</summary>
    public const byte RoleId = 21;

    public override string Name => "薛定谔的船员";
    public override string NameEn => "Schrodinger";
    public override Faction Faction => Faction.Crewmate;
    public override Color Color => new(0.7f, 0.85f, 1f); // 量子淡蓝
    public override string Description => "每一轮都必须猎杀一人，否则散会即死。死亡后跟随内鬼胜利。";

    /// <summary>本轮是否已击杀（会议开始时重置；未击杀则自杀）</summary>
    public bool KilledThisRound { get; private set; }

    /// <summary>击杀冷却剩余（跟随全局设置）</summary>
    public float KillTimer { get; private set; }

    public override void OnAssign(PlayerControl player)
    {
        base.OnAssign(player);
        KillTimer = GlobalKillCooldown(); // 开局冷却跟随全局设置
        // 准则：带刀职业给予原版击杀按钮（无模组端也可用）
        CustomRoleManager.GrantVanillaButtons(player);
    }

    /// <summary>主机驱动（Host Only）</summary>
    public override void OnUpdate()
    {
        if (Player == null || Player.Data == null || Player.Data.IsDead) return;
        if (KillTimer > 0f) KillTimer -= Time.fixedDeltaTime;
    }

    /// <summary>击杀结算：标记本轮已猎杀 + CD 跟随全局设置</summary>
    public override void OnMurder(PlayerControl target)
    {
        KilledThisRound = true;
        KillTimer = GlobalKillCooldown();
    }

    /// <summary>新一轮开始（会议结算后）</summary>
    public void NewRound() => KilledThisRound = false;

    /// <summary>全局设置的击杀冷却</summary>
    private static float GlobalKillCooldown()
    {
        var opts = GameOptionsManager.Instance?.CurrentGameOptions;
        return opts != null ? opts.GetFloat(AmongUs.GameOptions.FloatOptionNames.KillCooldown) : 30f;
    }

    public override string GetStatusText()
    {
        var hunt = KilledThisRound ? "本轮已猎杀" : "未猎杀（散会即死）";
        return KillTimer > 0f ? $"{hunt} CD {KillTimer:0}s" : hunt;
    }
}
