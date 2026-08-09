using UnityEngine;

namespace CHE.Roles.Addons;

/// <summary>
/// 赌怪（附加职业）：会议中可点击其他玩家名牌前的准星，猜测其职业。
/// 猜对目标死亡，猜错自己死亡。猜测目标是否包含附加职业由配置项控制。
/// 会议 UI 与判定逻辑见 Patches/GuesserPatch。
/// </summary>
public class Guesser : AddonBase
{
    /// <summary>注册 ID（与 RoleRegistry 职业 ID 同空间，从 4 起）</summary>
    public const byte AddonId = 4;

    public override string Name => "赌怪";
    public override string NameEn => "Guesser";
    public override Color Color => new(1f, 0.6f, 0.2f); // 橙色
}
