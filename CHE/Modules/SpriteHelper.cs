using UnityEngine;

namespace CHE.Modules;

/// <summary>共享程序生成贴图</summary>
public static class SpriteHelper
{
    private static Sprite? _solid;

    /// <summary>1x1 纯色图（颜色由 SpriteRenderer.color 控制）</summary>
    public static Sprite Solid()
    {
        if (_solid != null) return _solid;

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();

        _solid = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        return _solid;
    }
}
