using UnityEngine;

namespace TAHS.Roles.Addons;

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

    /// <summary>良性附加职业（使徒完成任务时只会赐予良性附加）</summary>
    public virtual bool IsBenign => true;

    /// <summary>拥有该附加职业的玩家</summary>
    public PlayerControl? Player { get; private set; }

    /// <summary>分配时调用</summary>
    public virtual void OnAssign(PlayerControl player)
    {
        Player = player;
    }
}
