using TAHS.Modules;
using UnityEngine;

namespace TAHS.Roles.Neutral;

/// <summary>
/// TON（带刀中立）：
/// - 变形按钮选择一名玩家跟随其胜利（对方会得知 TON 的身份）
/// - 选择的玩家死亡后可重新选择（选择次数受配置限制）
/// - 拥有击杀能力，但只能击杀当前选择的玩家
/// - 击杀满配置人数后直接单独获胜
/// 配置：击杀CD / 可选择玩家次数 / 击杀多少玩家获胜 / 可否使用赌怪功能 / 可否使用管道 / 内鬼视野
/// </summary>
public class TON : RoleBase
{
    /// <summary>注册 ID（与 RoleRegistry 一致）</summary>
    public const byte RoleId = 22;

    public override string Name => "TON";
    public override string NameEn => "TON";
    public override Faction Faction => Faction.Neutral;
    public override bool IsHostileNeutral => true; // 带刀中立
    public override bool UsesShapeshiftButton => true; // 变形按钮选择跟随对象
    public override Color Color => new(0.85f, 0.7f, 0.3f); // 暗金
    public override string Description =>
        "变形选择一名玩家跟随其胜利（对方会得知你）；你只能杀死你选择的人，杀满人数直接获胜。";

    /// <summary>当前跟随的玩家（各端同步：选择经变形 RPC 广播执行）</summary>
    public byte? SelectedId { get; private set; }

    /// <summary>已击杀数（各端同步：击杀经 MurderPatch 广播执行）</summary>
    public int Kills { get; private set; }

    /// <summary>已用选择次数</summary>
    public int SelectionsUsed { get; private set; }

    /// <summary>击杀冷却剩余</summary>
    public float KillTimer { get; private set; }

    public override void OnAssign(PlayerControl player)
    {
        base.OnAssign(player);
        KillTimer = CustomOptions.TonKillCd.ScaledValue;
        // 准则：带刀职业给予原版击杀按钮（无模组端也可用）
        CustomRoleManager.GrantVanillaButtons(player);
    }

    /// <summary>主机驱动（Host Only）</summary>
    public override void OnUpdate()
    {
        if (Player == null || Player.Data == null || Player.Data.IsDead) return;
        if (KillTimer > 0f) KillTimer -= Time.fixedDeltaTime;

        // 跟随对象已死亡：清空，可重新选择
        if (SelectedId is { } id)
        {
            var selected = FindPlayer(id);
            if (selected == null || selected.Data == null || selected.Data.IsDead)
                SelectedId = null;
        }
    }

    /// <summary>选择跟随对象（各端执行；提示与日志仅主机）</summary>
    public void Select(PlayerControl target)
    {
        var host = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
        System.Action<string> tell = msg => { if (host) ChatHelper.ShowPrivate(Player, msg); };

        if (Player == null || Player.Data == null || Player.Data.IsDead) return;
        if (target == null || target.Data == null || target.Data.IsDead) return;
        if (target == Player)
        {
            tell("[TAHS] 不能选择自己");
            return;
        }

        // 当前跟随对象仍存活：不可换选
        if (SelectedId is { } current)
        {
            var cur = FindPlayer(current);
            if (cur != null && cur.Data != null && !cur.Data.IsDead)
            {
                tell("[TAHS] 当前跟随对象仍存活，不能更换");
                return;
            }
        }

        if (SelectionsUsed >= CustomOptions.TonSelectCount.Value)
        {
            tell("[TAHS] 选择次数已用完");
            return;
        }

        SelectedId = target.PlayerId;
        SelectionsUsed++;

        if (host)
        {
            ChatHelper.ShowPrivate(Player,
                $"[TAHS] 你选择跟随 {target.Data.PlayerName} 胜利（第 {SelectionsUsed}/{CustomOptions.TonSelectCount.Value} 次选择）");
            ChatHelper.ShowPrivate(target,
                $"[TAHS] {Player.Data.PlayerName} 是 TON（带刀中立），TA 选择了跟随你胜利");
            TAHSPlugin.Log.LogInfo($"[TAHS] TON {Player.Data.PlayerName} 选择跟随 {target.Data.PlayerName}");
            GameArchive.RecordTransition($"TON {Player.Data.PlayerName} 选择跟随 {target.Data.PlayerName}");
        }
    }

    /// <summary>击杀规则：只能杀当前跟随对象且冷却完毕（CheckMurderPatch 调用）</summary>
    public bool CanKill(PlayerControl target)
    {
        return SelectedId == target.PlayerId && KillTimer <= 0f;
    }

    /// <summary>击杀结算（各端执行；达到击杀目标时主机结束游戏）</summary>
    public override void OnMurder(PlayerControl target)
    {
        KillTimer = CustomOptions.TonKillCd.ScaledValue;
        Kills++;

        if (SelectedId == target.PlayerId)
            SelectedId = null;

        if (Kills >= CustomOptions.TonKillsToWin.Value)
        {
            TAHSPlugin.Log.LogInfo($"[TAHS] TON {Player?.Data?.PlayerName} 击杀满 {Kills} 人，直接获胜");
            GameArchive.RecordTransition($"TON {Player?.Data?.PlayerName} 击杀满 {Kills} 人直接获胜");
            // 各端登记胜利者（结算画面读取本地状态），主机负责结束游戏
            CustomRoleManager.SetCustomWinner(Player);
            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost
                && GameManager.Instance != null)
                GameManager.Instance.RpcEndGame(GameOverReason.ImpostorDisconnect, false);
        }
    }

    private static PlayerControl? FindPlayer(byte playerId)
    {
        return PlayerControl.AllPlayerControls.ToArray()
            .FirstOrDefault(p => p != null && p.PlayerId == playerId);
    }

    public override string GetStatusText()
    {
        var sel = SelectedId is { } id ? FindPlayer(id)?.Data?.PlayerName ?? "?" : "未选择";
        var baseText = $"跟随:{sel} 击杀 {Kills}/{CustomOptions.TonKillsToWin.Value} 选择 {SelectionsUsed}/{CustomOptions.TonSelectCount.Value}";
        return KillTimer > 0f ? $"{baseText} CD {KillTimer:0}s" : baseText;
    }
}
