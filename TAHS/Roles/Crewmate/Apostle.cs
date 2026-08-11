using UnityEngine;

namespace TAHS.Roles.Crewmate;

/// <summary>
/// 使徒（船员阵营）：
/// - 在场（存活）时所有人可使用 /kc 查看存活内鬼与中立人数（分开显示）
/// - 可看到已死亡人员的阵营（红=内鬼 灰=中立 青=船员）与死因（会议中）
/// - 每完成一个任务，随机赐予一名船员阵营玩家一个良性附加职业
/// </summary>
public class Apostle : RoleBase
{
    public override string Name => "使徒";
    public override string NameEn => "Apostle";
    public override Faction Faction => Faction.Crewmate;
    public override Color Color => new(1f, 0.95f, 0.6f); // 圣光黄
    public override string Description =>
        "在场时全员可用 /kc；能看破死者的阵营与死因；完成任务即赐福船员。";

    /// <summary>场上是否存在存活使徒（/kc 可用条件）</summary>
    public static bool AliveApostleExists()
    {
        foreach (var p in PlayerControl.AllPlayerControls)
        {
            if (p == null || p.Data == null || p.Data.IsDead) continue;
            if (CustomRoleManager.GetRole(p) is Apostle) return true;
        }
        return false;
    }

    /// <summary>本机玩家是否是使徒（无论生死都能看到死者信息）</summary>
    public static bool LocalIsApostle()
    {
        var local = PlayerControl.LocalPlayer;
        return local != null && CustomRoleManager.GetRole(local) is Apostle;
    }
}
