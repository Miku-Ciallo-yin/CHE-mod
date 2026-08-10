using Object = UnityEngine.Object;
using CHE.Modules;
using CHE.Roles;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;

namespace CHE.Patches;

/// <summary>
/// CHE 页签内容构建（参考 TOHE 的 GameOptionsMenuPatch）：
/// - Initialize / CreateSettings Prefix：模组页签下完全接管，return false 阻断原版填充
/// - 数据对象一律 ScriptableObject.CreateInstance，Title 填 StringNames.Accept 占位，
///   真实文字在 SetUpFromData 之后直接写 TitleText
/// - 模组设置页：全局选项平铺（布尔用 ToggleOption，数值用 NumberOption）
/// - 职业设置页：职业名按钮列表 → 点击进职业详情页（← 返回回列表）
/// </summary>
[HarmonyPatch(typeof(GameOptionsMenu))]
public static class ModOptionsMenuPatch
{
    // 布局常量（沿用 TOHE）
    private const float StartY = 2.0f;
    private const float PosX = 0.952f;
    private const float PosZ = -2.0f;
    private const float RowHeight = 0.45f;
    private const int MaskLayer = 20;

    /// <summary>行注册信息（供控件方法按 InstanceID 反查）</summary>
    public class RowInfo
    {
        public CustomOption? Opt;      // null = 展示行（职业名按钮）
        public string Title = string.Empty;
        public string ValueText = string.Empty;
        public IntGameSetting? Data;   // 数值行的底层数据对象（用于同步显示）
    }

    /// <summary>InstanceID -> 行信息。原版控件方法被拦截时据此反查（参考 TOHE 的 OptionList）</summary>
    public static readonly System.Collections.Generic.Dictionary<int, RowInfo> Rows = new();

    [HarmonyPatch(nameof(GameOptionsMenu.Initialize)), HarmonyPrefix]
    public static bool InitializePrefix(GameOptionsMenu __instance)
    {
        if (ModGameOptionsMenu.TabIndex < ModGameOptionsMenu.ModTabIndex) return true;

        if (__instance.Children == null || __instance.Children.Count == 0)
        {
            if (__instance.MapPicker != null)
                __instance.MapPicker.gameObject.SetActive(false);
            __instance.Children ??= new Il2CppSystem.Collections.Generic.List<OptionBehaviour>();
            BuildContent(__instance);
        }

        // 参考 TOHE：克隆页签必须补齐这两项，否则其 Update 每帧空引用
        try
        {
            __instance.cachedData = GameOptionsManager.Instance.CurrentGameOptions;
            __instance.InitializeControllerNavigation();
        }
        catch (System.Exception e)
        {
            CHEPlugin.Log.LogWarning($"[CHE] 页签导航初始化跳过: {e.Message}");
        }
        return false;
    }

    [HarmonyPatch(nameof(GameOptionsMenu.CreateSettings)), HarmonyPrefix]
    public static bool CreateSettingsPrefix(GameOptionsMenu __instance)
    {
        if (ModGameOptionsMenu.TabIndex < ModGameOptionsMenu.ModTabIndex) return true;

        BuildContent(__instance);
        return false;
    }

    /// <summary>页签激活时确保内容已构建（供 ModSettingsMenuPatch 调用）</summary>
    public static void EnsureContent(GameOptionsMenu menu)
    {
        if (menu.Children == null)
            menu.Children = new Il2CppSystem.Collections.Generic.List<OptionBehaviour>();
        if (menu.Children.Count == 0)
            BuildContent(menu);

        // 克隆页签补齐数据，避免其 Update 空引用（参考 TOHE）
        try
        {
            menu.cachedData = GameOptionsManager.Instance.CurrentGameOptions;
        }
        catch { /* 大厅外可能拿不到，忽略 */ }
    }

    /// <summary>构建/重建页签内容（幂等：先清空再按当前状态绘制）</summary>
    public static void BuildContent(GameOptionsMenu menu)
    {
        try
        {
            ClearContent(menu);

            var y = StartY;
            if (menu.name == "CHE_ModTab")
            {
                foreach (var opt in CustomOption.OfRole(CustomOptions.ModGroupId))
                {
                    // 有父选项且父选项未开启时收缩不显示（如猜测模式的下级开关）
                    if (opt.ParentId is { } parentId
                        && (CustomOption.Get(parentId)?.Value ?? 0) == 0)
                        continue;

                    AddOptionRow(menu, opt, y);
                    y -= RowHeight;
                }
            }
            else if (menu.name == "CHE_RolesTab")
            {
                y = BuildRolesTab(menu, y);
            }

            if (menu.scrollBar != null)
                menu.scrollBar.SetYBoundsMax(-y - 1.65f);

            // 参考 TOHE：克隆页签的 ControllerSelectable 里是指向已销毁原版选项的悬空引用，
            // 手柄导航每帧访问就会刷 NullReferenceException，必须重建
            try
            {
                if (menu.ControllerSelectable != null && menu.scrollBar != null)
                {
                    menu.ControllerSelectable.Clear();
                    foreach (var el in menu.scrollBar.GetComponentsInChildren<UiElement>())
                        menu.ControllerSelectable.Add(el);
                }
            }
            catch (System.Exception e)
            {
                CHEPlugin.Log.LogWarning($"[CHE] 重建 ControllerSelectable 跳过: {e.Message}");
            }
        }
        catch (System.Exception e)
        {
            CHEPlugin.Log.LogError($"[CHE] 选项页构建失败({menu.name}): {e}");
        }
    }

    private static void ClearContent(GameOptionsMenu menu)
    {
        if (menu.MapPicker != null)
            menu.MapPicker.gameObject.SetActive(false);

        // 注意：不要清空 Rows 注册表——它是全局的，清掉会让另一个页签的行失去拦截。
        // 旧行已销毁不可点击，重建时同 InstanceID 直接覆盖（与 TOHE 的 TryAdd 语义一致）。

        if (menu.Children != null)
        {
            for (var i = 0; i < menu.Children.Count; i++)
                if (menu.Children[i] != null)
                    Object.Destroy(menu.Children[i].gameObject);
            menu.Children.Clear();
        }

        // 兜底：容器里可能还有克隆带来的原版行（IL2CPP 下不用 Transform 遍历）
        foreach (var ob in menu.settingsContainer.GetComponentsInChildren<OptionBehaviour>(true))
            if (ob != null) Object.Destroy(ob.gameObject);
        foreach (var header in menu.settingsContainer.GetComponentsInChildren<CategoryHeaderMasked>(true))
            if (header != null) Object.Destroy(header.gameObject);
    }

    /// <summary>职业分类名称（索引即 DetailCategory 的值）</summary>
    private static readonly string[] CategoryNames = { "船员职业", "中立职业", "内鬼职业", "附加职业" };

    /// <summary>Faction → 分类索引（附加职业暂无，预留分类 3）</summary>
    private static int CategoryOf(Faction faction) => faction switch
    {
        Faction.Crewmate => 0,
        Faction.Neutral => 1,
        Faction.Impostor => 2,
        _ => 3,
    };

    /// <summary>职业设置页：分类列表 / 分类内职业列表 / 职业详情页</summary>
    private static float BuildRolesTab(GameOptionsMenu menu, float y)
    {
        // 第三级：职业详情页（返回 → 职业列表）
        if (ModGameOptionsMenu.DetailRoleId is { } detailRoleId)
        {
            var back = CreateDisplayRow(menu, "← 返回", string.Empty, y);
            MakeClickable(back, () =>
            {
                ModGameOptionsMenu.DetailRoleId = null;
                BuildContent(menu);
            });
            y -= RowHeight;

            foreach (var opt in CustomOption.OfRole(detailRoleId))
            {
                // 有父选项且父选项未开启时收缩不显示（如内阁的长/中/短任务数）
                if (opt.ParentId is { } parentId
                    && (CustomOption.Get(parentId)?.Value ?? 0) == 0)
                    continue;

                AddOptionRow(menu, opt, y);
                y -= RowHeight;
            }
            return y;
        }

        // 第二级：某分类下的职业/附加职业列表（返回 → 分类列表）
        if (ModGameOptionsMenu.DetailCategory is { } category)
        {
            var back = CreateDisplayRow(menu, "← 返回", CategoryNames[category], y);
            MakeClickable(back, () =>
            {
                ModGameOptionsMenu.DetailCategory = null;
                BuildContent(menu);
            });
            y -= RowHeight;

            // 分类 3 是附加职业，其余按阵营归类主职业
            if (category == 3)
            {
                foreach (var (addonId, addonName) in CustomRoleManager.GetRegisteredAddons())
                {
                    var row = CreateDisplayRow(menu, addonName, $"{CustomOptions.GetRoleChance(addonId)}%", y);
                    var id = addonId;
                    MakeClickable(row, () =>
                    {
                        ModGameOptionsMenu.DetailRoleId = id;
                        BuildContent(menu);
                    });
                    y -= RowHeight;
                }
                return y;
            }

            foreach (var (roleId, roleName, faction) in CustomRoleManager.GetRegisteredRoles())
            {
                if (CategoryOf(faction) != category) continue;

                var row = CreateDisplayRow(menu, roleName, $"{CustomOptions.GetRoleChance(roleId)}%", y);
                var id = roleId;
                MakeClickable(row, () =>
                {
                    ModGameOptionsMenu.DetailRoleId = id;
                    BuildContent(menu);
                });
                y -= RowHeight;
            }
            return y;
        }

        // 第一级：四个职业分类按钮（右侧显示该分类职业数）
        for (var cat = 0; cat < CategoryNames.Length; cat++)
        {
            var count = cat == 3
                ? CustomRoleManager.GetRegisteredAddons().Count()
                : CustomRoleManager.GetRegisteredRoles().Count(r => CategoryOf(r.Faction) == cat);
            var row = CreateDisplayRow(menu, CategoryNames[cat], $"{count}个", y);
            var c = cat;
            MakeClickable(row, () =>
            {
                ModGameOptionsMenu.DetailCategory = c;
                BuildContent(menu);
            });
            y -= RowHeight;
        }
        return y;
    }

    /// <summary>可调选项行：布尔用 ToggleOption，数值用 NumberOption</summary>
    private static void AddOptionRow(GameOptionsMenu menu, CustomOption opt, float y)
    {
        if (opt.IsBool)
        {
            var data = ScriptableObject.CreateInstance<CheckboxGameSetting>();
            data.Type = OptionTypes.Checkbox;
            data.Title = StringNames.Accept; // 占位，真实标题由 Initialize 拦截提供

            var tog = Object.Instantiate(menu.checkboxOrigin, menu.settingsContainer);
            tog.transform.localPosition = new Vector3(PosX, y, PosZ);
            tog.SetClickMask(menu.ButtonClickMask);
            tog.SetUpFromData(data, MaskLayer);
            tog.TitleText.text = opt.Name;
            tog.CheckMark.enabled = opt.Value == 1;
            Rows[tog.GetInstanceID()] = new RowInfo { Opt = opt, Title = opt.Name };
            menu.Children.Add(tog);
        }
        else
        {
            var data = ScriptableObject.CreateInstance<IntGameSetting>();
            data.Type = OptionTypes.Int;
            data.Title = StringNames.Accept;
            data.Value = opt.Value;
            data.Increment = opt.Step;
            data.ValidRange = new IntRange(opt.Min, opt.Max);
            data.ZeroIsInfinity = false;
            data.FormatString = string.Empty;

            var num = Object.Instantiate(menu.numberOptionOrigin, menu.settingsContainer);
            num.transform.localPosition = new Vector3(PosX, y, PosZ);
            num.SetClickMask(menu.ButtonClickMask);
            num.SetUpFromData(data, MaskLayer);
            num.TitleText.text = opt.Name;
            num.ValueText.text = opt.DisplayValue;
            Rows[num.GetInstanceID()] = new RowInfo { Opt = opt, Title = opt.Name, Data = data };
            menu.Children.Add(num);
        }
    }

    /// <summary>展示用行：隐藏加减按钮，整行可点击</summary>
    private static NumberOption CreateDisplayRow(GameOptionsMenu menu, string title, string value, float y)
    {
        var data = ScriptableObject.CreateInstance<IntGameSetting>();
        data.Type = OptionTypes.Int;
        data.Title = StringNames.Accept;
        data.ValidRange = new IntRange(0, 100);
        data.Increment = 10;
        data.Value = 0;

        var num = Object.Instantiate(menu.numberOptionOrigin, menu.settingsContainer);
        num.transform.localPosition = new Vector3(PosX, y, PosZ);
        num.SetClickMask(menu.ButtonClickMask);
        num.SetUpFromData(data, MaskLayer);
        num.TitleText.text = title;
        num.ValueText.text = value;
        num.PlusBtn.gameObject.SetActive(false);
        num.MinusBtn.gameObject.SetActive(false);
        Rows[num.GetInstanceID()] = new RowInfo { Title = title, ValueText = value };
        menu.Children.Add(num);
        return num;
    }

    /// <summary>
    /// 让行可点击：复用行内已装配好的 PlusBtn 作为点击入口
    /// （不新建 PassiveButton——AddComponent 创建的组件字段为空，Update 会空引用）。
    /// </summary>
    private static void MakeClickable(NumberOption row, System.Action onClick)
    {
        var btn = row.PlusBtn;
        btn.gameObject.SetActive(true);
        btn.OnClick.RemoveAllListeners();
        btn.OnClick.AddListener((UnityAction)(() => onClick()));
    }
}

/// <summary>
/// CHE 选项行交互拦截（参考 TOHE：按 InstanceID 反查，return false 阻断原版逻辑）。
/// 原版 NumberOption/ToggleOption 的方法会拿 Data.Title（占位 Accept）去游戏选项管理器
/// 读写值（报错 "Could not update value of Accept"）并刷新标题（全部显示"接受"），必须拦截。
/// </summary>
public static class ModOptionRowPatches
{
    private static bool IsHost => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;

    private static bool TryGetRow(OptionBehaviour behaviour, out ModOptionsMenuPatch.RowInfo info)
    {
        return ModOptionsMenuPatch.Rows.TryGetValue(behaviour.GetInstanceID(), out info!);
    }

    [HarmonyPatch(typeof(NumberOption), nameof(NumberOption.Initialize))]
    public static class NumberInitialize
    {
        public static bool Prefix(NumberOption __instance)
        {
            if (!TryGetRow(__instance, out var info)) return true;
            __instance.TitleText.text = info.Title;
            __instance.ValueText.text = info.Opt != null ? info.Opt.DisplayValue : info.ValueText;
            return false;
        }
    }

    /// <summary>阻断原版 UpdateValue（它会用 Accept 标题去更新游戏选项管理器）</summary>
    [HarmonyPatch(typeof(NumberOption), nameof(NumberOption.UpdateValue))]
    public static class NumberUpdateValue
    {
        public static bool Prefix(NumberOption __instance) => !TryGetRow(__instance, out _);
    }

    /// <summary>阻断原版 FixedUpdate 对标题/数值的刷新</summary>
    [HarmonyPatch(typeof(NumberOption), nameof(NumberOption.FixedUpdate))]
    public static class NumberFixedUpdate
    {
        public static bool Prefix(NumberOption __instance) => !TryGetRow(__instance, out _);
    }

    [HarmonyPatch(typeof(NumberOption), nameof(NumberOption.Increase))]
    public static class NumberIncrease
    {
        public static bool Prefix(NumberOption __instance) => !Change(__instance, +1);
    }

    [HarmonyPatch(typeof(NumberOption), nameof(NumberOption.Decrease))]
    public static class NumberDecrease
    {
        public static bool Prefix(NumberOption __instance) => !Change(__instance, -1);
    }

    /// <summary>自己处理加减（循环回绕），返回 true 表示已拦截</summary>
    private static bool Change(NumberOption num, int dir)
    {
        if (!TryGetRow(num, out var info)) return false;
        if (info.Opt == null || !IsHost) return true; // 展示行 / 非主机不响应

        var opt = info.Opt;
        var v = opt.Value + dir * opt.Step;
        if (v > opt.Max) v = opt.Min;
        if (v < opt.Min) v = opt.Max;
        opt.Value = v;
        if (info.Data != null) info.Data.Value = v;

        num.ValueText.text = opt.DisplayValue;
        RpcSync.BroadcastOptions();
        return true;
    }

    [HarmonyPatch(typeof(ToggleOption), nameof(ToggleOption.Initialize))]
    public static class ToggleInitialize
    {
        public static bool Prefix(ToggleOption __instance)
        {
            if (!TryGetRow(__instance, out var info)) return true;
            __instance.TitleText.text = info.Title;
            if (info.Opt != null)
                __instance.CheckMark.enabled = info.Opt.Value == 1;
            return false;
        }
    }

    [HarmonyPatch(typeof(ToggleOption), nameof(ToggleOption.Toggle))]
    public static class ToggleToggle
    {
        public static bool Prefix(ToggleOption __instance)
        {
            if (!TryGetRow(__instance, out var info)) return true;
            if (info.Opt == null || !IsHost) return false;

            info.Opt.Value = info.Opt.Value == 1 ? 0 : 1;
            __instance.CheckMark.enabled = info.Opt.Value == 1;
            RpcSync.BroadcastOptions();

            // 切换父选项后重建页签：下级选项随之显示/收缩
            var menu = __instance.GetComponentInParent<GameOptionsMenu>();
            if (menu != null)
                ModOptionsMenuPatch.BuildContent(menu);
            return false;
        }
    }

    [HarmonyPatch(typeof(ToggleOption), nameof(ToggleOption.FixedUpdate))]
    public static class ToggleFixedUpdate
    {
        public static bool Prefix(ToggleOption __instance) => !TryGetRow(__instance, out _);
    }
}
