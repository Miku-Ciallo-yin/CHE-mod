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
}
