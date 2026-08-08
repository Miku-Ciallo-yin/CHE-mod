using UnityEngine;

namespace CHE.Roles.Crewmate;

/// <summary>
/// 警长（船员阵营示例职业）：可以出刀击杀内鬼，杀错船员则自毙。
/// TODO: 击杀按钮、杀错判定、RPC 同步。
/// </summary>
public class Sheriff : RoleBase
{
    public override string Name => "警长";
    public override string NameEn => "Sheriff";
    public override Faction Faction => Faction.Crewmate;
    public override Color Color => new(1f, 0.84f, 0f); // 金色
    public override string Description => "你可以击杀内鬼，但杀错船员会付出代价。";
}
