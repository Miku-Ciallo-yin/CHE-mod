using TAHS.Modules;
using UnityEngine;

namespace TAHS.Roles.Impostor;

/// <summary>
/// 埋雷兵（内鬼阵营）：
/// - 用变形按钮（Shift）在当前位置放置地雷
/// - 地雷放置后显示，配置秒数后隐形；走入范围的玩家被炸死（会议中不触发，不随会议清除）
/// - 数量达上限后放置会移除最早的地雷
/// 配置：埋雷CD / 地雷显示时间 / 地雷判定范围 / 地雷数量上限 / 地雷是否击杀内鬼
/// </summary>
public class Miner : RoleBase
{
    private class Mine
    {
        public int Index;
        public Vector2 Pos;
        public float Range;
        public float ArmTimer; // 放置后短暂布防时间
    }

    /// <summary>布防时间（秒）：放置后地雷不立即触发</summary>
    private const float ArmDelay = 1f;

    private readonly List<Mine> _mines = new();
    private float _cdTimer;
    private int _nextIndex;

    public override string Name => "埋雷兵";
    public override string NameEn => "Miner";
    public override Faction Faction => Faction.Impostor;
    public override Color Color => new(0.55f, 0.35f, 0.15f); // 土黄
    public override string Description => "小心脚下。Shift 放雷，过会儿它就看不见了。";

    /// <summary>技能挂原版变形按钮</summary>
    public override bool UsesShapeshiftButton => true;

    public override void OnAssign(PlayerControl player)
    {
        base.OnAssign(player);
        // 准则：技能职业给予原版变形按钮用于释放技能
        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            player.RpcSetRole(AmongUs.GameOptions.RoleTypes.Shapeshifter);
    }

    /// <summary>主机驱动（Host Only）</summary>
    public override void OnUpdate()
    {
        if (Player == null || Player.Data == null || Player.Data.IsDead) return;

        var dt = Time.fixedDeltaTime;
        if (_cdTimer > 0f) _cdTimer -= dt;

        if (MeetingHud.Instance != null) return; // 会议中不触发

        for (var i = _mines.Count - 1; i >= 0; i--)
        {
            var mine = _mines[i];
            if (mine.ArmTimer > 0f)
            {
                mine.ArmTimer -= dt;
                continue;
            }

            var victim = FindVictim(mine);
            if (victim == null) continue;

            // 触发：范围内全部有效目标被炸死，地雷消耗
            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (!ValidVictim(p)) continue;
                if (Vector2.Distance(mine.Pos, p.GetTruePosition()) <= mine.Range)
                {
                    p.RpcMurderPlayer(p, true);
                    TAHSPlugin.Log.LogInfo($"[TAHS] 地雷炸死了 {p.Data?.PlayerName}");
                }
            }
            _mines.RemoveAt(i);
            RpcSync.SendMineSync(2, mine.Index, Vector2.zero, 0f, 0f);
        }
    }

    private PlayerControl? FindVictim(Mine mine)
    {
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (!ValidVictim(p)) continue;
            if (Vector2.Distance(mine.Pos, p.GetTruePosition()) <= mine.Range)
                return p;
        }
        return null;
    }

    /// <summary>有效目标：存活、非埋雷兵本人、（配置关闭时）非内鬼</summary>
    private bool ValidVictim(PlayerControl p)
    {
        if (p == null || p.Data == null || p.Data.IsDead) return false;
        if (p == Player) return false;
        if (CustomOptions.MinerKillImpostor.Value != 1
            && CustomRoleManager.GetFaction(p) == Faction.Impostor)
            return false;
        return true;
    }

    /// <summary>主机：放置地雷（Shift 劫持触发）</summary>
    public void PlaceMine()
    {
        if (_cdTimer > 0f || Player == null || Player.Data.IsDead) return;

        _cdTimer = CustomOptions.MinerCd.ScaledValue;

        // 超上限：移除最早的地雷
        if (_mines.Count >= CustomOptions.MinerMaxCount.Value)
        {
            var oldest = _mines[0];
            _mines.RemoveAt(0);
            RpcSync.SendMineSync(2, oldest.Index, Vector2.zero, 0f, 0f);
        }

        var mine = new Mine
        {
            Index = _nextIndex++,
            Pos = Player.GetTruePosition(),
            Range = CustomOptions.MinerRange.ScaledValue,
            ArmTimer = ArmDelay,
        };
        _mines.Add(mine);

        RpcSync.SendMineSync(1, mine.Index, mine.Pos, mine.Range, CustomOptions.MinerVisibleTime.Value);
        TAHSPlugin.Log.LogInfo($"[TAHS] 埋雷兵放置了地雷 #{mine.Index}（当前 {_mines.Count}/{CustomOptions.MinerMaxCount.Value}）");
    }

    public override string GetStatusText()
    {
        var status = $"地雷 {_mines.Count}/{CustomOptions.MinerMaxCount.Value}";
        if (_cdTimer > 0f) status += $" CD {_cdTimer:0}s";
        return status;
    }
}
