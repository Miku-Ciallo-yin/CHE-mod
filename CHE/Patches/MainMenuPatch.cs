using CHE.Modules;
using System.IO;
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
            SetupBackground(__instance);
            RemoveFloatersAndFrame(__instance);
            MakeLeftPanelTransparent(__instance);
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
    public static void LateUpdatePostfix(MainMenuManager __instance)
    {
        CustomPopup.Update();

        // 游戏初始化后会重新启用/恢复这些对象，每帧强制压掉（边框、漂浮小人）
        if (_particles != null && _particles.gameObject.activeSelf)
            _particles.gameObject.SetActive(false);
        if (_square != null && _square.gameObject.activeSelf)
            _square.gameObject.SetActive(false);
        foreach (var sr in _frameSprites)
            if (sr != null && sr.color.a > 0f)
                sr.color = new Color(1f, 1f, 1f, 0f);

        // 左侧按钮：游戏会重置精灵颜色，每帧把 alpha 压到目标值（幂等）
        foreach (var sr in _leftPanelSprites)
            if (sr != null && sr.color.a > LeftPanelAlpha)
            {
                var c = sr.color;
                c.a = LeftPanelAlpha;
                sr.color = c;
            }
    }

    private static Transform? _particles;
    private static Transform? _square;
    private static readonly List<SpriteRenderer> _frameSprites = new();

    /// <summary>
    /// 主菜单背景：优先加载游戏目录 CHE-DATA/background.png（放一张二次元人物图即可），
    /// 没有则用程序生成的渐变背景兜底。
    /// </summary>
    private static void SetupBackground(MainMenuManager menu)
    {
        var sprite = LoadCustomBackground() ?? GenerateGradientBackground();

        var bg = new GameObject("CHE_Background").AddComponent<SpriteRenderer>();
        bg.sprite = sprite;
        bg.transform.SetParent(menu.transform, false);
        bg.transform.localPosition = new Vector3(0f, 0f, 5f); // 置于 UI 之后

        // 缩放铺满屏幕（按相机视野高度）
        var cam = Camera.main;
        var height = cam != null ? cam.orthographicSize * 2f : 10f;
        var width = height * (cam != null ? cam.aspect : 16f / 9f);
        bg.transform.localScale = new Vector3(
            width / sprite.bounds.size.x * 1.1f,
            height / sprite.bounds.size.y * 1.1f,
            1f);

        // 隐藏原版星空背景，露出自定义背景
        var vanilla = GameObject.Find("BackgroundTexture");
        if (vanilla != null) vanilla.SetActive(false);
    }

    /// <summary>左侧菜单：收集所有精灵做每帧透明压制（按钮状态逻辑会重置颜色），底图全透明</summary>
    private static void MakeLeftPanelTransparent(MainMenuManager menu)
    {
        var leftPanel = FindDirectChild(menu.transform, "MainUI/AspectScaler", "LeftPanel");
        if (leftPanel == null) return;

        _leftPanelSprites.Clear();
        foreach (var sr in leftPanel.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr == null) continue;
            _leftPanelSprites.Add(sr);
        }

        // LeftPanel 自身的黑色底图（含边框）加入每帧全透明压制
        foreach (var sr in leftPanel.GetComponents<SpriteRenderer>())
            if (sr != null && !_frameSprites.Contains(sr)) _frameSprites.Add(sr);
    }

    private static readonly List<SpriteRenderer> _leftPanelSprites = new();
    private const float LeftPanelAlpha = 0.95f;

    /// <summary>
    /// 收集需要每帧压制的对象：漂浮小人（Ambience/PlayerParticles）、
    /// 右侧边框候选（Square / RightPanel / FullScreen 的精灵）。
    /// 游戏初始化后会恢复这些对象，真正的压制在 LateUpdate 每帧执行。
    /// </summary>
    private static void RemoveFloatersAndFrame(MainMenuManager menu)
    {
        _particles = GameObject.Find("Ambience")?.transform.Find("PlayerParticles");
        _square = menu.transform.Find("Square");

        _frameSprites.Clear();
        var scaler = menu.transform.Find("MainUI/AspectScaler");
        if (scaler == null) return;

        foreach (var name in new[] { "RightPanel", "FullScreen" })
        {
            for (var i = 0; i < scaler.childCount; i++)
            {
                var child = scaler.GetChild(i);
                if (child.name != name) continue;
                foreach (var sr in child.GetComponents<SpriteRenderer>())
                    if (sr != null) _frameSprites.Add(sr);
            }
        }
        // RightPanel/MaskedBlackScreen：带遮罩的黑色圆角板，镂空四周的黑环就是边框
        var rightPanel = FindDirectChild(menu.transform, "MainUI/AspectScaler", "RightPanel");
        var masked = rightPanel?.Find("MaskedBlackScreen");
        if (masked != null)
            foreach (var sr in masked.GetComponents<SpriteRenderer>())
                if (sr != null) _frameSprites.Add(sr);
        // MainUI 直接子级 FullScreen（14x14）也一并处理
        var mainUI = menu.transform.Find("MainUI");
        if (mainUI != null)
            for (var i = 0; i < mainUI.childCount; i++)
            {
                var child = mainUI.GetChild(i);
                if (child.name is "FullScreen" or "Tint")
                    foreach (var sr in child.GetComponents<SpriteRenderer>())
                        if (sr != null) _frameSprites.Add(sr);
            }
    }

    /// <summary>先按斜杠路径找到父级，再在直接子级里按名字查找（避免多级 Find 失效）</summary>
    private static Transform? FindDirectChild(Transform root, string parentPath, string childName)
    {
        var parent = root.Find(parentPath);
        if (parent == null) return null;
        for (var i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == childName)
                return parent.GetChild(i);
        return null;
    }

    /// <summary>从 CHE-DATA 加载自定义背景（优先 background.png，否则取目录里第一张 PNG）</summary>
    private static Sprite? LoadCustomBackground()
    {
        try
        {
            var dir = Path.Combine(Environment.CurrentDirectory, "CHE-DATA");
            if (!Directory.Exists(dir)) return null;

            var path = Path.Combine(dir, "background.png");
            if (!File.Exists(path))
                path = Directory.GetFiles(dir, "*.png").FirstOrDefault();
            if (path == null) return null;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(path))) return null;

            CHEPlugin.Log.LogInfo($"[CHE] 已加载自定义主菜单背景 {Path.GetFileName(path)}");
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f);
        }
        catch (System.Exception e)
        {
            CHEPlugin.Log.LogWarning($"[CHE] 背景图加载失败: {e.Message}");
            return null;
        }
    }

    /// <summary>程序生成的深蓝渐变兜底背景（点缀星星）</summary>
    private static Sprite GenerateGradientBackground()
    {
        const int w = 256, h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var top = new Color(0.03f, 0.08f, 0.16f);
        var bottom = new Color(0.05f, 0.25f, 0.35f);
        var rng = new System.Random(42);

        for (var y = 0; y < h; y++)
        {
            var t = (float)y / (h - 1);
            var row = Color.Lerp(bottom, top, t);
            for (var x = 0; x < w; x++)
                tex.SetPixel(x, y, row);
        }
        // 星星
        for (var i = 0; i < 120; i++)
        {
            var sx = rng.Next(w);
            var sy = rng.Next(h);
            var brightness = 0.5f + (float)rng.NextDouble() * 0.5f;
            tex.SetPixel(sx, sy, new Color(brightness, brightness, brightness, 0.9f));
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>Logo 区下方的版本徽章</summary>
    private static void CreateVersionBadge(MainMenuManager menu, TextMeshPro tmpTemplate)
    {
        var badge = Object.Instantiate(tmpTemplate, menu.playButton.transform.parent);
        badge.DestroyTranslator();
        badge.text = $"<color=#4FC3F7>CHE v{CHEPlugin.Version} by 米裤恰油</color>";
        badge.fontSize = 3.2f;
        badge.fontStyle = FontStyles.Normal;

        var aspect = badge.GetComponent<AspectPosition>();
        if (aspect == null) aspect = badge.gameObject.AddComponent<AspectPosition>();
        aspect.anchorPoint = new Vector2(0.2f, 0.72f);
        aspect.updateAlways = true;
    }

    /// <summary>左侧竖排自定义按钮（以"退出"按钮为参照向右偏移竖排，对齐 TONE 布局）</summary>
    private static void CreateButtons(MainMenuManager menu)
    {
        var template = menu.creditsButton.gameObject;
        var basePos = menu.quitButton.transform.localPosition;

        CreateButton(menu, template, "关于 CHE", basePos + new Vector3(2.4f, 0.8f, 0f),
            () => CustomPopup.Show(menu.transform, "关于 CHE", AboutText));
        CreateButton(menu, template, "GitHub", basePos + new Vector3(2.4f, 1.55f, 0f),
            () => Application.OpenURL(ModConfig.GithubUrl.Value));
        CreateButton(menu, template, "交流群", basePos + new Vector3(2.4f, 2.3f, 0f),
            () => Application.OpenURL(ModConfig.CommunityUrl.Value));
    }

    private static void CreateButton(MainMenuManager menu, GameObject template, string text, Vector3 localPos, System.Action action)
    {
        var button = Object.Instantiate(template, template.transform.parent);
        button.name = "CHE_" + text;
        button.gameObject.SetActive(true);

        // 克隆按钮自带的 AspectPosition 会按锚点覆盖位置，销毁后用局部坐标定位
        var aspect = button.GetComponent<AspectPosition>();
        if (aspect != null) Object.Destroy(aspect);
        button.transform.localPosition = localPos;

        var label = button.transform.FindChild("FontPlacer").GetChild(0).gameObject;
        label.DestroyTranslator();
        var tmp = label.GetComponent<TextMeshPro>();
        tmp.text = text;
        tmp.color = CheColor;
        tmp.fontStyle = FontStyles.Normal;

        var passive = button.GetComponent<PassiveButton>();
        CustomPopup.ClearButtonEvents(passive);
        passive.OnClick.AddListener((UnityAction)(System.Action)(() => action()));
        if (passive.inactiveSprites != null)
            passive.inactiveSprites.GetComponent<SpriteRenderer>().color = CheColor;
        if (passive.activeSprites != null)
            passive.activeSprites.GetComponent<SpriteRenderer>().color = CheColor;
        if (passive.selectedSprites != null)
            passive.selectedSprites.GetComponent<SpriteRenderer>().color = CheColor;
    }
}
