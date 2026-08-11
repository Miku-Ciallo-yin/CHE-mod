using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TAHS.Modules;

/// <summary>
/// 醒目公告（/s 指令）：屏幕中上方显示大字消息，约 6 秒后消失。
/// 房主消息红色标题，协管消息青色标题。
/// </summary>
public static class Announcement
{
    private static GameObject? _root;
    private static float _timer;

    private const float Duration = 6f;

    /// <summary>主机广播：模组端走 RPC 显示公告，全员经官方聊天看到内容</summary>
    public static void Broadcast(bool fromHost, string content)
    {
        var label = fromHost ? "房主消息" : "协管消息";
        Show(label, content);
        RpcSync.SendAnnouncement(label, content);

        // 官方聊天广播：无模组客户端也能看到
        if (PlayerControl.LocalPlayer != null)
            PlayerControl.LocalPlayer.RpcSendChat($"【{label}】{content}");
    }

    /// <summary>本机显示公告</summary>
    public static void Show(string label, string content)
    {
        Close();

        var hud = DestroyableSingleton<HudManager>.Instance;
        if (hud == null) return;
        var template = hud.GetComponentInChildren<TextMeshPro>(true);
        if (template == null) return;

        // 过滤富文本符号，防止注入
        var safe = content.Replace('<', ' ').Replace('>', ' ');

        _root = new GameObject("TAHS_Announcement");
        _root.transform.SetParent(hud.transform, false);
        _root.transform.localPosition = new Vector3(0f, 2.2f, -100f);

        var tmp = Object.Instantiate(template, _root.transform);
        tmp.DestroyTranslator();
        var color = label == "房主消息" ? "#FF5555" : "#4FC3F7";
        tmp.text = $"<color={color}><b>【{label}】</b></color>\n<b>{safe}</b>";
        tmp.fontSize = 6f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = Color.black;
        tmp.enableWordWrapping = true;
        tmp.rectTransform.sizeDelta = new Vector2(12f, 3f);

        _timer = Duration;
    }

    /// <summary>每帧驱动（HudManager.Update 补丁调用）</summary>
    public static void Tick()
    {
        if (_root == null) return;
        _timer -= Time.deltaTime;
        if (_timer <= 0f) Close();
    }

    public static void Close()
    {
        if (_root != null) Object.Destroy(_root);
        _root = null;
    }
}
