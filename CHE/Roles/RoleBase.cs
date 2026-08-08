using UnityEngine;

namespace CHE.Roles;

/// <summary>
/// 职业基类。每个职业实例绑定一名玩家。
/// 新职业：继承本类并在 <see cref="CustomRoleManager"/> 中注册。
/// </summary>
public abstract class RoleBase
{
    /// <summary>职业名（中文）</summary>
    public abstract string Name { get; }

    /// <summary>职业名（英文）</summary>
    public abstract string NameEn { get; }

    /// <summary>所属阵营</summary>
    public abstract Faction Faction { get; }

    /// <summary>职业主题色</summary>
    public abstract Color Color { get; }

    /// <summary>职业描述（开局介绍用）</summary>
    public virtual string Description => string.Empty;

    /// <summary>拥有该职业的玩家</summary>
    public PlayerControl? Player { get; private set; }

    /// <summary>名字旁显示用的颜色标签，如 &lt;color=#FFD700&gt;</summary>
    public string ColorTag => $"<color=#{ColorUtility.ToHtmlStringRGB(Color)}>";

    /// <summary>分配职业时调用</summary>
    public virtual void OnAssign(PlayerControl player)
    {
        Player = player;
    }

    /// <summary>游戏开始时调用（分配完成后）</summary>
    public virtual void OnGameStart()
    {
    }

    /// <summary>游戏结束 / 会议开始时重置状态</summary>
    public virtual void OnReset()
    {
    }
}
