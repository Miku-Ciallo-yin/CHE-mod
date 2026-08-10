using Object = UnityEngine.Object;
using CHE.Modules;
using CHE.Roles;
using CHE.Roles.Addons;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace CHE.Patches;

/// <summary>
/// 赌怪会议 UI（参考 TOHE 的 Guesser 实现思路）：
/// - 会议开始：本机玩家是赌怪时，在其他存活玩家名牌前生成准星标记
/// - 点击准星：打开猜测面板，列出当前已启用（生成概率 &gt; 0）的全部职业名称
/// - 点击名称即猜测：猜对目标死亡，猜错赌怪自己死亡
/// - 点击判定用碰撞盒 bounds 手动检测（不新建 PassiveButton，避免空引用）
/// </summary>
[HarmonyPatch(typeof(MeetingHud))]
public static class GuesserPatch
{
    /// <summary>猜测条目（职业或附加职业）</summary>
    public class GuessEntry
    {
        public bool IsAddon;
        public byte Id;
        public string Name = string.Empty;
    }

    private static readonly List<(SpriteRenderer Mark, PlayerControl Target)> _marks = new();
    private static GameObject? _panel;
    private static readonly List<(BoxCollider2D Col, GuessEntry Entry)> _panelItems = new();
    private static PlayerControl? _panelTarget;

    private static Sprite? _crosshairSprite;
    private static Sprite? _solidSprite;

    /// <summary>
    /// 玩家是否可以使用猜测功能（赌怪与猜测模式互不干扰）：
    /// - 拥有赌怪附加职业：始终可猜（与猜测模式开关无关）
    /// - 猜测模式开启：按阵营勾选放行（无需赌怪附加）
    /// </summary>
    public static bool CanGuess(PlayerControl player)
    {
        if (CustomRoleManager.HasAddon(player, Guesser.AddonId)) return true;
        if (CustomOptions.GuessMode.Value != 1) return false;

        var faction = CustomRoleManager.GetFaction(player);
        return faction switch
        {
            Faction.Crewmate => CustomOptions.GuessCrewmate.Value == 1,
            Faction.Impostor => CustomOptions.GuessImpostor.Value == 1,
            Faction.Neutral => IsHostileNeutral(player)
                ? CustomOptions.GuessHostileNeutral.Value == 1
                : CustomOptions.GuessFriendlyNeutral.Value == 1,
            _ => false,
        };
    }

    private static bool IsHostileNeutral(PlayerControl player)
    {
        return CustomRoleManager.GetRole(player)?.IsHostileNeutral ?? false;
    }

    [HarmonyPatch(nameof(MeetingHud.Start)), HarmonyPostfix]
    public static void StartPostfix(MeetingHud __instance)
    {        Cleanup();
        try
        {
            var local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null || local.Data.IsDead) return;
            if (!CanGuess(local)) return;

            foreach (var pva in __instance.playerStates)
            {
                var target = PlayerControl.AllPlayerControls.ToArray()
                    .FirstOrDefault(p => p != null && p.PlayerId == pva.TargetPlayerId);
                if (target == null || target == local) continue;
                if (target.Data == null || target.Data.IsDead) continue;
                if (pva.NameText == null) continue;

                // 名牌前的准星标记
                var mark = new GameObject("CHE_GuessMark").AddComponent<SpriteRenderer>();
                mark.sprite = GetCrosshairSprite();
                mark.transform.SetParent(pva.transform, false);
                mark.transform.localPosition =
                    pva.NameText.transform.localPosition + new Vector3(-0.55f, 0f, -5f);
                mark.transform.localScale = Vector3.one * 0.4f;

                var col = mark.gameObject.AddComponent<BoxCollider2D>();
                col.size = new Vector2(2.2f, 2.2f); // 点击区域比图标大一些

                _marks.Add((mark, target));
            }
        }
        catch (System.Exception e)
        {
            CHEPlugin.Log.LogError($"[CHE] 赌怪标记创建失败: {e}");
        }
    }

    [HarmonyPatch(nameof(MeetingHud.Update)), HarmonyPostfix]
    public static void UpdatePostfix(MeetingHud __instance)
    {
        if (_marks.Count == 0 && _panel == null) return;
        if (!Input.GetMouseButtonDown(0)) return;

        var cam = Camera.main;
        if (cam == null) return;
        var pos = cam.ScreenToWorldPoint(Input.mousePosition);

        // 面板打开时：点条目 = 猜测；点其他位置 = 关闭
        if (_panel != null)
        {
            foreach (var (col, entry) in _panelItems)
            {
                if (col == null || _panelTarget == null) continue;
                if (col.bounds.Contains(pos))
                {
                    RequestGuess(PlayerControl.LocalPlayer, _panelTarget, entry);
                    break;
                }
            }
            ClosePanel();
            return;
        }

        foreach (var (mark, target) in _marks)
        {
            if (mark == null) continue;
            if (mark.bounds.Contains(pos))
            {
                OpenPanel(__instance, target);
                break;
            }
        }
    }

    [HarmonyPatch(nameof(MeetingHud.Close)), HarmonyPostfix]
    public static void ClosePostfix() => Cleanup();

    [HarmonyPatch(nameof(MeetingHud.OnDestroy)), HarmonyPostfix]
    public static void OnDestroyPostfix() => Cleanup();

    /// <summary>当前已启用（生成概率 > 0）的猜测条目（含配置开启时的附加职业）</summary>
    public static List<GuessEntry> GetEnabledEntries()
    {
        var entries = new List<GuessEntry>();
        foreach (var (id, name, _) in CustomRoleManager.GetRegisteredRoles())
            if (CustomOptions.GetRoleChance(id) > 0)
                entries.Add(new GuessEntry { Id = id, Name = name });
        if (CustomOptions.GuesserCanGuessAddons.Value == 1)
            foreach (var (id, name) in CustomRoleManager.GetRegisteredAddons())
                if (CustomOptions.GetRoleChance(id) > 0)
                    entries.Add(new GuessEntry { IsAddon = true, Id = id, Name = $"{name}(附加)" });
        return entries;
    }

    /// <summary>打开猜测面板：当前已启用的全部职业（可选包含附加职业）</summary>
    private static void OpenPanel(MeetingHud meeting, PlayerControl target)
    {
        ClosePanel();
        _panelTarget = target;

        var entries = GetEnabledEntries();

        var template = meeting.playerStates[0].NameText;

        _panel = new GameObject("CHE_GuessPanel");
        _panel.transform.SetParent(meeting.transform, false);
        _panel.transform.localPosition = new Vector3(0f, 0f, -50f);

        // 背景
        var bg = new GameObject("CHE_GuessPanelBG").AddComponent<SpriteRenderer>();
        bg.sprite = GetSolidSprite();
        bg.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);
        bg.transform.SetParent(_panel.transform, false);
        bg.transform.localScale = new Vector3(5.5f, 1.2f + 0.55f * (entries.Count + 1), 1f);

        // 标题
        var title = Object.Instantiate(template, _panel.transform);
        title.DestroyTranslator();
        title.text = $"猜测 {(target.Data != null ? target.Data.PlayerName : "?")} 的职业";
        title.transform.localPosition = new Vector3(0f, 0.55f * entries.Count * 0.5f + 0.35f, -1f);

        // 条目
        var y = 0.55f * (entries.Count - 1) * 0.5f;
        foreach (var entry in entries)
        {
            var label = Object.Instantiate(template, _panel.transform);
            label.DestroyTranslator();
            label.text = entry.Name;
            label.transform.localPosition = new Vector3(0f, y, -1f);

            var col = label.gameObject.AddComponent<BoxCollider2D>();
            col.size = new Vector2(4.5f, 0.5f);
            _panelItems.Add((col, entry));

            y -= 0.55f;
        }
    }

    private static void ClosePanel()
    {
        if (_panel != null) Object.Destroy(_panel);
        _panel = null;
        _panelTarget = null;
        _panelItems.Clear();
    }

    /// <summary>
    /// 发起一次猜测（模组端入口：准星面板 / /bt 命令）：
    /// 主机本地直接执行；非主机模组端经 RPC（CallId 221）由主机验证后执行（Host Only 原则）。
    /// </summary>
    public static void RequestGuess(PlayerControl guesser, PlayerControl target, GuessEntry entry)
    {
        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
        {
            ExecuteGuess(guesser, target, entry);
            return;
        }
        RpcSync.SendGuessRequest(target.PlayerId, entry.IsAddon, entry.Id);
    }

    /// <summary>主机：验证并执行猜测（猜对目标死，猜错猜测者死）</summary>
    public static void ExecuteGuess(PlayerControl? guesser, PlayerControl? target, GuessEntry entry)
    {
        if (guesser == null || guesser.Data == null || guesser.Data.IsDead) return;
        if (target == null || target.Data == null || target.Data.IsDead) return;
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
        if (!CanGuess(guesser)) return;

        var correct = entry.IsAddon
            ? CustomRoleManager.GetAddons(target).Any(a => a.Id == entry.Id)
            : CustomRoleManager.GetRole(target)?.Id == entry.Id;

        var victim = correct ? target : guesser;
        CHEPlugin.Log.LogInfo(
            $"[CHE] {guesser.Data.PlayerName} 猜测 {target.Data.PlayerName} 是 {entry.Name}" +
            $"：{(correct ? "正确" : "错误")}，{victim.Data!.PlayerName} 死亡");

        // 自杀式击杀走游戏 RPC，各端一致；会议中死亡由原版流程处理
        victim.RpcMurderPlayer(victim, true);
    }

    private static void Cleanup()
    {
        foreach (var (mark, _) in _marks)
            if (mark != null) Object.Destroy(mark.gameObject);
        _marks.Clear();
        ClosePanel();
    }

    /// <summary>程序生成的准星图标（圆环 + 十字线）</summary>
    private static Sprite GetCrosshairSprite()
    {
        if (_crosshairSprite != null) return _crosshairSprite;

        const int s = 64;
        const float center = (s - 1) / 2f;
        var color = new Color(1f, 0.25f, 0.25f);

        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (var x = 0; x < s; x++)
        for (var y = 0; y < s; y++)
        {
            var dx = x - center;
            var dy = y - center;
            var dist = Mathf.Sqrt(dx * dx + dy * dy);

            var ring = Mathf.Abs(dist - 21f) < 2f;
            var cross = (Mathf.Abs(dx) < 2f || Mathf.Abs(dy) < 2f) && dist > 12f && dist < 29f;
            tex.SetPixel(x, y, ring || cross ? color : Color.clear);
        }
        tex.Apply();

        _crosshairSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return _crosshairSprite;
    }

    /// <summary>1x1 纯色图（面板背景用，颜色由 SpriteRenderer.color 控制）</summary>
    private static Sprite GetSolidSprite()
    {
        if (_solidSprite != null) return _solidSprite;

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();

        _solidSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        return _solidSprite;
    }
}
