using TAHS.Modules;
using UnityEngine;

namespace TAHS.Roles.Crewmate;

/// <summary>
/// 美警（船员阵营，可转变为内鬼）：
/// - 手动击杀（Q）：目标是中立/内鬼则对方死亡；目标是船员则美警自杀
/// - 自动击杀：贴近深色船员一段时间后自动将其击杀（美警存活）
/// - 自动击杀满人数后转变为内鬼阵营，此后击杀 CD 跟随全局设置（此前用配置的击杀时间）
/// </summary>
public class Cop : RoleBase
{
    /// <summary>手动击杀范围</summary>
    private const float KillRange = 2.5f;

    /// <summary>深色船员颜色 ID：深蓝/黑/紫/棕/栗/灰</summary>
    private static readonly int[] DarkColorIds = { 1, 6, 8, 9, 12, 15 };

    public override string Name => "美警";
    public override string NameEn => "Cop";
    public override Faction Faction => _faction;
    public override Color Color => new(0.2f, 0.4f, 1f); // 警蓝
    public override string Description => "执法有度，伤及无辜者以命抵命。";

    private Faction _faction = Faction.Crewmate;

    /// <summary>击杀冷却剩余（转变前用配置的击杀时间，转变后跟随全局设置）</summary>
    public float KillTimer { get; private set; }

    /// <summary>已自动击杀的深色船员数</summary>
    public int AutoKillCount { get; private set; }

    /// <summary>是否已转变为内鬼</summary>
    public bool Converted => _faction == Faction.Impostor;

    private float _proximityTimer;
    private PlayerControl? _proximityTarget;

    public override void OnAssign(PlayerControl player)
    {
        base.OnAssign(player);
        KillTimer = CustomOptions.CopKillCooldown.ScaledValue;
        // 准则：带刀职业给予原版击杀按钮（无模组端也可用）
        CustomRoleManager.GrantVanillaButtons(player);
    }

    /// <summary>主机驱动（Host Only）</summary>
    public override void OnUpdate()
    {
        if (Player == null || Player.Data == null || Player.Data.IsDead) return;

        var dt = Time.fixedDeltaTime;
        if (KillTimer > 0f) KillTimer -= dt;

        // 自动击杀：贴近深色船员
        UpdateAutoKill(dt);

    }

    /// <summary>击杀船员规则（击杀按钮路径被 KillRulesPatch 拦截到这里执行）：船员按配置死亡，美警自杀抵命</summary>
    public void ExecuteCrewKill(PlayerControl target)
    {
        if (KillTimer > 0f) return;

        if (CustomOptions.CopKillCrewmateAlsoDies.Value == 1)
        {
            target.RpcMurderPlayer(target, true);
            TAHSPlugin.Log.LogInfo($"[TAHS] 美警击杀船员 {target.Data!.PlayerName}（配置：船员一并死亡）");
        }
        Player!.RpcMurderPlayer(Player, true);
        KillTimer = CustomOptions.CopKillCooldown.ScaledValue;
        TAHSPlugin.Log.LogInfo("[TAHS] 美警误杀船员，以命抵命（自杀）");
    }

    /// <summary>击杀结算（按钮路径，中立/内鬼目标）：应用击杀 CD</summary>
    public override void OnMurder(PlayerControl target)
    {
        KillTimer = Converted ? GlobalKillCooldown() : CustomOptions.CopKillCooldown.ScaledValue;
        TAHSPlugin.Log.LogInfo($"[TAHS] 美警击杀了 {target.Data!.PlayerName}（{CustomRoleManager.GetFaction(target)}）");
    }

    /// <summary>自动击杀：贴近内阁直接秒杀（无需计时、不计入转变人数）；贴近深色船员达配置时间后击杀</summary>
    private void UpdateAutoKill(float dt)
    {
        // 内阁：直接击杀（独立距离配置，不计入转变人数）
        if (!Converted)
        {
            var minister = FindNearest(CustomOptions.CopKillMinisterRange.ScaledValue);
            if (minister != null && CustomRoleManager.GetRole(minister) is Minister)
            {
                minister.RpcMurderPlayer(minister, true);
                _proximityTarget = null;
                _proximityTimer = 0f;
                TAHSPlugin.Log.LogInfo("[TAHS] 美警直接击杀了内阁（无需计时，不计入转变人数）");
                return;
            }
        }

        var nearest = FindNearest(CustomOptions.CopAutoKillRange.ScaledValue);
        if (nearest == null || !IsDarkCrewmate(nearest) || nearest != _proximityTarget)
        {
            _proximityTarget = nearest != null && IsDarkCrewmate(nearest) ? nearest : null;
            _proximityTimer = 0f;
            if (_proximityTarget == null) return;
        }

        _proximityTimer += dt;
        if (_proximityTimer < CustomOptions.CopAutoKillTime.Value) return;

        var target = _proximityTarget;
        _proximityTarget = null;
        _proximityTimer = 0f;

        target.RpcMurderPlayer(target, true);
        AutoKillCount++;
        TAHSPlugin.Log.LogInfo(
            $"[TAHS] 美警自动击杀了深色船员 {target.Data!.PlayerName}" +
            $"（{AutoKillCount}/{CustomOptions.CopAutoKillsToConvert.Value}）");

        if (!Converted && AutoKillCount >= CustomOptions.CopAutoKillsToConvert.Value)
        {
            _faction = Faction.Impostor;
            KillTimer = GlobalKillCooldown();
            TAHSPlugin.Log.LogInfo("[TAHS] 美警已转变为内鬼阵营，击杀CD跟随全局设置");
            GameArchive.RecordTransition($"美警 {Player?.Data?.PlayerName} 转变为内鬼阵营");
        }
    }

    /// <summary>深色皮肤的船员阵营玩家</summary>
    private static bool IsDarkCrewmate(PlayerControl player)
    {
        if (player.Data == null) return false;
        if (CustomRoleManager.GetFaction(player) != Faction.Crewmate) return false;
        return DarkColorIds.Contains(player.Data.DefaultOutfit.ColorId);
    }

    private PlayerControl? FindNearest(float range)
    {
        var pos = Player!.GetTruePosition();
        return PlayerControl.AllPlayerControls.ToArray()
            .Where(p => p != null && p != Player && p.Data != null && !p.Data.IsDead)
            .Where(p => Vector2.Distance(pos, p.GetTruePosition()) <= range)
            .OrderBy(p => Vector2.Distance(pos, p.GetTruePosition()))
            .FirstOrDefault();
    }

    /// <summary>全局设置的击杀冷却</summary>
    private static float GlobalKillCooldown()
    {
        var opts = GameOptionsManager.Instance?.CurrentGameOptions;
        return opts != null ? opts.GetFloat(AmongUs.GameOptions.FloatOptionNames.KillCooldown) : 30f;
    }

    public override string GetStatusText()
    {
        if (Converted)
            return "已转变为内鬼（CD跟随全局）";

        var status = $"深色猎杀 {AutoKillCount}/{CustomOptions.CopAutoKillsToConvert.Value}";
        if (_proximityTarget != null)
            status += $"（贴近 {_proximityTimer:0}/{CustomOptions.CopAutoKillTime.Value}s）";
        if (KillTimer > 0f)
            status += $" CD {KillTimer:0}s";
        return status;
    }
}
