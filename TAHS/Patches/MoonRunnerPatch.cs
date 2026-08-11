using Object = UnityEngine.Object;
using TAHS.Modules;
using TAHS.Roles.Neutral;
using HarmonyLib;
using UnityEngine;

namespace TAHS.Patches;

/// <summary>
/// 月跑入机配套补丁：
/// - 击杀拦截：技能期间无敌 / 追杀者在后者死前无法被击杀
/// - 投票拦截：受保护的追杀者无法被投票（返还投票并提示）
/// - 速度增益：PlayerPhysics.FixedUpdate 应用倍率
/// - 追杀箭头：追杀者本机显示指向后者的箭头
/// </summary>
public static class MoonRunnerPatch
{
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
    public static class MurderBlock
    {
        public static bool Prefix(PlayerControl __instance, PlayerControl target)
        {
            if (target == null || __instance == null) return true;

            // 月跑入机技能期间（有激活增益）无敌
            if (MoonRunner.HasActiveBuffAnywhere(target)) return false;
            // 追杀者在后者死亡前无法被击杀
            if (MoonRunner.IsProtectedHunter(target)) return false;
            // 追杀者只能击杀后者（击杀按钮路径也要拦）
            if (MoonRunner.HunterPrey.TryGetValue(__instance.PlayerId, out var preyId)
                && target.PlayerId != preyId)
                return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CmdCastVote))]
    public static class VoteBlock
    {
        public static bool Prefix(byte playerId, byte suspectIdx)
        {
            var suspect = PlayerControl.AllPlayerControls.ToArray()
                .FirstOrDefault(p => p != null && p.PlayerId == suspectIdx);
            if (suspect == null || !MoonRunner.IsProtectedHunter(suspect)) return true;

            if (PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.PlayerId == playerId)
                ChatHelper.Show("[TAHS] 该对象目前无法投票");
            return false; // 返还投票
        }
    }

    [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.FixedUpdate))]
    public static class SpeedBuff
    {
        public static void Postfix(PlayerPhysics __instance)
        {
            var player = __instance.myPlayer;
            if (player == null) return;

            var mult = MoonRunner.GetSpeedMultiplier(player);
            if (!Mathf.Approximately(mult, 1f))
                __instance.Speed *= mult;
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class HuntArrow
    {
        private static GameObject? _arrow;
        private static Sprite? _sprite;

        public static void Postfix(PlayerControl __instance)
        {
            if (!__instance.AmOwner) return;

            var isHunter = MoonRunner.HunterPrey.TryGetValue(__instance.PlayerId, out var preyId)
                           && MoonRunner.IsProtectedHunter(__instance);
            if (!isHunter)
            {
                if (_arrow != null) { Object.Destroy(_arrow); _arrow = null; }
                return;
            }

            var prey = PlayerControl.AllPlayerControls.ToArray()
                .FirstOrDefault(p => p != null && p.PlayerId == preyId);
            if (prey == null) return;

            // 箭头对象（首次创建）
            if (_arrow == null)
            {
                _arrow = new GameObject("TAHS_HuntArrow");
                var sr = _arrow.AddComponent<SpriteRenderer>();
                sr.sprite = GetArrowSprite();
                sr.color = new Color(1f, 0.2f, 0.2f, 0.9f);
            }

            var from = __instance.GetTruePosition();
            var dir = (prey.GetTruePosition() - from);
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;
            dir.Normalize();

            _arrow.transform.position = new Vector3(from.x + dir.x * 0.8f, from.y + dir.y * 0.8f, -5f);
            var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            _arrow.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>程序生成的三角形箭头</summary>
        private static Sprite GetArrowSprite()
        {
            if (_sprite != null) return _sprite;

            const int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (var y = 0; y < s; y++)
            for (var x = 0; x < s; x++)
            {
                // 朝上的实心三角
                var half = (s - 1 - y) * 0.5f;
                var inside = x >= s / 2f - half && x <= s / 2f + half;
                tex.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
            tex.Apply();

            _sprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            return _sprite;
        }
    }
}
