using Object = UnityEngine.Object;
using  UnityEngine;

namespace TAHS.Modules;

/// <summary>
/// 地雷视觉（模组端）：主机通过 RPC 228 同步放置/移除，
/// 显示时间到后本地隐形。无模组客户端看不到地雷（固有降级，仍会触发）。
/// </summary>
public static class MineVisuals
{
    private class Visual
    {
        public GameObject Go = null!;
        public float HideTimer;
    }

    private static readonly Dictionary<int, Visual> _visuals = new();
    private static Sprite? _sprite;

    /// <summary>放置地雷视觉（红色圆盘，半径=判定范围）</summary>
    public static void OnPlace(int index, Vector2 pos, float range, float visibleSeconds)
    {
        Remove(index);

        var go = new GameObject($"TAHS_Mine_{index}");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetMineSprite();
        sr.color = new Color(1f, 0.25f, 0.25f, 0.75f);
        go.transform.position = new Vector3(pos.x, pos.y, 1f);
        go.transform.localScale = Vector3.one * (range * 2f);

        _visuals[index] = new Visual { Go = go, HideTimer = visibleSeconds };
    }

    public static void Remove(int index)
    {
        if (!_visuals.TryGetValue(index, out var visual)) return;
        if (visual.Go != null) Object.Destroy(visual.Go);
        _visuals.Remove(index);
    }

    /// <summary>每帧驱动（显示时间到后隐形）</summary>
    public static void Tick()
    {
        foreach (var visual in _visuals.Values)
        {
            if (visual.Go == null || !visual.Go.activeSelf) continue;
            visual.HideTimer -= Time.deltaTime;
            if (visual.HideTimer <= 0f)
                visual.Go.SetActive(false);
        }
    }

    /// <summary>全部销毁（对局重置时调用）</summary>
    public static void Clear()
    {
        foreach (var visual in _visuals.Values)
            if (visual.Go != null) Object.Destroy(visual.Go);
        _visuals.Clear();
    }

    /// <summary>程序生成的实心圆盘</summary>
    private static Sprite GetMineSprite()
    {
        if (_sprite != null) return _sprite;

        const int s = 64;
        const float center = (s - 1) / 2f;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (var x = 0; x < s; x++)
        for (var y = 0; y < s; y++)
        {
            var dx = x - center;
            var dy = y - center;
            tex.SetPixel(x, y, dx * dx + dy * dy <= center * center ? Color.white : Color.clear);
        }
        tex.Apply();

        _sprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return _sprite;
    }
}
