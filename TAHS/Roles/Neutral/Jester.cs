using UnityEngine;

namespace TAHS.Roles.Neutral;

/// <summary>
/// 小丑（中立阵营）：被投票放逐即单独获胜。
/// </summary>
public class Jester : RoleBase
{
    public override string Name => "小丑";
    public override string NameEn => "Jester";
    public override Faction Faction => Faction.Neutral;
    public override Color Color => new(0.93f, 0.51f, 0.93f); // 粉紫色
    public override string Description => "想办法让大家把你投出去，被放逐即获胜。";

    /// <summary>被投票放逐：判定小丑单独获胜并结束游戏</summary>
    public override void OnExile()
    {
        // 测试模式：不触发小丑胜利结束游戏
        if (TAHS.Modules.CustomOptions.TestMode.Value == 1) return;

        // 叛徒附加：原本胜利条件失效（跟随内鬼），被投出不触发小丑胜利
        if (Player != null && CustomRoleManager.HasAddon(Player, TAHS.Roles.Addons.Traitor.AddonId)) return;

        CustomRoleManager.SetCustomWinner(Player);

        // 只有房主有权结束游戏，其他端通过 RPC 同步结算
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
        if (GameManager.Instance == null) return;

        TAHSPlugin.Log.LogInfo("[TAHS] 小丑被投出，游戏结束");
        GameManager.Instance.RpcEndGame(GameOverReason.CrewmatesByVote, false);
    }
}
