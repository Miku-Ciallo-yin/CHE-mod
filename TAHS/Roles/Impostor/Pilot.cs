using TAHS.Modules;
using UnityEngine;

namespace TAHS.Roles.Impostor;

/// <summary>
/// 中东机长（内鬼阵营）：
/// - 使用变形按钮（Shift）释放技能：沿当前移动方向直线冲刺，撞到障碍物后爆炸
/// - 冲刺沿途击杀（冲刺击杀范围），爆炸范围击杀（爆炸击杀范围）
/// - 冲刺移动由主机用官方 RpcSnapTo 驱动，无模组客户端同样生效
/// - 配置：技能冷却 / 是否正常击杀 / 击杀冷却 / 爆炸是否存活 / 是否误杀队友 /
///   冲刺速度 / 冲刺击杀范围 / 爆炸击杀范围
/// </summary>
public class Pilot : RoleBase
{
    /// <summary>快照步进（秒）：冲刺时主机的位置推进频率</summary>
    private const float SnapStep = 0.05f;

    public override string Name => "中东机长";
    public override string NameEn => "Pilot";
    public override Faction Faction => Faction.Impostor;
    public override Color Color => new(0.9f, 0.5f, 0.1f); // 沙橙
    public override string Description => "塔台，这是最后一程。";

    /// <summary>技能冷却剩余</summary>
    public float SkillTimer { get; private set; }

    /// <summary>是否正在冲刺</summary>
    public bool Dashing { get; private set; }

    private Vector2 _dashDir;
    private float _snapTimer;
    private Vector2 _lastPos;
    private bool _hasLastPos;

    public override void OnAssign(PlayerControl player)
    {
        base.OnAssign(player);

        // 主机把原版身份设为变形者：获得 Shift 按钮（无模组客户端也可用）
        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            player.RpcSetRole(AmongUs.GameOptions.RoleTypes.Shapeshifter);

        player.SetKillTimer(CustomOptions.PilotKillCd.ScaledValue);
    }

    /// <summary>主机驱动（Host Only）</summary>
    public override void OnUpdate()
    {
        if (Player == null || Player.Data == null || Player.Data.IsDead) return;

        var dt = Time.fixedDeltaTime;
        if (SkillTimer > 0f) SkillTimer -= dt;

        // 记录移动方向（用于冲刺方向 = 使用技能后移动的方向）
        var pos = Player.GetTruePosition();
        if (_hasLastPos && !Dashing)
        {
            var delta = pos - _lastPos;
            if (delta.sqrMagnitude > 0.0004f)
                _dashDir = delta.normalized;
        }
        _lastPos = pos;
        _hasLastPos = true;

        if (Dashing)
            DashTick(dt);
    }

    /// <summary>主机：Shift 被劫持时尝试释放技能</summary>
    public void TryStartDash()
    {
        if (SkillTimer > 0f || Dashing || Player == null || Player.Data.IsDead) return;

        if (_dashDir.sqrMagnitude < 0.01f)
            _dashDir = Vector2.right; // 无移动输入时默认向右

        Dashing = true;
        SkillTimer = CustomOptions.PilotSkillCd.ScaledValue;
        TAHSPlugin.Log.LogInfo($"[TAHS] 中东机长 {Player.Data?.PlayerName} 开始冲刺，方向 {_dashDir}");
    }

    /// <summary>冲刺推进：快照移动 + 沿途击杀 + 撞墙爆炸</summary>
    private void DashTick(float dt)
    {
        var pos = Player!.GetTruePosition();
        var speed = CustomOptions.PilotDashSpeed.ScaledValue;
        var step = speed * dt;

        // 撞墙检测（船只碰撞层）
        var hit = Physics2D.Raycast(pos, _dashDir, step + 0.3f, LayerMask.GetMask("Ship"));
        if (hit.collider != null)
        {
            Dashing = false;
            Explode(pos);
            return;
        }

        // 推进
        _snapTimer -= dt;
        if (_snapTimer <= 0f)
        {
            _snapTimer = SnapStep;
            Player.NetTransform.RpcSnapTo(pos + _dashDir * (speed * SnapStep));
        }

        // 沿途击杀
        var killRange = CustomOptions.PilotDashKillRange.ScaledValue;
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!ShouldKill(p, includeSelf: false)) continue;
            if (Vector2.Distance(pos, p.GetTruePosition()) <= killRange)
                KillVictim(p);
        }
    }

    /// <summary>爆炸：范围击杀</summary>
    private void Explode(Vector2 center)
    {
        TAHSPlugin.Log.LogInfo($"[TAHS] 中东机长 {Player!.Data?.PlayerName} 撞墙爆炸");
        GameArchive.RecordTransition($"中东机长 {Player.Data?.PlayerName} 冲刺撞墙爆炸");

        var range = CustomOptions.PilotExplosionRange.ScaledValue;
        var survive = CustomOptions.PilotSurviveExplosion.Value == 1;

        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!ShouldKill(p, includeSelf: !survive)) continue;
            if (Vector2.Distance(center, p.GetTruePosition()) <= range)
                KillVictim(p);
        }
    }

    /// <summary>击杀过滤：误杀队友开关 / 自身存活开关</summary>
    private bool ShouldKill(PlayerControl p, bool includeSelf)
    {
        if (p == null || p.Data == null || p.Data.IsDead) return false;

        if (p == Player)
            return includeSelf;

        // 队友（内鬼阵营）仅在开启误杀时被波及
        if (CustomOptions.PilotFriendlyFire.Value != 1
            && CustomRoleManager.GetFaction(p) == Faction.Impostor)
            return false;

        return true;
    }

    private void KillVictim(PlayerControl victim)
    {
        victim.RpcMurderPlayer(victim, true);
        TAHSPlugin.Log.LogInfo($"[TAHS] 中东机长技能击杀了 {victim.Data?.PlayerName}");
    }

    public override string GetStatusText()
    {
        if (Dashing) return "冲刺中！";
        return SkillTimer > 0f ? $"技能冷却 {SkillTimer:0}s" : "Shift 冲刺就绪";
    }
}
