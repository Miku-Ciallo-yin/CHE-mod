using Hazel;
using InnerNet;
using UnityEngine;

namespace TAHS.Modules;

/// <summary>
/// 首刀保护（参考开源模组）：上一局第一个死亡的玩家，本局不能被首刀。
/// - 保护对象名字前有蓝色十字前缀（主机广播改名，含无模组端全员可见）；
/// - 本局首次击杀发生后护盾不立刻消失，而是延迟 1~5 秒（主机每局随机）后消失，
///   消失时前缀一并去除；
/// - 击杀走 CheckMurder 主机验证流，主机在 MurderPlayer 前缀拦截，无模组端出刀同样被挡。
/// </summary>
public static class FirstKillProtection
{
    /// <summary>蓝色十字前缀（全角加号，保证游戏字体可渲染）</summary>
    private const string PrefixDisplay = "<color=#55AAFF>＋</color>";

    /// <summary>本局保护对象（上一局首死者）</summary>
    private static byte? _protectedId;
    private static string? _protectedCode;

    /// <summary>本局首个死亡的玩家（下一局的保护对象）</summary>
    private static byte? _firstVictimId;
    private static string? _firstVictimCode;
    private static bool _deathRecorded;

    /// <summary>护盾延迟消失的时长（每局随机 1~5 秒，种子全网一致）与到期时间</summary>
    private static float _shieldDuration;
    private static float _shieldExpireAt = -1f;

    /// <summary>对局计数（配合 GameId 做全网一致的随机种子）</summary>
    private static int _gameCounter;

    private static float _nameRefreshTimer;

    private static bool IsHost => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;

    /// <summary>新对局开始（各端应用分配结果时调用）：结转上一局首死者为本局保护对象</summary>
    public static void OnNewGame()
    {
        _protectedId = _firstVictimId;
        _protectedCode = _firstVictimCode;
        _firstVictimId = null;
        _firstVictimCode = null;
        _deathRecorded = false;
        _shieldExpireAt = -1f;
        // 每局随机 1~5 秒；种子 = 房间 GameId ^ 对局计数，各端结果一致
        var seed = (AmongUsClient.Instance?.GameId ?? 0) ^ (++_gameCounter * 7919);
        _shieldDuration = 1f + (float)new System.Random(seed).NextDouble() * 4f;

        // 主机：立即给保护对象挂上蓝色十字前缀
        if (IsHost && _protectedId is { } id)
        {
            var player = FindPlayer(id);
            if (player != null) BroadcastName(player, true);
        }
    }

    /// <summary>击杀成功时记录（MurderPatch 调用）：记下首死者供下局保护；首杀触发护盾消失倒计时</summary>
    public static void RecordDeath(PlayerControl victim)
    {
        if (_deathRecorded || victim == null || victim.Data == null) return;
        _deathRecorded = true;
        _firstVictimId = victim.PlayerId;
        _firstVictimCode = victim.Data.FriendCode;

        // 护盾不立刻消失：延迟 _shieldDuration 秒后才失效
        if (_protectedId != null)
            _shieldExpireAt = Time.time + _shieldDuration;
    }

    /// <summary>目标当前是否处于首刀保护中（护盾倒计时期间仍受保护）</summary>
    public static bool IsProtected(PlayerControl target)
    {
        if (CustomOptions.FirstKillProtection.Value != 1) return false;
        if (_protectedId == null || target == null || target.Data == null) return false;
        if (_shieldExpireAt >= 0f && Time.time >= _shieldExpireAt) return false; // 护盾已消失
        if (target.PlayerId != _protectedId.Value) return false;
        // 好友代码双重校验，防止玩家进出房间后 PlayerId 串号
        return !string.IsNullOrEmpty(_protectedCode) && target.Data.FriendCode == _protectedCode;
    }

    /// <summary>某玩家名字应带的蓝色十字前缀（不在保护中返回 null；PrivateTag 合成名字用）</summary>
    public static string? NamePrefixFor(byte playerId)
    {
        return _protectedId == playerId ? PrefixDisplay : null;
    }

    /// <summary>每帧驱动（AnnouncementPatch 调用）：主机负责护盾到期消失与前缀刷新</summary>
    public static void Tick()
    {
        if (!IsHost || _protectedId == null) return;

        // 护盾到期：解除保护并去除前缀
        if (_shieldExpireAt >= 0f && Time.time >= _shieldExpireAt)
        {
            var player = FindPlayer(_protectedId.Value);
            if (player != null) BroadcastName(player, false);
            _protectedId = null;
            _protectedCode = null;
            _shieldExpireAt = -1f;
            return;
        }

        // 定期刷新前缀（防止被游戏同步/改名覆盖）
        _nameRefreshTimer -= Time.deltaTime;
        if (_nameRefreshTimer > 0f) return;
        _nameRefreshTimer = 2f;

        var target = FindPlayer(_protectedId.Value);
        if (target != null) BroadcastName(target, true);
    }

    /// <summary>主机广播改名：withPrefix 时加蓝色十字前缀，否则恢复原名</summary>
    private static void BroadcastName(PlayerControl player, bool withPrefix)
    {
        var baseName = player.Data?.PlayerName;
        if (string.IsNullOrEmpty(baseName)) return;

        // 剥离标签行、颜色标记与已有十字前缀，防止嵌套叠加
        var clean = System.Text.RegularExpressions.Regex
            .Replace(baseName.Split('\n')[0], "</?color[^>]*>", string.Empty)
            .TrimStart('＋');

        var name = withPrefix ? PrefixDisplay + clean : clean;

        var writer = AmongUsClient.Instance.StartRpcImmediately(
            player.NetId, (byte)RpcCalls.SetName, SendOption.Reliable, -1);
        writer.Write(name);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
        player.SetName(name); // 主机本地应用
    }

    private static PlayerControl? FindPlayer(byte playerId)
    {
        return PlayerControl.AllPlayerControls.ToArray()
            .FirstOrDefault(p => p != null && p.PlayerId == playerId);
    }
}
