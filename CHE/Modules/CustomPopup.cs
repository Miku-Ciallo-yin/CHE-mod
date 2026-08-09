using TMPro;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace CHE.Modules;

/// <summary>
/// 模态弹窗（参考 TONEX.CustomPopup 简化版）：标题 + 正文 + 确认按钮。
/// 背景为程序生成纯色贴图，控件克隆自主菜单原版按钮/TMP。
/// 由 MainMenuPatch.Setup 注入模板，LateUpdate 驱动 Update。
/// </summary>
public static class CustomPopup
{
    private static GameObject? _root;
    private static PassiveButton? _buttonTemplate;
    private static TextMeshPro? _tmpTemplate;

    public static bool IsOpen => _root != null;

    /// <summary>注入克隆模板（主菜单 Start 时调用一次）</summary>
    public static void Setup(PassiveButton buttonTemplate, TextMeshPro tmpTemplate)
    {
        _buttonTemplate = buttonTemplate;
        _tmpTemplate = tmpTemplate;
    }

    public static void Show(Transform parent, string title, string body)
    {
        Close();
        if (_buttonTemplate == null || _tmpTemplate == null) return;

        _root = new GameObject("CHE_Popup");
        _root.transform.SetParent(parent, false);
        _root.transform.localPosition = new Vector3(0f, 0f, -60f);

        var bg = new GameObject("CHE_PopupBG").AddComponent<SpriteRenderer>();
        bg.sprite = SpriteHelper.Solid();
        bg.color = new Color(0.05f, 0.08f, 0.1f, 0.95f);
        bg.transform.SetParent(_root.transform, false);
        bg.transform.localScale = new Vector3(11f, 6.5f, 1f);

        var titleTmp = Object.Instantiate(_tmpTemplate, _root.transform);
        titleTmp.DestroyTranslator();
        titleTmp.text = $"<color=#4FC3F7>{title}</color>";
        titleTmp.fontSize = 5f;
        titleTmp.transform.localPosition = new Vector3(0f, 2.3f, -1f);

        var bodyTmp = Object.Instantiate(_tmpTemplate, _root.transform);
        bodyTmp.DestroyTranslator();
        bodyTmp.text = body;
        bodyTmp.fontSize = 2.8f;
        bodyTmp.alignment = TextAlignmentOptions.TopLeft;
        bodyTmp.rectTransform.sizeDelta = new Vector2(9.5f, 3.5f);
        bodyTmp.transform.localPosition = new Vector3(0f, 1.3f, -1f);

        var ok = Object.Instantiate(_buttonTemplate, _root.transform);
        ok.gameObject.SetActive(true);
        var label = ok.transform.FindChild("FontPlacer").GetChild(0).gameObject;
        label.DestroyTranslator();
        label.GetComponent<TextMeshPro>().text = "确 定";
        ok.OnClick.RemoveAllListeners();
        ok.OnClick.AddListener((UnityAction)(() => Close()));
        ok.transform.localPosition = new Vector3(0f, -2.3f, -1f);
    }

    public static void Close()
    {
        if (_root != null) Object.Destroy(_root);
        _root = null;
    }

    public static void Update()
    {
        if (_root == null) return;
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }
}
