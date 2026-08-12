using Object = UnityEngine.Object;
using TAHS.Modules;
using TAHS.Roles;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace TAHS.Patches;

/// <summary>
/// TAB 职业介绍悬浮层（参考 TOHE 的 RoleSummary，仅模组端）：
/// 对局中按 TAB 在画面左上角显示/隐藏本局已开启职业及简介（按阵营分节）。
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class RoleIntroPatch
{
    private static GameObject? _root;
    private static TextMeshPro? _text;

    public static void Postfix(HudManager __instance)
    {
        if (!Input.GetKeyDown(KeyCode.Tab)) return;
        if (AmongUsClient.Instance == null
            || AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started)
            return;

        // 聊天框/设置菜单打开时不响应，避免与界面操作冲突
        var hud = HudManager.Instance;
        if (hud == null || (hud.Chat != null && hud.Chat.IsOpenOrOpening)) return;
        if (GameSettingMenu.Instance != null && GameSettingMenu.Instance.gameObject.activeSelf) return;

        Toggle(__instance);
    }

    private static void Toggle(HudManager hud)
    {
        if (_root == null) Create(hud);
        if (_root == null) return;

        if (_root.activeSelf)
        {
            Close();
            return;
        }

        if (_text != null) _text.text = BuildContent();
        _root.SetActive(true);
    }

    private static void Create(HudManager hud)
    {
        var template = hud.GetComponentInChildren<TextMeshPro>(true);
        if (template == null) return;

        _root = new GameObject("TAHS_RoleIntro");
        _root.transform.SetParent(hud.transform, false);
        _root.transform.localPosition = new Vector3(-5.2f, 2.9f, -100f);

        _text = Object.Instantiate(template, _root.transform);
        _text.DestroyTranslator();
        _text.alignment = TextAlignmentOptions.TopLeft;
        _text.fontSize = 1.5f;
        _text.color = Color.white;
        _text.outlineWidth = 0.2f;
        _text.outlineColor = Color.black;
        _text.enableWordWrapping = false;
        _text.rectTransform.sizeDelta = new Vector2(12f, 6f);
        _root.SetActive(false);
    }

    public static void Close()
    {
        if (_root != null) Object.Destroy(_root);
        _root = null;
        _text = null;
    }

    /// <summary>悬浮层内容：已开启职业按阵营分节，带一句话简介</summary>
    private static string BuildContent()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<color=#4FC3F7><b>===== 本局职业介绍 =====</b></color> <size=70%>(TAB 关闭)</size>\n");
        AppendSection(sb, Faction.Crewmate, "#66E6FF", "船员职业");
        AppendSection(sb, Faction.Impostor, "#FF5555", "内鬼职业");
        AppendSection(sb, Faction.Neutral, "#999999", "中立职业");

        var first = true;
        foreach (var (id, addon) in CustomRoleManager.GetAddonSamples())
        {
            if (CustomOptions.GetRoleChance(id) <= 0) continue;
            if (first)
            {
                sb.Append("<color=#FFB84D><b>—— 附加职业 ——</b></color>\n");
                first = false;
            }
            AppendLine(sb, addon.Name, addon.Description);
        }
        return sb.ToString();
    }

    private static void AppendSection(System.Text.StringBuilder sb, Faction faction, string color, string title)
    {
        var first = true;
        foreach (var (id, role) in CustomRoleManager.GetRoleSamples())
        {
            if (role.Faction != faction) continue;
            if (CustomOptions.GetRoleChance(id) <= 0) continue;
            if (first)
            {
                sb.Append($"<color={color}><b>—— {title} ——</b></color>\n");
                first = false;
            }
            AppendLine(sb, role.Name, role.Description);
        }
    }

    private static void AppendLine(System.Text.StringBuilder sb, string name, string description)
    {
        sb.Append(name);
        if (!string.IsNullOrEmpty(description))
            sb.Append($"<size=75%>：{description}</size>");
        sb.Append('\n');
    }
}
