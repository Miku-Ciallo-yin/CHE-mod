using Object = UnityEngine.Object;
using CHE.Modules;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace CHE.Patches;

/// <summary>
/// 大厅"编辑"设置菜单的 CHE 页签（参考 TownOfHost_Y / EHR 的 GameSettingMenuPatch）：
/// - OnEnable：克隆模板按钮和模板页签（隐藏保留，复用）
/// - Start：隐藏原版职业设置页签，重排原版按钮，创建"模组设置 / 职业设置"两个 CHE 页签按钮和空页签
/// - ChangeTab：tabNum &gt;= 3 时拦截，隐藏原版页签、显示 CHE 页签（原版 tabNum==2 重定向到 CHE 职业设置）
/// - Close：销毁克隆体，下次打开重新创建
/// </summary>
[HarmonyPatch(typeof(GameSettingMenu))]
public static class ModSettingsMenuPatch
{
    private static readonly System.Collections.Generic.Dictionary<int, PassiveButton> _buttons = new();
    private static readonly System.Collections.Generic.Dictionary<int, GameOptionsMenu> _tabs = new();

    [HarmonyPatch(nameof(GameSettingMenu.OnEnable)), HarmonyPostfix]
    public static void OnEnablePostfix(GameSettingMenu __instance)
    {
        try
        {
            EnsureTemplates(__instance);
        }
        catch (System.Exception e)
        {
            CHEPlugin.Log.LogError($"[CHE] 设置模板创建失败: {e}");
        }
    }

    private static void EnsureTemplates(GameSettingMenu menu)
    {
        if (ModGameOptionsMenu.TemplateMenu == null)
        {
            ModGameOptionsMenu.TemplateMenu = Object.Instantiate(
                menu.GameSettingsTab, menu.GameSettingsTab.transform.parent);
            ModGameOptionsMenu.TemplateMenu.gameObject.SetActive(false);
        }
        if (ModGameOptionsMenu.TemplateButton == null)
        {
            ModGameOptionsMenu.TemplateButton = Object.Instantiate(
                menu.GameSettingsButton, menu.GameSettingsButton.transform.parent);
            ModGameOptionsMenu.TemplateButton.gameObject.SetActive(false);
        }
    }

    [HarmonyPatch(nameof(GameSettingMenu.Start)), HarmonyPostfix]
    public static void StartPostfix(GameSettingMenu __instance)
    {
        try
        {
            _buttons.Clear();
            _tabs.Clear();
            EnsureTemplates(__instance);

            // 原版职业设置页签由 CHE 职业设置取代，隐藏按钮和页签
            __instance.RoleSettingsButton.gameObject.SetActive(false);
            __instance.RoleSettingsTab?.gameObject.SetActive(false);

            // 原版按钮重排：预设左列、游戏设置右列（第一排）
            LayoutButton(__instance.GamePresetsButton, ModGameOptionsMenu.ButtonPosLeft);
            LayoutButton(__instance.GameSettingsButton, ModGameOptionsMenu.ButtonPosRight);

            // CHE 页签按钮（第二排）：模组设置左、职业设置右
            var row2 = new Vector3(0f, ModGameOptionsMenu.ButtonRowStep, 0f);
            _buttons[ModGameOptionsMenu.ModTabIndex] = CreateTabButton(
                __instance, "CHE_ModButton", "模组设置",
                ModGameOptionsMenu.ButtonPosLeft - row2, ModGameOptionsMenu.ModTabIndex);
            _buttons[ModGameOptionsMenu.RolesTabIndex] = CreateTabButton(
                __instance, "CHE_RolesButton", "职业设置",
                ModGameOptionsMenu.ButtonPosRight - row2, ModGameOptionsMenu.RolesTabIndex);

            // CHE 空页签（内容在 ModOptionsMenuPatch 中构建）
            _tabs[ModGameOptionsMenu.ModTabIndex] = CreateTab(__instance, "CHE_ModTab");
            _tabs[ModGameOptionsMenu.RolesTabIndex] = CreateTab(__instance, "CHE_RolesTab");

            CHEPlugin.Log.LogInfo("[CHE] 模组设置 / 职业设置页签已创建");
        }
        catch (System.Exception e)
        {
            CHEPlugin.Log.LogError($"[CHE] 设置页签创建失败: {e}");
        }
    }

    private static void LayoutButton(PassiveButton button, Vector3 pos)
    {
        button.transform.localPosition = pos;
        button.transform.localScale = ModGameOptionsMenu.ButtonSize;
    }

    private static PassiveButton CreateTabButton(GameSettingMenu menu, string name, string text, Vector3 pos, int tabNum)
    {
        var button = Object.Instantiate(
            ModGameOptionsMenu.TemplateButton, menu.GameSettingsButton.transform.parent);
        button.gameObject.SetActive(true);
        button.name = name;

        // 参考 TOHE：先销毁翻译组件再设置文字
        var label = button.GetComponentInChildren<TextMeshPro>();
        label.DestroyTranslator();
        label.text = text;

        var color = ModGameOptionsMenu.CheColor;
        button.inactiveSprites.GetComponent<SpriteRenderer>().color = color;
        button.activeSprites.GetComponent<SpriteRenderer>().color = color;
        button.selectedSprites.GetComponent<SpriteRenderer>().color = color;

        button.transform.localPosition = pos;
        button.transform.localScale = ModGameOptionsMenu.ButtonSize;

        var comp = button.GetComponent<PassiveButton>();
        comp.OnClick.RemoveAllListeners();
        comp.OnClick.AddListener((UnityAction)(System.Action)(() => menu.ChangeTab(tabNum, false)));

        return button;
    }

    private static GameOptionsMenu CreateTab(GameSettingMenu menu, string name)
    {
        var tab = Object.Instantiate(
            ModGameOptionsMenu.TemplateMenu, menu.GameSettingsTab.transform.parent);
        tab.name = name;
        tab.gameObject.SetActive(false);
        return tab;
    }

    [HarmonyPatch(nameof(GameSettingMenu.ChangeTab)), HarmonyPrefix]
    public static bool ChangeTabPrefix(GameSettingMenu __instance, ref int tabNum)
    {
        try
        {
            // 原版职业设置页签（2）重定向到 CHE 职业设置（4）
            if (tabNum == 2) tabNum = ModGameOptionsMenu.RolesTabIndex;
            ModGameOptionsMenu.TabIndex = tabNum;

            // 先收起所有 CHE 页签和按钮选中态
            foreach (var tab in _tabs.Values)
                if (tab != null) tab.gameObject.SetActive(false);
            foreach (var button in _buttons.Values)
                if (button != null) button.SelectButton(false);

            if (tabNum < ModGameOptionsMenu.ModTabIndex)
            {
                // 原版页签放行，但保持原版职业设置页签隐藏
                __instance.RoleSettingsTab?.gameObject.SetActive(false);
                return true;
            }

            // CHE 页签：隐藏原版内容，显示自己的页签
            __instance.PresetsTab?.gameObject.SetActive(false);
            __instance.GameSettingsTab?.gameObject.SetActive(false);
            __instance.RoleSettingsTab?.gameObject.SetActive(false);
            __instance.GamePresetsButton?.SelectButton(false);
            __instance.GameSettingsButton?.SelectButton(false);
            __instance.RoleSettingsButton?.SelectButton(false);

            if (_tabs.TryGetValue(tabNum, out var settingsTab) && settingsTab != null)
            {
                ModGameOptionsMenu.DetailRoleId = null; // 每次切页签回到列表页
                settingsTab.gameObject.SetActive(true);
                ModOptionsMenuPatch.EnsureContent(settingsTab);

                if (__instance.MenuDescriptionText != null)
                {
                    __instance.MenuDescriptionText.DestroyTranslator();
                    __instance.MenuDescriptionText.text = tabNum == ModGameOptionsMenu.ModTabIndex
                        ? "CHE 模组的全局功能设置。"
                        : "CHE 模组职业设置：点击职业名调整该职业的配置项。";
                }
            }

            if (_buttons.TryGetValue(tabNum, out var tabButton) && tabButton != null)
                tabButton.SelectButton(true);

            return false; // 拦截原版逻辑
        }
        catch (System.Exception e)
        {
            CHEPlugin.Log.LogError($"[CHE] 页签切换失败: {e}");
            return true;
        }
    }

    [HarmonyPatch(nameof(GameSettingMenu.Close)), HarmonyPostfix]
    public static void ClosePostfix()
    {
        foreach (var button in _buttons.Values)
            if (button != null) Object.Destroy(button.gameObject);
        foreach (var tab in _tabs.Values)
            if (tab != null) Object.Destroy(tab.gameObject);
        _buttons.Clear();
        _tabs.Clear();
        ModGameOptionsMenu.DetailRoleId = null;
    }
}
