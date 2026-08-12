using TAHS.Modules;
using HarmonyLib;
using UnityEngine;

namespace TAHS.Patches;

/// <summary>
/// 大厅自由行动（参考 TONE，仅模组端本地表现）：
/// - 按住 Ctrl：等待大厅中关闭本地玩家碰撞体，可穿墙走到飞船外面，松开恢复
/// - 滚轮：等待大厅 / 对局死亡后缩放视角（orthographicSize 夹在 1.5~12）
/// 均无模组客户端不参与（也无碍：移动/位置同步走原版 NetTransform）。
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class LobbyMovePatch
{
    private const float MinZoom = 1.5f;
    private const float MaxZoom = 12f;
    private const float ZoomStep = 0.6f;

    private static bool _noClip;
    private static float _defaultZoom = -1f;
    private static float _zoom = -1f;

    /// <summary>等待大厅中</summary>
    public static bool InLobby => LobbyBehaviour.Instance != null;

    /// <summary>允许缩放视角的场景：等待大厅，或对局中已死亡（幽灵）</summary>
    public static bool ZoomAllowed
    {
        get
        {
            if (InLobby) return true;
            var local = PlayerControl.LocalPlayer;
            return ShipStatus.Instance != null
                   && local != null && local.Data != null && local.Data.IsDead;
        }
    }

    public static void Postfix()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null) return;

        TickNoClip(local);
        TickZoom();
    }

    /// <summary>Ctrl 穿墙：按住时禁用本地碰撞体，松开/离开大厅恢复</summary>
    private static void TickNoClip(PlayerControl local)
    {
        var want = InLobby
                   && CustomOptions.CtrlNoClip.Value == 1
                   && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));

        if (want == _noClip) return;
        _noClip = want;
        SetColliders(local, !want);
    }

    private static void SetColliders(PlayerControl player, bool enabled)
    {
        var colliders = player.GetComponents<Collider2D>();
        for (var i = 0; i < colliders.Count; i++)
        {
            var col = colliders[i];
            if (col != null && !col.isTrigger) col.enabled = enabled;
        }
    }

    /// <summary>滚轮缩放：允许时按滚轮调整并应用，不允许时恢复默认</summary>
    private static void TickZoom()
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        if (_defaultZoom < 0f)
        {
            _defaultZoom = cam.orthographicSize;
            _zoom = _defaultZoom;
        }

        if (!ZoomAllowed)
        {
            if (!Mathf.Approximately(cam.orthographicSize, _defaultZoom))
            {
                cam.orthographicSize = _defaultZoom;
                _zoom = _defaultZoom;
            }
            return;
        }

        var scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
            _zoom = Mathf.Clamp(_zoom - scroll * ZoomStep, MinZoom, MaxZoom);

        if (!Mathf.Approximately(cam.orthographicSize, _zoom))
            cam.orthographicSize = _zoom;
    }

    /// <summary>对局/大厅切换时重置穿墙状态（避免碰撞体卡在禁用态）</summary>
    public static void ResetState()
    {
        if (_noClip && PlayerControl.LocalPlayer != null)
            SetColliders(PlayerControl.LocalPlayer, true);
        _noClip = false;
        _defaultZoom = -1f;
        _zoom = -1f;
    }

    /// <summary>主机记录的各玩家 /tpout 前位置（无模组端 /tpin 返回用）</summary>
    private static readonly Dictionary<byte, Vector2> _hostTpReturn = new();

    /// <summary>
    /// 主机：处理聊天中收到的 /tpout、/tpin（参考 TONE）。
    /// 模组端的指令在本地处理且不会广播，能走到这里的只有无模组端玩家的消息；
    /// 传送由主机用官方 RpcSnapTo 驱动，无模组客户端同样生效；
    /// 反馈经 ChatHelper.ShowPrivate 定向发送，仅指令发起者可见。
    /// </summary>
    public static void HandleHostCommand(PlayerControl source, string text)
    {
        if (text.Equals("/tpout", System.StringComparison.OrdinalIgnoreCase))
            HostTp(source, true);
        else if (text.Equals("/tpin", System.StringComparison.OrdinalIgnoreCase))
            HostTp(source, false);
    }

    private static void HostTp(PlayerControl player, bool tpOut)
    {
        if (player == null || player.Data == null) return;
        System.Action<string> show = msg => ChatHelper.ShowPrivate(player, msg);
        var cmd = tpOut ? "/tpout" : "/tpin";

        if (CustomOptions.TpCommands.Value != 1)
        {
            show($"[TAHS] {cmd} 已被房主关闭");
            return;
        }
        if (!InLobby && !player.Data.IsDead)
        {
            show($"[TAHS] {cmd} 仅在等待大厅或对局死亡后可用");
            return;
        }

        if (tpOut)
        {
            _hostTpReturn[player.PlayerId] = player.transform.position;
            player.NetTransform.RpcSnapTo((Vector2)player.transform.position + Vector2.down * 8f);
            show("[TAHS] 已传送到飞船外面（/tpin 返回）");
        }
        else
        {
            var pos = _hostTpReturn.TryGetValue(player.PlayerId, out var saved)
                ? saved
                : new Vector2(0f, 0.5f);
            _hostTpReturn.Remove(player.PlayerId);
            player.NetTransform.RpcSnapTo(pos);
            show("[TAHS] 已返回飞船内");
        }
    }
}

/// <summary>
/// 聊天指令隐藏 + 主机代收（参考 TONE）：
/// - 模组端收到任何以 / 开头的聊天消息一律不显示（防指令泄露）；
/// - 主机在此基础上处理无模组端玩家发来的指令（/tpout、/tpin）。
/// 无模组客户端之间仍能看到彼此发出的指令原文（原版聊天广播，Host Only 无法拦截）。
/// </summary>
[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
public static class HostChatCommandPatch
{
    public static bool Prefix(PlayerControl sourcePlayer, string chatText)
    {
        var text = chatText?.Trim();
        if (string.IsNullOrEmpty(text) || !text.StartsWith('/')) return true;

        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost
            && sourcePlayer != null && !sourcePlayer.AmOwner)
            LobbyMovePatch.HandleHostCommand(sourcePlayer, text);

        return false; // 指令一律不在聊天栏显示
    }
}
