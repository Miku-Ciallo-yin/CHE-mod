using TAHS.Roles;

namespace TAHS.Modules;

/// <summary>
/// 击杀记录：记录每位被害者的击杀者（名字 + 当时职业/身份），供 /d 指令查询。
/// 对局结束/重开时清空。
/// </summary>
public static class DeathTracker
{
    private static readonly Dictionary<byte, string> _killerInfoByVictim = new();
    private static readonly Dictionary<byte, string> _causeByVictim = new();

    /// <summary>记录一次击杀（MurderPlayer 补丁调用）</summary>
    public static void Record(PlayerControl killer, PlayerControl victim)
    {
        if (killer == null || victim == null || killer.Data == null) return;

        var role = CustomRoleManager.GetRole(killer);
        var roleName = role != null
            ? role.Name
            : (killer.Data.Role != null && killer.Data.Role.IsImpostor ? "内鬼" : "船员");

        _killerInfoByVictim[victim.PlayerId] = $"{killer.Data.PlayerName}（{roleName}）";
        _causeByVictim[victim.PlayerId] = killer == victim ? "自杀" : "击杀";
    }

    /// <summary>记录放逐（ExileController 补丁调用）</summary>
    public static void RecordExile(PlayerControl exiled)
    {
        if (exiled == null) return;
        _causeByVictim[exiled.PlayerId] = "放逐";
    }

    /// <summary>查询被害者的击杀者信息，无记录返回 null</summary>
    public static string? GetKillerInfo(byte victimId)
    {
        return _killerInfoByVictim.TryGetValue(victimId, out var info) ? info : null;
    }

    /// <summary>查询死因（击杀/自杀/放逐），无记录返回 null</summary>
    public static string? GetCause(byte victimId)
    {
        return _causeByVictim.TryGetValue(victimId, out var cause) ? cause : null;
    }

    public static void Clear()
    {
        _killerInfoByVictim.Clear();
        _causeByVictim.Clear();
    }
}
