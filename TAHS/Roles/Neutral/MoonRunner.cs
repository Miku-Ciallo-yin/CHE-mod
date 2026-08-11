using TAHS.Modules;
using UnityEngine;

namespace TAHS.Roles.Neutral;

/// <summary>
/// 月跑入机（友好中立）：
/// - Q 对最近玩家使用技能：目标获得速度增益（初始值×叠加倍率^次数），技能期间月跑入机无敌
/// - 同一目标叠满次数达到最大值：双方身份自动透露（可配置）且无法被赌
/// - 已有最大值玩家后再增益他人 → 前者获得只能击杀后者的能力+箭头，
///   前者在后者死前无法被击杀/被投票；后者显示红色追杀标记；
///   前者限期未击杀后者则自杀且无法胜利，后者存活则与月跑入机跟随胜利
/// - 同时 2 人增益达到最大值 → 月跑入机与这些人立刻胜利
/// </summary>
public class MoonRunner : RoleBase
{
    /// <summary>技能范围</summary>
    private const float SkillRange = 2.5f;

    /// <summary>同时达到最大值直接胜利所需人数</summary>
    private const int InstantWinMaxedCount = 2;

    private class BuffState
    {
        public int Stacks;
        public float Timer;
        public bool Maxed;
    }

    private readonly Dictionary<byte, BuffState> _buffs = new();
    private readonly HashSet<byte> _unguessable = new();
    private PlayerControl? _maxed;   // 当前达到最大值的玩家
    private PlayerControl? _hunter;  // 前者（追杀者）
    private PlayerControl? _prey;    // 后者（被追杀者）
    private bool _hunterWasImpostor; // 追杀者原本身份是否为内鬼系（链接结束恢复用）
    private float _huntTimer;
    private float _huntCd;
    private float _skillTimer;

    /// <summary>后者死亡前，前者（hunterId -> preyId）受保护</summary>
    public static readonly Dictionary<byte, byte> HunterPrey = new();

    /// <summary>身份被透露的玩家（playerId -> 显示文本）</summary>
    public static readonly Dictionary<byte, string> Revealed = new();

    /// <summary>跟随胜利的幸存者（结算时并入胜利名单）</summary>
    public static readonly List<PlayerControl> CoWinners = new();

    public override string Name => "月跑入机";
    public override string NameEn => "MoonRunner";
    public override Faction Faction => Faction.Neutral;
    public override bool IsHostileNeutral => false; // 友好中立
    public override Color Color => new(0.55f, 0.9f, 0.9f); // 月白青
    public override string Description => "赐人神速，亦赐人杀机。";

    /// <summary>清理静态状态（对局重置时由 CustomRoleManager 调用）</summary>
    public static void ResetStatics()
    {
        HunterPrey.Clear();
        Revealed.Clear();
        CoWinners.Clear();
    }

    public override void OnReset()
    {
        _buffs.Clear();
        _unguessable.Clear();
        _maxed = null;
        _hunter = null;
        _prey = null;
    }

    /// <summary>主机驱动（Host Only）</summary>
    public override void OnUpdate()
    {
        if (Player == null || Player.Data == null || Player.Data.IsDead) return;

        var dt = Time.fixedDeltaTime;
        if (_skillTimer > 0f) _skillTimer -= dt;
        if (_huntCd > 0f) _huntCd -= dt;

        // 增益到期：层数清零、最大值失效
        foreach (var (pid, st) in _buffs)
        {
            if (st.Timer <= 0f) continue;
            st.Timer -= dt;
            if (st.Timer > 0f) continue;
            st.Stacks = 0;
            st.Maxed = false;
            _unguessable.Remove(pid);
            Revealed.Remove(pid);
            if (_maxed != null && _maxed.PlayerId == pid) _maxed = null;
        }

        // 主机本地按 Q 使用技能
        if (Player.AmOwner && _skillTimer <= 0f && Input.GetKeyDown(KeyCode.Q))
        {
            var target = FindNearest(SkillRange);
            if (target != null) UseSkill(target);
        }

        // 无模组客户端的增益加速：主机按官方 RpcSnapTo 做位移助推
        // （速度由各自客户端计算，模组端本地加速，无模组端只能由主机助推）
        _snapTimer -= dt;
        if (_snapTimer <= 0f)
        {
            _snapTimer = SnapInterval;
            SnapBoostTick();
        }

        UpdateHunt(dt);
    }

    /// <summary>快照助推间隔（秒）</summary>
    private const float SnapInterval = 0.2f;
    private float _snapTimer;
    private readonly Dictionary<byte, Vector2> _lastPos = new();

    /// <summary>对无模组客户端的增益玩家按移动方向助推</summary>
    private void SnapBoostTick()
    {
        foreach (var (pid, st) in _buffs)
        {
            if (st.Timer <= 0f) continue;
            var p = FindByPlayerId(pid);
            if (p == null || p.Data == null || p.Data.IsDead) continue;
            if (PlayerIdManager.IsModdedClient(p)) continue; // 模组端本地加速，无需助推

            var pos = p.GetTruePosition();
            if (_lastPos.TryGetValue(pid, out var last))
            {
                var delta = pos - last;
                if (delta.sqrMagnitude > 0.0001f)
                {
                    var mult = GetSpeedMultiplier(p);
                    if (mult > 1f)
                    {
                        var baseSpeed = GameOptionsManager.Instance.CurrentGameOptions
                            .GetFloat(AmongUs.GameOptions.FloatOptionNames.PlayerSpeedMod);
                        var extra = delta.normalized * (baseSpeed * (mult - 1f) * SnapInterval);
                        p.NetTransform.RpcSnapTo(pos + extra);
                    }
                }
            }
            _lastPos[pid] = pos;
        }
    }

    /// <summary>非主机模组端：按 Q 请求使用技能</summary>
    public override void OnClientUpdate()
    {
        if (Player == null || Player.Data == null || Player.Data.IsDead) return;
        if (!Input.GetKeyDown(KeyCode.Q)) return;

        var target = FindNearest(SkillRange);
        if (target != null)
            RpcSync.SendKillRequest(target.PlayerId); // 复用击杀请求通道，主机按职业路由到 UseSkill
    }

    /// <summary>主机：使用技能（增益目标）</summary>
    public void UseSkill(PlayerControl target)
    {
        if (_skillTimer > 0f || Player == null || target == null || target.Data == null || target.Data.IsDead) return;
        if (Vector2.Distance(Player.GetTruePosition(), target.GetTruePosition()) > SkillRange) return;

        _skillTimer = CustomOptions.MoonSkillCd.ScaledValue;

        // 已有最大值玩家且增益了别人 → 前者获得追杀后者的能力
        if (_maxed != null && !_maxed.Data.IsDead && target != _maxed && _hunter == null)
            StartHunt(_maxed, target);

        var st = GetBuff(target);
        st.Stacks++;
        st.Timer = CustomOptions.MoonBuffDuration.ScaledValue;
        TAHSPlugin.Log.LogInfo($"[TAHS] 月跑入机增益了 {target.Data.PlayerName}（{st.Stacks} 层）");

        if (!st.Maxed && st.Stacks >= CustomOptions.MoonBuffMaxStacks.Value)
        {
            st.Maxed = true;
            OnMaxed(target);
        }
    }

    /// <summary>达到最大值：身份透露（可配置）+ 无法被赌 + 直接胜利判定</summary>
    private void OnMaxed(PlayerControl target)
    {
        _maxed = target;

        if (CustomOptions.MoonReveal.Value == 1)
        {
            Revealed[Player!.PlayerId] = $"{Name}（月跑入机）";
            var targetRole = CustomRoleManager.GetRole(target);
            var targetName = targetRole?.Name ?? (target.Data!.Role != null && target.Data.Role.IsImpostor ? "内鬼" : "船员");
            Revealed[target.PlayerId] = targetName;
        }

        _unguessable.Add(Player!.PlayerId);
        _unguessable.Add(target.PlayerId);

        GameArchive.RecordTransition($"月跑入机 {Player.Data?.PlayerName} 与 {target.Data?.PlayerName} 增益达到最大值，双方身份透露");

        // 同时 N 人达到最大值 → 立刻胜利
        var maxedCount = _buffs.Values.Count(b => b.Maxed);
        if (maxedCount >= InstantWinMaxedCount)
            InstantWin();
    }

    /// <summary>直接胜利：月跑入机 + 全部最大值玩家</summary>
    private void InstantWin()
    {
        var winners = new List<PlayerControl> { Player! };
        foreach (var (pid, st) in _buffs)
        {
            if (!st.Maxed) continue;
            var p = FindByPlayerId(pid);
            if (p != null) winners.Add(p);
        }

        TAHSPlugin.Log.LogInfo("[TAHS] 月跑入机与最大值玩家直接胜利");
        GameArchive.RecordTransition($"月跑入机 {Player!.Data?.PlayerName} 与 {winners.Count - 1} 名最大值玩家直接胜利");
        CustomRoleManager.SetCustomWinners(winners);
        GameManager.Instance.RpcEndGame(GameOverReason.ImpostorDisconnect, false);
    }

    /// <summary>前者获得只能击杀后者的能力，进入追杀链接</summary>
    private void StartHunt(PlayerControl hunter, PlayerControl prey)
    {
        _hunter = hunter;
        _prey = prey;
        HunterPrey[hunter.PlayerId] = prey.PlayerId;
        _huntTimer = CustomOptions.MoonHuntSuicideTime.Value;
        _huntCd = 0f;

        // 赋予原版内鬼系职业（变形者）：获得击杀按钮，无模组客户端也可用；
        // 链接结束后恢复原身份
        _hunterWasImpostor = hunter.Data != null && hunter.Data.Role != null && hunter.Data.Role.IsImpostor;
        hunter.RpcSetRole(AmongUs.GameOptions.RoleTypes.Shapeshifter);

        TAHSPlugin.Log.LogInfo($"[TAHS] 追杀开始：{hunter.Data?.PlayerName} → {prey.Data?.PlayerName}（已赋予击杀按钮）");
        GameArchive.RecordTransition($"{hunter.Data?.PlayerName} 获得追杀 {prey.Data?.PlayerName} 的能力（击杀按钮）");
    }

    /// <summary>追杀计时与结算</summary>
    private void UpdateHunt(float dt)
    {
        if (_hunter == null || _prey == null) return;

        // 一方死亡：链接结束（后者被击杀即追杀完成）
        if (_hunter.Data == null || _hunter.Data.IsDead || _prey.Data == null || _prey.Data.IsDead)
        {
            if (_prey.Data != null && _prey.Data.IsDead)
                GameArchive.RecordTransition($"{_hunter.Data?.PlayerName} 追杀了 {_prey.Data?.PlayerName}");
            EndHunt();
            return;
        }

        _huntTimer -= dt;
        if (_huntTimer > 0f) return;

        // 超时：前者自杀且无法胜利；后者与月跑入机跟随胜利
        var hunter = _hunter;
        var prey = _prey;
        EndHunt();

        CustomRoleManager.TransformToRole(hunter, new DoomedNeutral());
        hunter.RpcMurderPlayer(hunter, true);
        if (!CoWinners.Contains(prey)) CoWinners.Add(prey);
        if (!CoWinners.Contains(Player!)) CoWinners.Add(Player!);

        TAHSPlugin.Log.LogInfo($"[TAHS] 追杀超时：{hunter.Data?.PlayerName} 自杀且无法胜利，{prey.Data?.PlayerName} 将与月跑入机共同胜利");
        GameArchive.RecordTransition($"{hunter.Data?.PlayerName} 追杀超时自杀（无法胜利），{prey.Data?.PlayerName} 与月跑入机将共同胜利");
    }

    private void EndHunt()
    {
        if (_hunter != null)
        {
            HunterPrey.Remove(_hunter.PlayerId);
            // 回收击杀按钮：恢复原身份
            if (_hunter.Data != null && !_hunter.Data.IsDead)
                _hunter.RpcSetRole(_hunterWasImpostor ? AmongUs.GameOptions.RoleTypes.Impostor : AmongUs.GameOptions.RoleTypes.Crewmate);
        }
        _hunter = null;
        _prey = null;
    }

    /// <summary>主机：追杀者击杀（只能杀后者，受追杀 CD 限制）</summary>
    public static void ServerHunterKill(PlayerControl hunter, PlayerControl target)
    {
        var runner = FindByHunter(hunter.PlayerId);
        if (runner == null || runner._prey == null || runner._hunter == null) return;
        if (target == null || target.PlayerId != runner._prey.PlayerId) return; // 只能击杀后者
        if (runner._huntCd > 0f) return;

        target.RpcMurderPlayer(target, true);
        runner._huntCd = CustomOptions.MoonHuntCd.ScaledValue;
        TAHSPlugin.Log.LogInfo($"[TAHS] {hunter.Data?.PlayerName} 完成追杀 {target.Data?.PlayerName}");
        GameArchive.RecordTransition($"{hunter.Data?.PlayerName} 追杀了 {target.Data?.PlayerName}");
        runner.EndHunt();
    }

    // ===== 静态查询（供补丁聚合判定） =====

    /// <summary>目标当前速度倍率（无增益返回 1）</summary>
    public static float GetSpeedMultiplier(PlayerControl player)
    {
        var mult = 1f;
        foreach (var runner in Runners())
        {
            if (!runner._buffs.TryGetValue(player.PlayerId, out var st) || st.Timer <= 0f) continue;
            var m = CustomOptions.MoonBuffInitial.ScaledValue;
            for (var i = 1; i < st.Stacks; i++)
                m *= CustomOptions.MoonBuffRate.ScaledValue;
            mult = Mathf.Max(mult, m);
        }
        return mult;
    }

    /// <summary>月跑入机使用技能期间无敌（有激活增益时）</summary>
    public static bool HasActiveBuffAnywhere(PlayerControl player)
    {
        foreach (var runner in Runners())
        {
            if (runner.Player == null || runner.Player.PlayerId != player.PlayerId) continue;
            foreach (var st in runner._buffs.Values)
                if (st.Timer > 0f) return true;
        }
        return false;
    }

    /// <summary>该玩家是否无法被赌（月跑入机或最大值玩家）</summary>
    public static bool IsUnguessableAnywhere(PlayerControl player)
    {
        foreach (var runner in Runners())
            if (runner._unguessable.Contains(player.PlayerId)) return true;
        return false;
    }

    /// <summary>该玩家是否为受保护的追杀者（后者死前无法被击杀/投票）</summary>
    public static bool IsProtectedHunter(PlayerControl player)
    {
        if (!HunterPrey.TryGetValue(player.PlayerId, out var preyId)) return false;
        var prey = FindByPlayerId(preyId);
        return prey != null && prey.Data != null && !prey.Data.IsDead;
    }

    /// <summary>该玩家是否为被追杀的后者</summary>
    public static bool IsPrey(PlayerControl player)
    {
        foreach (var preyId in HunterPrey.Values)
            if (preyId == player.PlayerId) return true;
        return false;
    }

    private static MoonRunner? FindByHunter(byte hunterId)
    {
        foreach (var runner in Runners())
            if (runner._hunter != null && runner._hunter.PlayerId == hunterId)
                return runner;
        return null;
    }

    private static IEnumerable<MoonRunner> Runners()
    {
        foreach (var role in CustomRoleManager.ActiveRoles)
            if (role is MoonRunner runner) yield return runner;
    }

    private static PlayerControl? FindByPlayerId(byte playerId)
    {
        return PlayerControl.AllPlayerControls.ToArray()
            .FirstOrDefault(p => p != null && p.PlayerId == playerId);
    }

    private BuffState GetBuff(PlayerControl target)
    {
        if (!_buffs.TryGetValue(target.PlayerId, out var st))
            _buffs[target.PlayerId] = st = new BuffState();
        return st;
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

    public override string GetStatusText()
    {
        if (_hunter != null && _prey != null)
            return $"追杀：{_prey.Data?.PlayerName}（{_huntTimer:0}s）";
        var maxed = _buffs.Values.Count(b => b.Maxed);
        var status = $"最大值 {maxed}/{InstantWinMaxedCount}";
        if (_skillTimer > 0f) status += $" CD {_skillTimer:0}s";
        return status;
    }
}
