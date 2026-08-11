using TAHS.Roles;

namespace TAHS.Modules;

/// <summary>
/// 击杀记录：记录每位被害者的击杀者（名字 + 当时职业/身份），供 /d 指令查询。
/// 对局结束/重开时清空。
/// </summary>
public static class DeathTracker
{
    private static readonly Dictionary<byte, string> _killerInfoByVictim = new();

    /// <summary>记录一次击杀（MurderPlayer 补丁调用）</summary>
    public static void Record(PlayerControl killer, PlayerControl victim)
    {
        if (killer == null || victim == null || killer.Data == null) return;

        var role = CustomRoleManager.GetRole(killer);
        var roleName = role != null
            ? role.Name
            : (killer.Data.Role != null && killer.Data.Role.IsImpostor ? "内鬼" : "船员");

        _killerInfoByVictim[victim.PlayerId] = $"{killer.Data.PlayerName}（{roleName}）";
    }

    /// <summary>查询被害者的击杀者信息，无记录返回 null</summary>
    public static string? GetKillerInfo(byte victimId)
    {
        return _killerInfoByVictim.TryGetValue(victimId, out var info) ? info : null;
    }

    public static void Clear() => _killerInfoByVictim.Clear();
}
