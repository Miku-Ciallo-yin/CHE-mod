using TAHS.Modules;
using TAHS.Roles.Addons;
using UnityEngine;

namespace TAHS.Roles.Crewmate;

/// <summary>
/// 法军（船员阵营）：
/// - 拥有击杀能力（原版击杀按钮，CD 跟随全局设置）
/// - 被内鬼击杀时不会死亡，而是被"缴械"：获得叛徒附加职业并失去击杀能力；
///   对他出刀的内鬼击杀 CD 被重置（见 Patches/KillRulesPatch）
/// </summary>
public class FrenchArmy : RoleBase
{
    /// <summary>注册 ID（与 RoleRegistry 一致）</summary>
    public const byte RoleId = 16;

    public override string Name => "法军";
    public override string NameEn => "FrenchArmy";
    public override Faction Faction => Faction.Crewmate;
    public override Color Color => new(0.35f, 0.55f, 1f); // 法式蓝
    public override string Description =>
        "拥有击杀能力；被内鬼击杀时不会死亡，而是缴械成为叛徒（失去击杀，跟随内鬼胜利）。";

    /// <summary>击杀冷却剩余（跟随全局设置）</summary>
    public float KillTimer { get; private set; }

    /// <summary>是否已被缴械（获得叛徒附加、失去击杀能力）</summary>
    public bool Disarmed => Player != null && CustomRoleManager.HasAddon(Player, Traitor.AddonId);

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

    /// <summary>击杀结算：CD 跟随全局设置</summary>
    public override void OnMurder(PlayerControl target)
    {
        KillTimer = GlobalKillCooldown();
    }

    /// <summary>
    /// 被内鬼出刀时触发（KillRulesPatch 拦截，各端执行）：不会死亡。
    /// 各端：重置出刀内鬼的击杀 CD；主机：缴械并赐予叛徒附加（RPC 广播各端）。
    /// </summary>
    public void OnAttackedByImpostor(PlayerControl killer)
    {
        killer.SetKillTimer(GlobalKillCooldown());

        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
        if (Player == null || Player.Data == null) return;

        CustomRoleManager.RevokeVanillaButtons(Player); // 失去击杀能力
        CustomRoleManager.GrantAddon(Player, Traitor.AddonId);
        RpcSync.BroadcastAddonGrant(Player.PlayerId, Traitor.AddonId);

        ChatHelper.ShowPrivate(Player, "[TAHS] 你被内鬼击杀，缴械成为叛徒（跟随内鬼胜利）");
        ChatHelper.ShowPrivate(killer, "[TAHS] 目标是法军，击杀无效，击杀冷却已重置");
        TAHSPlugin.Log.LogInfo($"[TAHS] 法军 {Player.Data.PlayerName} 被内鬼击杀，缴械成为叛徒");
        GameArchive.RecordTransition($"法军 {Player.Data.PlayerName} 被内鬼击杀，缴械成为叛徒");
    }

    /// <summary>全局设置的击杀冷却</summary>
    private static float GlobalKillCooldown()
    {
        var opts = GameOptionsManager.Instance?.CurrentGameOptions;
        return opts != null ? opts.GetFloat(AmongUs.GameOptions.FloatOptionNames.KillCooldown) : 30f;
    }

    public override string GetStatusText()
    {
        if (Disarmed) return "已缴械（叛徒）";
        return KillTimer > 0f ? $"冷却 {KillTimer:0}s" : string.Empty;
    }
}
