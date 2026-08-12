using UnityEngine;

namespace TAHS.Roles;

/// <summary>
/// 职业基类。每个职业实例绑定一名玩家。
/// 新职业：继承本类并在 <see cref="CustomRoleManager"/> 中注册。
/// </summary>
public abstract class RoleBase
{
    /// <summary>注册表 ID（分配时写入，RPC 同步用）</summary>
    public byte Id { get; internal set; }

    /// <summary>职业名（中文）</summary>
    public abstract string Name { get; }

    /// <summary>职业名（英文）</summary>
    public abstract string NameEn { get; }

    /// <summary>所属阵营</summary>
    public abstract Faction Faction { get; }

    /// <summary>敌对中立（如带刀中立）。猜测模式的"友好中立/敌对中立"开关据此区分，默认为友好</summary>
    public virtual bool IsHostileNeutral => false;

    /// <summary>是否使用原版变形按钮（Shift）释放技能——此类职业保留 Shift 按钮显示，其余假内鬼隐藏</summary>
    public virtual bool UsesShapeshiftButton => false;

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

    /// <summary>该职业的玩家被投票放逐时调用（小丑类职业在此判定获胜）</summary>
    public virtual void OnExile()
    {
    }

    /// <summary>本机玩家持有该职业时，每个 FixedUpdate 调用（驱动技能逻辑）。仅在主机上对所有玩家调用（Host Only 架构）</summary>
    public virtual void OnUpdate()
    {
    }

    /// <summary>非主机模组端的自身输入处理（如佃农按 Q 请求击杀），主机验证后执行</summary>
    public virtual void OnClientUpdate()
    {
    }

    /// <summary>该职业的玩家成功击杀目标时调用</summary>
    public virtual void OnMurder(PlayerControl target)
    {
    }

    /// <summary>名字下方显示的状态行（如冷却、进度），空字符串表示不显示</summary>
    public virtual string GetStatusText() => string.Empty;
}
