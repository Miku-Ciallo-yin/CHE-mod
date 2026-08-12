namespace TAHS.Modules;

/// <summary>
/// 首刀保护（参考开源模组）：上一局第一个死亡的玩家，本局不能被首刀
/// （本局首次击杀发生后保护自动失效）。击杀走 CheckMurder 主机验证流，
/// 主机在 MurderPlayer 前缀拦截即可，无模组端出刀同样被挡。
/// </summary>
public static class FirstKillProtection
{
    /// <summary>本局保护对象（上一局首死者）</summary>
    private static byte? _protectedId;
    private static string? _protectedCode;

    /// <summary>本局首个死亡的玩家（下一局的保护对象）</summary>
    private static byte? _firstVictimId;
    private static string? _firstVictimCode;
    private static bool _deathRecorded;

    /// <summary>新对局开始（各端应用分配结果时调用）：结转上一局首死者为本局保护对象</summary>
    public static void OnNewGame()
    {
        _protectedId = _firstVictimId;
        _protectedCode = _firstVictimCode;
        _firstVictimId = null;
        _firstVictimCode = null;
        _deathRecorded = false;
    }

    /// <summary>击杀成功时记录（MurderPatch 调用）：首杀发生 → 保护失效，记下首死者供下局保护</summary>
    public static void RecordDeath(PlayerControl victim)
    {
        if (_deathRecorded || victim == null || victim.Data == null) return;
        _deathRecorded = true;
        _protectedId = null;
        _protectedCode = null;
        _firstVictimId = victim.PlayerId;
        _firstVictimCode = victim.Data.FriendCode;
    }

    /// <summary>目标当前是否处于首刀保护中</summary>
    public static bool IsProtected(PlayerControl target)
    {
        if (CustomOptions.FirstKillProtection.Value != 1) return false;
        if (_protectedId == null || target == null || target.Data == null) return false;
        if (target.PlayerId != _protectedId.Value) return false;
        // 好友代码双重校验，防止玩家进出房间后 PlayerId 串号
        return !string.IsNullOrEmpty(_protectedCode) && target.Data.FriendCode == _protectedCode;
    }
}
