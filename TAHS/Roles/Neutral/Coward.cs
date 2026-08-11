using TAHS.Modules;
using UnityEngine;

namespace TAHS.Roles.Neutral;

/// <summary>
/// 懦弱者（敌对中立）：
/// - 开局带刀，击杀冷却跟随全局设置；按 Q 击杀最近玩家（主机本地）或请求主机击杀
/// - 击杀满 3 人后，持续贴近某名玩家一段时间会转变为该玩家的阵营：
///   - 船员：转变为船员并失去击杀能力
///   - 内鬼：转变为内鬼（保留击杀）
///   - 中立：转变为该中立玩家的职业并与其共同胜利；
///     任何一方之后转变阵营或职业，共同胜利失效
/// </summary>
public class Coward : RoleBase
{
    /// <summary>击杀范围</summary>
    private const float KillRange = 2.5f;

    /// <summary>转变阵营需要的击杀数（职业设置中可调）</summary>
    private static int KillsToConvert => CustomOptions.CowardKillsToConvert.Value;

    /// <summary>转变阵营所需贴近时间（秒，职业设置中可调）</summary>
    private static float ConvertTime => CustomOptions.CowardConvertTime.Value;

    /// <summary>转变阵营所需贴近距离（职业设置中可调）</summary>
    private static float ConvertRange => CustomOptions.CowardConvertRange.ScaledValue;

    public override string Name => "懦弱者";
    public override string NameEn => "Coward";
    public override Faction Faction => _faction;
    public override bool IsHostileNeutral => true; // 敌对中立
    public override Color Color => new(0.6f, 0.6f, 0.6f); // 灰色
    public override string Description => "杀够三人后，找个靠山吧。";

    private Faction _faction = Faction.Neutral;

    /// <summary>已击杀人数</summary>
    public int KillCount { get; private set; }

    /// <summary>击杀冷却剩余</summary>
    public float KillTimer { get; private set; }

    /// <summary>是否还拥有击杀能力（转化为船员或中立职业后失去）</summary>
    public bool HasKillAbility => !_converted || _faction == Faction.Impostor;

    /// <summary>共同胜利链接的伙伴</summary>
    public PlayerControl? LinkedPlayer { get; private set; }

    /// <summary>共同胜利链接是否有效（一方转变阵营/职业后失效）</summary>
    public bool LinkActive { get; private set; }

    private bool _converted;
    private string? _adoptedRoleName;
    private byte _linkedRoleId;
    private float _proximityTimer;
    private PlayerControl? _proximityTarget;

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

        var dt = Time.fixedDeltaTime;
        if (KillTimer > 0f) KillTimer -= dt;

        if (!_converted)
        {
            if (KillCount >= KillsToConvert)
                UpdateProximity(dt);
        }

        CheckLink();
    }

    /// <summary>贴近转化：杀满 3 人后持续贴近同一名玩家则转变为其阵营</summary>
    private void UpdateProximity(float dt)
    {
        var nearest = FindNearest(ConvertRange);
        if (nearest == null || nearest != _proximityTarget)
        {
            _proximityTarget = nearest;
            _proximityTimer = 0f;
            if (nearest == null) return;
        }

        _proximityTimer += dt;
        if (_proximityTimer >= ConvertTime)
            ConvertTo(nearest);
    }

    private void ConvertTo(PlayerControl target)
    {
        _converted = true;
        _proximityTarget = null;
        _proximityTimer = 0f;

        var faction = CustomRoleManager.GetFaction(target);
        switch (faction)
        {
            case Faction.Crewmate:
                _faction = Faction.Crewmate;
                CustomRoleManager.RevokeVanillaButtons(Player!); // 失去击杀能力，回收按钮
                TAHSPlugin.Log.LogInfo("[TAHS] 懦弱者转变为船员阵营，失去击杀能力");
                GameArchive.RecordTransition($"懦弱者 {Player?.Data?.PlayerName} 转变为船员阵营，失去击杀能力");
                break;

            case Faction.Impostor:
                _faction = Faction.Impostor;
                TAHSPlugin.Log.LogInfo("[TAHS] 懦弱者转变为内鬼阵营");
                GameArchive.RecordTransition($"懦弱者 {Player?.Data?.PlayerName} 转变为内鬼阵营");
                break;

            case Faction.Neutral:
                _faction = Faction.Neutral;
                CustomRoleManager.RevokeVanillaButtons(Player!); // 变为对方职业，回收按钮
                var role = CustomRoleManager.GetRole(target);
                _adoptedRoleName = role?.Name ?? "中立";
                LinkedPlayer = target;
                _linkedRoleId = role?.Id ?? 0;
                LinkActive = role != null;
                TAHSPlugin.Log.LogInfo(
                    $"[TAHS] 懦弱者转变为中立职业「{_adoptedRoleName}」，" +
                    $"与 {target.Data?.PlayerName} 共同胜利");
                GameArchive.RecordTransition($"懦弱者 {Player?.Data?.PlayerName} 转变为中立职业「{_adoptedRoleName}」，与 {target.Data?.PlayerName} 共同胜利");
                break;
        }
    }

    /// <summary>链接检查：伙伴转变阵营或职业则共同胜利失效</summary>
    private void CheckLink()
    {
        if (LinkedPlayer == null || !LinkActive) return;

        if (CustomRoleManager.GetFaction(LinkedPlayer) != Faction.Neutral
            || CustomRoleManager.GetRole(LinkedPlayer)?.Id != _linkedRoleId)
        {
            LinkActive = false;
            TAHSPlugin.Log.LogInfo("[TAHS] 懦弱者的共同胜利链接已失效（伙伴转变了阵营或职业）");
            GameArchive.RecordTransition($"懦弱者 {Player?.Data?.PlayerName} 的共同胜利链接失效");
        }
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
        if (!_converted)
        {
            if (KillCount < KillsToConvert)
                return $"击杀 {KillCount}/{KillsToConvert}" +
                       (KillTimer > 0f ? $"（冷却 {KillTimer:0}s）" : "");
            return $"贴近转化 {_proximityTimer:0}/{ConvertTime:0}s";
        }

        return _faction switch
        {
            Faction.Crewmate => "已转变为船员（失去击杀）",
            Faction.Impostor => "已转变为内鬼",
            _ => LinkActive
                ? $"已转变为{_adoptedRoleName}（共同胜利）"
                : $"已转变为{_adoptedRoleName}（共同胜利失效）",
        };
    }
}
