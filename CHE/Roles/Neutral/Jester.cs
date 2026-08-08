using UnityEngine;

namespace CHE.Roles.Neutral;

/// <summary>
/// 小丑（中立阵营示例职业）：被投票放逐即单独获胜。
/// TODO: 放逐胜利判定（接管 GameOver 流程）、独立结算画面。
/// </summary>
public class Jester : RoleBase
{
    public override string Name => "小丑";
    public override string NameEn => "Jester";
    public override Faction Faction => Faction.Neutral;
    public override Color Color => new(0.93f, 0.51f, 0.93f); // 粉紫色
    public override string Description => "想办法让大家把你投出去，被放逐即获胜。";
}
