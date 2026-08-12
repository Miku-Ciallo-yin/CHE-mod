using UnityEngine;

namespace TAHS.Roles.Addons;

/// <summary>
/// 叛徒（良性附加职业）：获得后原本的胜利条件失效，改为跟随内鬼胜负。
/// 配置项：是否记入内鬼阵营人数 / 是否与内鬼互认 / 是否与其他叛徒互认。
/// 胜利结算见 Patches/EndGamePatch，人数判定见 Patches/TraitorPatch，红名互认见 ImpostorVisionPatch。
/// </summary>
public class Traitor : AddonBase
{
    /// <summary>注册 ID（与 RoleRegistry 职业 ID 同空间）</summary>
    public const byte AddonId = 15;

    public override string Name => "叛徒";
    public override string NameEn => "Traitor";
    public override Color Color => new(1f, 0.3f, 0.3f); // 内鬼红

    /// <summary>良性分类（使徒可赐予）</summary>
    public override AddonType Type => AddonType.Benign;

    public override string Description =>
        "你的原胜利条件已失效，改为跟随内鬼胜负（本身不获得击杀能力）。";

    /// <summary>玩家是否是叛徒</summary>
    public static bool IsTraitor(PlayerControl? player)
    {
        return player != null && CustomRoleManager.HasAddon(player, AddonId);
    }
}
