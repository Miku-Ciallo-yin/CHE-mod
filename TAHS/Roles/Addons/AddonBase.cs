using UnityEngine;

namespace TAHS.Roles.Addons;

/// <summary>附加职业类别（用于职业设置页分组）</summary>
public enum AddonType
{
    /// <summary>良性：对持有者有利（使徒只会赐予此类）</summary>
    Benign,
    /// <summary>恶性：对持有者不利</summary>
    Malignant,
    /// <summary>内鬼专属</summary>
    Impostor,
}

/// <summary>
/// 附加职业基类。附加职业叠加在主职业之上，一名玩家可同时拥有主职业和多个附加职业。
/// 新附加职业：继承本类并在 <see cref="CustomRoleManager"/> 的 AddonRegistry 中注册。
/// </summary>
public abstract class AddonBase
{
    /// <summary>注册表 ID（分配时写入）</summary>
    public byte Id { get; internal set; }

    /// <summary>附加职业名（中文）</summary>
    public abstract string Name { get; }

    /// <summary>附加职业名（英文）</summary>
    public abstract string NameEn { get; }

    /// <summary>主题色</summary>
    public abstract Color Color { get; }

    /// <summary>附加职业类别（默认良性）</summary>
    public virtual AddonType Type => AddonType.Benign;

    /// <summary>良性附加职业（使徒完成任务时只会赐予良性附加）</summary>
    public bool IsBenign => Type == AddonType.Benign;

    /// <summary>使徒完成任务时是否可赐予该附加职业（默认可）</summary>
    public virtual bool ApostleGrantable => true;

    /// <summary>附加职业说明（/m 展示，空则不显示）</summary>
    public virtual string Description => string.Empty;

    /// <summary>拥有该附加职业的玩家</summary>
    public PlayerControl? Player { get; private set; }

    /// <summary>分配时调用</summary>
    public virtual void OnAssign(PlayerControl player)
    {
        Player = player;
    }
}
