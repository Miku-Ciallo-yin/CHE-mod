using TAHS.Modules;
using UnityEngine;

namespace TAHS.Roles.Impostor;

/// <summary>
/// 忏悔者（内鬼阵营）：
/// - 带刀内鬼（CD 跟随全局设置），按 Q 击杀
/// - 击杀满配置人数后，按 F 使用变形转变为船员阵营
/// - 转变后失去击杀能力，并在配置秒数后自裁；自裁者转为中立（无法胜利）
/// </summary>
public class Repenter : RoleBase
{
    /// <summary>击杀范围</summary>
    private const float KillRange = 2.5f;

    public override string Name => "忏悔者";
    public override string NameEn => "Repenter";
    public override Faction Faction => _faction;
    public override Color Color => new(0.5f, 0.2f, 0.7f); // 暗紫
    public override string Description => "放下屠刀，以死赎罪。";

    private Faction _faction = Faction.Impostor;

    /// <summary>已击杀人数</summary>
    public int KillCount { get; private set; }

    /// <summary>击杀冷却剩余（跟随全局设置）</summary>
    public float KillTimer { get; private set; }

    /// <summary>是否已转变为船员（变形完成）</summary>
    public bool Converted => _converted;

    /// <summary>是否已自裁（无法胜利）</summary>
    public bool Suicided => _suicided;

    private bool _converted;
    private bool _suicided;
    private float _suicideTimer;

    /// <summary>是否可以变形（击杀满配置人数且未转变）</summary>
    public bool CanConvert => !_converted && KillCount >= CustomOptions.RepenterKillsToConvert.Value;

    public override void OnAssign(PlayerControl player)
    {
        base.OnAssign(player);
        KillTimer = GlobalKillCooldown();
        // 准则：技能职业给予原版变形按钮用于释放技能（变形者保留击杀按钮）
        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            player.RpcSetRole(AmongUs.GameOptions.RoleTypes.Shapeshifter);
    }

    /// <summary>主机驱动（Host Only）</summary>
    public override void OnUpdate()
    {
        if (Player == null || Player.Data == null || Player.Data.IsDead) return;

        var dt = Time.fixedDeltaTime;
        if (KillTimer > 0f) KillTimer -= dt;

        if (!_converted)
            return;

        // 转变后：自裁倒计时
        _suicideTimer -= dt;
        if (_suicideTimer <= 0f)
            Suicide();
    }

    /// <summary>主机：处理变形请求（验证击杀数后转变）</summary>
    public void ServerConvert()
    {
        if (!CanConvert) return;
        Convert();
    }

    /// <summary>变形：转变为船员阵营，失去击杀能力，开始自裁倒计时</summary>
    private void Convert()
    {
        _converted = true;
        _faction = Faction.Crewmate;
        CustomRoleManager.RevokeVanillaButtons(Player!); // 失去击杀能力，回收按钮
        _suicideTimer = CustomOptions.RepenterSuicideTime.Value;
        TAHSPlugin.Log.LogInfo($"[TAHS] 忏悔者变形为船员，{_suicideTimer:0} 秒后自裁");
        GameArchive.RecordTransition($"忏悔者 {Player?.Data?.PlayerName} 变形为船员阵营");
    }

    /// <summary>自裁：转为中立（无法胜利）后死亡</summary>
    private void Suicide()
    {
        if (_suicided) return;
        _suicided = true;
        _faction = Faction.Neutral; // 自裁者不属于任何阵营，无法胜利
        Player!.RpcMurderPlayer(Player, true);
        TAHSPlugin.Log.LogInfo("[TAHS] 忏悔者自裁（无法胜利）");
        GameArchive.RecordTransition($"忏悔者 {Player?.Data?.PlayerName} 自裁（无法胜利）");
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

    private static float GlobalKillCooldown()
    {
        var opts = GameOptionsManager.Instance?.CurrentGameOptions;
        return opts != null ? opts.GetFloat(AmongUs.GameOptions.FloatOptionNames.KillCooldown) : 30f;
    }

    public override string GetStatusText()
    {
        if (_suicided) return "已自裁";
        if (_converted) return $"船员（{_suicideTimer:0}s 后自裁）";
        if (CanConvert) return "按 [F] 变形为船员";
        var status = $"击杀 {KillCount}/{CustomOptions.RepenterKillsToConvert.Value}";
        if (KillTimer > 0f) status += $"（冷却 {KillTimer:0}s）";
        return status;
    }
}
