using Object = UnityEngine.Object;
using UnityEngine;

namespace CHE.Modules;

/// <summary>
/// 对象辅助。DestroyTranslator 参考 TOHE / TownOfHost_Y 的 ObjectHelper：
/// 克隆的 UI 对象上挂有 TextTranslatorTMP，会持续把文字覆盖回翻译表，
/// 设置自定义文本前必须先销毁它。
/// </summary>
public static class ObjectHelper
{
    public static void DestroyTranslator(this GameObject obj)
    {
        if (obj == null) return;
        foreach (var translator in obj.GetComponentsInChildren<TextTranslatorTMP>(true))
            if (translator != null)
                Object.Destroy(translator);
    }

    public static void DestroyTranslator(this MonoBehaviour obj)
    {
        if (obj != null) obj.gameObject.DestroyTranslator();
    }
}
