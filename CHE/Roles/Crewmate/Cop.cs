using CHE.Modules;
using UnityEngine;

namespace CHE.Roles.Crewmate;

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
    }

    /// <summary>主机驱动（Host Only）</summary>
    public override void OnUpdate()
    {
        if (Player == null || Player.Data == null || Player.Data.IsDead) return;

        var dt = Time.fixedDeltaTime;
        if (KillTimer > 0f) KillTimer -= dt;

        // 自动击杀：贴近深色船员
        UpdateAutoKill(dt);

        // 主机本地美警手动击杀
        if (Player.AmOwner && KillTimer <= 0f && Input.GetKeyDown(KeyCode.Q))
            TryKill();
    }

    /// <summary>非主机模组端：按 Q 向主机请求击杀</summary>
    public override void OnClientUpdate()
    {
        if (Player == null || Player.Data == null || Player.Data.IsDead) return;
        if (!Input.GetKeyDown(KeyCode.Q)) return;

        var target = FindNearest(KillRange);
        if (target != null)
            RpcSync.SendKillRequest(target.PlayerId);
    }

    /// <summary>主机：处理手动击杀请求</summary>
    public void ServerKillRequest(PlayerControl target)
    {
        if (KillTimer > 0f || Player == null || target == null) return;
        if (Vector2.Distance(Player.GetTruePosition(), target.GetTruePosition()) > KillRange) return;

        ExecuteManualKill(target);
    }

    private void TryKill()
    {
        var target = FindNearest(KillRange);
        if (target == null) return;
        ExecuteManualKill(target);
    }

    /// <summary>手动击杀规则：中立/内鬼则目标死，船员则美警自杀</summary>
    private void ExecuteManualKill(PlayerControl target)
    {
        var faction = CustomRoleManager.GetFaction(target);

        if (faction == Faction.Crewmate && !Converted)
        {
            // 误杀船员：美警自杀抵命；配置开启时船员一并死亡
            if (CustomOptions.CopKillCrewmateAlsoDies.Value == 1)
            {
                target.RpcMurderPlayer(target, true);
                CHEPlugin.Log.LogInfo($"[CHE] 美警击杀船员 {target.Data!.PlayerName}（配置：船员一并死亡）");
            }
            Player!.RpcMurderPlayer(Player, true);
            CHEPlugin.Log.LogInfo("[CHE] 美警误杀船员，以命抵命（自杀）");
            return;
        }

        target.RpcMurderPlayer(target, true);
        KillTimer = Converted ? GlobalKillCooldown() : CustomOptions.CopKillCooldown.ScaledValue;
        CHEPlugin.Log.LogInfo($"[CHE] 美警击杀了 {target.Data!.PlayerName}（{faction}）");
    }

    /// <summary>自动击杀：贴近深色船员达配置时间后将其击杀</summary>
    private void UpdateAutoKill(float dt)
    {
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
        CHEPlugin.Log.LogInfo(
            $"[CHE] 美警自动击杀了深色船员 {target.Data!.PlayerName}" +
            $"（{AutoKillCount}/{CustomOptions.CopAutoKillsToConvert.Value}）");

        if (!Converted && AutoKillCount >= CustomOptions.CopAutoKillsToConvert.Value)
        {
            _faction = Faction.Impostor;
            KillTimer = GlobalKillCooldown();
            CHEPlugin.Log.LogInfo("[CHE] 美警已转变为内鬼阵营，击杀CD跟随全局设置");
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
