using UnityEngine;

namespace CHE.Patches;

/// <summary>
/// 模组设置 UI 的共享状态（参考 TownOfHost_Y 的 ModGameOptionsMenu）。
/// 页签编号约定：0/1/2 为原版页签，3 起为 CHE 页签。
/// </summary>
public static class ModGameOptionsMenu
{
    /// <summary>模组设置页签</summary>
    public const int ModTabIndex = 3;

    /// <summary>职业设置页签</summary>
    public const int RolesTabIndex = 4;

    /// <summary>当前页签编号（ChangeTab 时更新）</summary>
    public static int TabIndex;

    /// <summary>职业设置页当前详情页职业 ID；null 表示职业列表页</summary>
    public static byte? DetailRoleId;

    /// <summary>克隆用模板（OnEnable 中克隆一次，隐藏保留复用）</summary>
    public static GameOptionsMenu? TemplateMenu;
    public static PassiveButton? TemplateButton;

    // 页签按钮布局常量（沿用 TownOfHost_Y 的双列布局坐标）
    public static readonly Vector3 ButtonPosLeft = new(-3.9f, -0.55f, 0f);
    public static readonly Vector3 ButtonPosRight = new(-2.4f, -0.55f, 0f);
    public static readonly Vector3 ButtonSize = new(0.45f, 0.6f, 1f);
    public const float ButtonRowStep = 0.5f;

    /// <summary>模组主题色</summary>
    public static readonly Color CheColor = new(0.31f, 0.76f, 0.97f); // #4FC3F7
}
