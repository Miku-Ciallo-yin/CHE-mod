using TAHS.Modules;
using UnityEngine;

namespace TAHS.Roles.Neutral;

/// <summary>
/// 失忆者（中立阵营，友好/无刀，参考 TONE 的 Amnesiac）：
/// 报告尸体时不召开会议，而是"记起"并接替死者的身份与阵营（含其职业技能）；
/// 死者也是失忆者时无事发生。原版报告按钮即技能键，无模组端同样可用。
/// </summary>
public class Amnesiac : RoleBase
{
    /// <summary>注册 ID（与 RoleRegistry 一致）</summary>
    public const byte RoleId = 19;

    public override string Name => "失忆者";
    public override string NameEn => "Amnesiac";
    public override Faction Faction => Faction.Neutral;
    public override Color Color => new(0.8f, 0.8f, 0.75f); // 灰白
    public override string Description => "我是谁？报告尸体即可记起并接替死者的身份与阵营。";

    /// <summary>主机：记起死者身份（报告尸体时由 ReportMeetingPatch 调用）</summary>
    public static void Remember(PlayerControl amnesiac, PlayerControl? dead)
    {
        if (amnesiac.Data == null || amnesiac.Data.IsDead) return;

        if (dead == null || dead.Data == null)
        {
            ChatHelper.ShowPrivate(amnesiac, "[TAHS] 这具尸体上没有残留的记忆");
            return;
        }

        var deadRole = CustomRoleManager.GetRole(dead);
        if (deadRole is Amnesiac)
        {
            ChatHelper.ShowPrivate(amnesiac, "[TAHS] 对方也是失忆者，无身份可记");
            return;
        }

        if (deadRole != null)
        {
            // 接替模组职业（含阵营与技能）
            var copy = CustomRoleManager.CreateRoleOfType(deadRole);
            if (copy != null)
            {
                CustomRoleManager.TransformToRole(amnesiac, copy);
                TAHSPlugin.Log.LogInfo($"[TAHS] 失忆者 {amnesiac.Data.PlayerName} 记起了 {copy.Name}（接替 {dead.Data.PlayerName}）");
                GameArchive.RecordTransition($"失忆者 {amnesiac.Data.PlayerName} 记起了「{copy.Name}」");
                ChatHelper.ShowPrivate(amnesiac, $"[TAHS] 你记起了自己的身份：{copy.Name}（{copy.Faction}）");
                return;
            }
        }

        // 死者无模组职业：接替其原版身份
        var wasImpostor = dead.Data.Role != null && dead.Data.Role.IsImpostor;
        CustomRoleManager.RemoveRole(amnesiac);
        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            amnesiac.RpcSetRole(wasImpostor
                ? AmongUs.GameOptions.RoleTypes.Impostor
                : AmongUs.GameOptions.RoleTypes.Crewmate);

        var vanillaName = wasImpostor ? "内鬼" : "船员";
        TAHSPlugin.Log.LogInfo($"[TAHS] 失忆者 {amnesiac.Data.PlayerName} 记起了原版身份：{vanillaName}");
        GameArchive.RecordTransition($"失忆者 {amnesiac.Data.PlayerName} 记起了原版身份「{vanillaName}」");
        ChatHelper.ShowPrivate(amnesiac, $"[TAHS] 你记起了自己的身份：{vanillaName}");
    }
}
