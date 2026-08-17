using Object = UnityEngine.Object;
using UnityEngine;

namespace TAHS.Modules;

/// <summary>
/// 局内语音系统（仅模组端，模组设置开启）：
/// - 按住 V 说话：8kHz 单声道 μ-law 压缩，50ms 一帧经 RPC（CallId 230）广播
/// - 会议中全音量；对局中按距离线性衰减（超过"声音传播距离"即静音），隔墙不可闻
/// - 死亡后可全音量收听；仅当局内玩家全部为模组端时才可开启（ToggleToggle 校验）
/// 注意：不做回声消除，建议佩戴耳机。
/// </summary>
public static class VoiceManager
{
    /// <summary>采样率（8k 单声道，语音足够且省带宽）</summary>
    private const int SampleRate = 8000;

    /// <summary>每帧采样数（50ms）</summary>
    private const int FrameSamples = 400;

    /// <summary>距离衰减的基准范围（游戏单位，实际范围 = 基准 × 距离倍率）</summary>
    private const float BaseRange = 14f;

    private static bool Enabled => CustomOptions.VoiceEnabled.Value == 1;

    // 采集端
    private static AudioClip? _mic;
    private static int _micPos;
    private static bool _talking;
    private static float _sendTimer;
    private static readonly float[] _frameBuf = new float[FrameSamples];
    private static readonly byte[] _packet = new byte[FrameSamples];

    // 播放端
    private class Channel
    {
        public AudioSource? Source;
        public readonly float[] Ring = new float[SampleRate];
        public int WritePos;
        public float OcclusionTimer;
        public bool Occluded;
        public float IdleTimer = 999f;
        public GameObject? MicIcon;
    }

    private static readonly System.Collections.Generic.Dictionary<byte, Channel> _channels = new();
    private static GameObject? _root;
    private static Sprite? _micSprite;

    /// <summary>新进玩家待检测（clientId -> 检测期限；3 秒内未完成模组握手视为非模组端）</summary>
    private static readonly System.Collections.Generic.Dictionary<int, float> _pendingJoins = new();

    /// <summary>每帧驱动（AnnouncementPatch 调用）</summary>
    public static void Tick()
    {
        var local = PlayerControl.LocalPlayer;
        var inGame = AmongUsClient.Instance != null
                     && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started;

        if (!Enabled || !inGame || local == null)
        {
            if (_talking) StopTalking();
            MuteAll();
            return;
        }

        var inMeeting = MeetingHud.Instance != null;

        // 按键说话
        var wantTalk = Input.GetKey(KeyCode.V);
        if (wantTalk && !_talking) StartTalking();
        else if (!wantTalk && _talking) StopTalking();

        // 采集并发送
        if (_talking)
        {
            _sendTimer -= Time.deltaTime;
            if (_sendTimer <= 0f)
            {
                _sendTimer = 0.05f;
                CaptureAndSend();
            }

            // 本机开麦：自己的麦克风标志高亮
            var selfCh = GetChannel(local.PlayerId);
            selfCh.IdleTimer = 0f;
        }

        // 新进玩家模组检测（仅主机）
        ProcessPendingJoins();

        // 播放端：按距离/隔墙更新音量 + 开麦标志
        foreach (var (senderId, ch) in _channels)
        {
            ch.IdleTimer += Time.deltaTime;
            UpdateChannelVolume(senderId, ch, inMeeting);
            UpdateMicIcon(senderId, ch, inMeeting);
        }
    }

    /// <summary>有玩家进房（PlayerIdPatch 调用，仅主机）：记录待检测</summary>
    public static void OnPlayerJoined(int clientId)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
        if (!Enabled) return;
        _pendingJoins[clientId] = Time.time + 3f; // 3 秒握手窗口
    }

    /// <summary>新进玩家检测：窗口期后仍非模组端则关闭语音系统（仅主机）</summary>
    private static void ProcessPendingJoins()
    {
        if (_pendingJoins.Count == 0) return;
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;

        foreach (var (clientId, deadline) in _pendingJoins.ToArray())
        {
            if (Time.time < deadline) continue;

            var player = PlayerControl.AllPlayerControls.ToArray()
                .FirstOrDefault(p => p != null && p.OwnerId == clientId);
            if (player == null || player.Data == null
                || !PlayerIdManager.IsModdedClient(player))
            {
                // 非模组端加入：关闭语音系统并广播
                CustomOptions.VoiceEnabled.Value = 0;
                RpcSync.BroadcastOptions();
                if (_talking) StopTalking();
                MuteAll();
                Announcement.Broadcast(true, "有非模组端玩家加入，语音系统已自动关闭");
                TAHSPlugin.Log.LogInfo($"[TAHS] 玩家 {clientId} 非模组端，语音系统已自动关闭");
            }

            _pendingJoins.Remove(clientId);
        }
    }

    private static void StartTalking()
    {
        if (Microphone.devices.Length == 0) return;
        _mic = Microphone.Start(null, true, 1, SampleRate);
        _micPos = 0;
        _talking = true;
        _sendTimer = 0f;
    }

    private static void StopTalking()
    {
        if (Microphone.IsRecording(null)) Microphone.End(null);
        _talking = false;
        _mic = null;
    }

    /// <summary>采集一帧并广播</summary>
    private static void CaptureAndSend()
    {
        if (_mic == null) return;

        var pos = Microphone.GetPosition(null);
        var available = pos - _micPos;
        if (available < 0) available += SampleRate;
        if (available < FrameSamples) return;

        // 读取（录音环尾回绕时本帧只读到尾部，余下等下一帧）
        var count = Mathf.Min(FrameSamples, SampleRate - _micPos);
        _mic.GetData(_frameBuf, _micPos);
        _micPos = (_micPos + count) % SampleRate;

        for (var i = 0; i < count; i++)
            _packet[i] = MuLawEncode(_frameBuf[i]);
        RpcSync.BroadcastVoice(_packet, count);
    }

    /// <summary>收到远端语音帧（RpcSync 调用）</summary>
    public static void OnVoiceReceived(byte senderId, byte[] data)
    {
        if (!Enabled) return;

        var ch = GetChannel(senderId);
        for (var i = 0; i < data.Length; i++)
        {
            ch.Ring[ch.WritePos] = MuLawDecode(data[i]);
            ch.WritePos = (ch.WritePos + 1) % SampleRate;
        }
        ch.Source!.clip.SetData(ch.Ring, 0);
        if (!ch.Source.isPlaying) ch.Source.Play();
        ch.IdleTimer = 0f;
    }

    private static Channel GetChannel(byte senderId)
    {
        if (_channels.TryGetValue(senderId, out var ch)) return ch;

        if (_root == null)
            _root = new GameObject("TAHS_Voice");

        var go = new GameObject($"Voice_{senderId}");
        go.transform.SetParent(_root.transform, false);

        ch = new Channel { Source = go.AddComponent<AudioSource>() };
        ch.Source.loop = true;
        ch.Source.spatialBlend = 0f; // 2D，距离衰减手动计算
        ch.Source.clip = AudioClip.Create("voice", SampleRate, 1, SampleRate, true);
        ch.Source.Play();
        _channels[senderId] = ch;
        return ch;
    }

    /// <summary>按距离/隔墙/会议状态更新某通道音量</summary>
    private static void UpdateChannelVolume(byte senderId, Channel ch, bool inMeeting)
    {
        var src = ch.Source;
        if (src == null) return;

        var local = PlayerControl.LocalPlayer;
        var sender = PlayerControl.AllPlayerControls.ToArray()
            .FirstOrDefault(p => p != null && p.PlayerId == senderId);

        // 会议中 / 本机死亡（幽灵收听）：全音量
        if (inMeeting || local == null || local.Data == null || local.Data.IsDead)
        {
            src.volume = 1f;
            return;
        }
        if (sender == null || sender.Data == null)
        {
            src.volume = 0f;
            return;
        }

        var localPos = local.GetTruePosition();
        var dist = Vector2.Distance(localPos, sender.GetTruePosition());
        var range = BaseRange * CustomOptions.VoiceRange.ScaledValue;
        if (dist >= range)
        {
            src.volume = 0f;
            return;
        }

        // 隔墙（0.25 秒缓存一次射线检测）
        ch.OcclusionTimer -= Time.deltaTime;
        if (ch.OcclusionTimer <= 0f)
        {
            ch.OcclusionTimer = 0.25f;
            var dir = sender.GetTruePosition() - localPos;
            var hit = Physics2D.Raycast(localPos, dir.normalized, dist, LayerMask.GetMask("Ship"));
            ch.Occluded = hit.collider != null;
        }

        src.volume = ch.Occluded ? 0f : 1f - dist / range;
    }

    private static void MuteAll()
    {
        foreach (var (_, ch) in _channels)
            if (ch.Source != null) ch.Source.volume = 0f;
    }

    /// <summary>开麦时头顶显示高亮麦克风标志（模组端本地渲染；会议中隐藏）</summary>
    private static void UpdateMicIcon(byte senderId, Channel ch, bool inMeeting)
    {
        var active = ch.IdleTimer < 0.5f;

        if (active && ch.MicIcon == null)
        {
            ch.MicIcon = new GameObject($"VoiceMic_{senderId}");
            var sr = ch.MicIcon.AddComponent<SpriteRenderer>();
            sr.sprite = GetMicSprite();
            ch.MicIcon.transform.localScale = Vector3.one * 0.55f;
        }

        if (ch.MicIcon == null) return;

        var sender = PlayerControl.AllPlayerControls.ToArray()
            .FirstOrDefault(p => p != null && p.PlayerId == senderId);
        var show = active && sender != null && !sender.Data!.IsDead && !inMeeting
                   && ExileController.Instance == null;
        if (ch.MicIcon.activeSelf != show)
            ch.MicIcon.SetActive(show);
        if (show)
        {
            var pos = sender!.GetTruePosition();
            ch.MicIcon.transform.position = new Vector3(pos.x + 0.35f, pos.y + 0.75f, -5f);
            // 高亮呼吸脉冲
            var pulse = 1f + 0.12f * Mathf.Sin(Time.time * 8f);
            ch.MicIcon.transform.localScale = Vector3.one * (0.55f * pulse);
        }
    }

    /// <summary>程序生成的麦克风图标（圆形拾音头 + 杆身 + 底座，亮黄色）</summary>
    private static Sprite GetMicSprite()
    {
        if (_micSprite != null) return _micSprite;

        const int s = 32;
        var color = new Color(1f, 0.92f, 0.2f);
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (var y = 0; y < s; y++)
        for (var x = 0; x < s; x++)
        {
            var set = false;
            // 拾音头（圆）
            var dx = x - 16f;
            var dy = y - 11f;
            if (dx * dx + dy * dy <= 6f * 6f) set = true;
            // 杆身
            if (x >= 14 && x <= 18 && y >= 16 && y <= 25) set = true;
            // 底座
            if (x >= 9 && x <= 23 && y >= 26 && y <= 28) set = true;

            tex.SetPixel(x, s - 1 - y, set ? color : Color.clear);
        }
        tex.Apply();

        _micSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return _micSprite;
    }

    /// <summary>场景切换时清理（RoleAssignPatch 重置时调用）</summary>
    public static void Clear()
    {
        if (_talking) StopTalking();
        foreach (var (_, ch) in _channels)
            if (ch.Source != null) Object.Destroy(ch.Source.gameObject);
        _channels.Clear();
    }

    // μ-law 编解码（ITU G.711，单字节采样，省带宽且保语音可懂度）
    private static byte MuLawEncode(float s)
    {
        const float mu = 255f;
        s = Mathf.Clamp(s, -1f, 1f);
        var sign = s < 0f ? 0x80 : 0;
        var mag = Mathf.Log(1f + mu * Mathf.Abs(s)) / Mathf.Log(1f + mu);
        return (byte)(sign | (int)(mag * 127f));
    }

    private static float MuLawDecode(byte b)
    {
        var sign = (b & 0x80) != 0 ? -1f : 1f;
        var mag = (b & 0x7F) / 127f;
        return sign * (Mathf.Pow(1f + 255f, mag) - 1f) / 255f;
    }
}
