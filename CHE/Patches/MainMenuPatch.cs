using CHE.Modules;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace CHE.Patches;

/// <summary>
/// TONE 风格主菜单（参考 TONEX MainMenuManagerPatch）：
/// - Logo 区下方加版本徽章 "CHE vX by 米裤恰油"
/// - 底部加一行自定义按钮：关于 CHE / GitHub / 交流群
/// 按钮克隆原版 creditsButton，DestroyTranslator 后设文字，AspectPosition 锚点定位。
/// </summary>
[HarmonyPatch(typeof(MainMenuManager))]
public static class MainMenuPatch
{
    private static readonly Color CheColor = new(0.31f, 0.76f, 0.97f); // #4FC3F7

    private const string AboutText =
        "<b>CHE</b> — Among Us 职业模组\n" +
        "开发者：米裤恰油\n\n" +
        "<color=#4FC3F7>职业</color>：警长 / 佃农 / 小丑\n" +
        "<color=#4FC3F7>附加职业</color>：赌怪\n\n" +
        "<color=#4FC3F7>聊天指令</color>：/start [秒] 倒计时开局 · /end 强制结束\n" +
        "<color=#4FC3F7>快捷键</color>：ALT+F4 强制结束对局\n" +
        "<color=#4FC3F7>设置</color>：大厅 → 游戏设置 → 编辑 → 模组设置 / 职业设置";

    [HarmonyPatch(nameof(MainMenuManager.Start)), HarmonyPostfix]
    public static void StartPostfix(MainMenuManager __instance)
    {
        try
        {
            var tmpTemplate = __instance.quitButton.transform
                .FindChild("FontPlacer").GetChild(0).GetComponent<TextMeshPro>();

            CustomPopup.Setup(__instance.quitButton, tmpTemplate);
            CreateVersionBadge(__instance, tmpTemplate);
            CreateButtons(__instance);

            CHEPlugin.Log.LogInfo("[CHE] 主菜单定制已创建");
        }
        catch (System.Exception e)
        {
            CHEPlugin.Log.LogError($"[CHE] 主菜单定制失败: {e}");
        }
    }

    [HarmonyPatch(nameof(MainMenuManager.LateUpdate)), HarmonyPostfix]
    public static void LateUpdatePostfix() => CustomPopup.Update();

    /// <summary>Logo 区下方的版本徽章</summary>
    private static void CreateVersionBadge(MainMenuManager menu, TextMeshPro tmpTemplate)
    {
        var badge = Object.Instantiate(tmpTemplate, menu.playButton.transform.parent);
        badge.DestroyTranslator();
        badge.text = $"<color=#4FC3F7>CHE v{CHEPlugin.Version} by 米裤恰油</color>";
        badge.fontSize = 3.2f;

        var aspect = badge.GetComponent<AspectPosition>();
        if (aspect == null) aspect = badge.gameObject.AddComponent<AspectPosition>();
        aspect.anchorPoint = new Vector2(0.23f, 0.66f);
        aspect.updateAlways = true;
    }

    /// <summary>底部一行自定义按钮（克隆 creditsButton 窄按钮）</summary>
    private static void CreateButtons(MainMenuManager menu)
    {
        var template = menu.creditsButton.gameObject;

        CreateButton(menu, template, "关于 CHE", new Vector2(0.35f, 0.42f),
            () => CustomPopup.Show(menu.transform, "关于 CHE", AboutText));
        CreateButton(menu, template, "GitHub", new Vector2(0.5f, 0.42f),
            () => Application.OpenURL(ModConfig.GithubUrl.Value));
        CreateButton(menu, template, "交流群", new Vector2(0.65f, 0.42f),
            () => Application.OpenURL(ModConfig.CommunityUrl.Value));
    }

    private static void CreateButton(MainMenuManager menu, GameObject template, string text, Vector2 anchor, System.Action action)
    {
        var button = Object.Instantiate(template, template.transform.parent);
        button.name = "CHE_" + text;
        button.gameObject.SetActive(true);

        var label = button.transform.FindChild("FontPlacer").GetChild(0).gameObject;
        label.DestroyTranslator();
        var tmp = label.GetComponent<TextMeshPro>();
        tmp.text = text;
        tmp.color = CheColor;

        var passive = button.GetComponent<PassiveButton>();
        passive.OnClick.RemoveAllListeners();
        passive.OnClick.AddListener((UnityAction)(System.Action)(() => action()));
        if (passive.inactiveSprites != null)
            passive.inactiveSprites.GetComponent<SpriteRenderer>().color = CheColor;
        if (passive.activeSprites != null)
            passive.activeSprites.GetComponent<SpriteRenderer>().color = CheColor;
        if (passive.selectedSprites != null)
            passive.selectedSprites.GetComponent<SpriteRenderer>().color = CheColor;

        var aspect = button.GetComponent<AspectPosition>();
        if (aspect != null) aspect.anchorPoint = anchor;
    }
}
