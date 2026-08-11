using UnityEngine;

namespace TAHS.Roles.Neutral;

/// <summary>
/// 自弃者（内部标记职业，不注册不分配）：
/// 用于"无法胜利"判定——追杀超时自杀的追杀者会被转变为此职业（中立阵营，
/// 不被任何一方结算为胜利）。
/// </summary>
public class DoomedNeutral : RoleBase
{
    public override string Name => "自弃者";
    public override string NameEn => "Doomed";
    public override Faction Faction => Faction.Neutral;
    public override bool IsHostileNeutral => true;
    public override Color Color => Color.black;
}
