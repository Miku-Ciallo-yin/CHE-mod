namespace TAHS.Roles;

/// <summary>
/// 阵营。参考 TONE：除船员/内鬼外，增加中立阵营。
/// </summary>
public enum Faction
{
    /// <summary>船员阵营</summary>
    Crewmate,

    /// <summary>内鬼阵营</summary>
    Impostor,

    /// <summary>中立阵营（独立胜利条件）</summary>
    Neutral,
}
