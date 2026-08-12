using HarmonyLib;

namespace TAHS.Patches;

/// <summary>
/// 官方服房间隔离（参考 TONE/TOH_Y 对模组房隐藏公开入口的做法）：
/// 官方服务器上模组房间一律保持私密——不出现在原版公开房间列表，
/// 任何玩家（含无模组端）只能通过搜索房间代码加入，避免路人误入举报导致官方反作弊封禁。
/// 社区服（Niko-AS 等 StaticHttpRegionInfo）不受影响，可正常开公开房。
/// 建房流程上房间总是先私密创建，仅大厅内 MakePublic 会公开，拦截它即可。
/// </summary>
[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.MakePublic))]
public static class OfficialIsolationPatch
{
    private static float _lastWarnTime;

    public static bool Prefix()
    {
        if (!OnOfficialServer()) return true;

        if (UnityEngine.Time.time - _lastWarnTime > 2f)
        {
            _lastWarnTime = UnityEngine.Time.time;
            Modules.ChatHelper.Show("[TAHS] 官方服模组房间已隔离：无法公开，只能凭房间代码加入（公开房请切换到 Niko 社区服）");
        }
        TAHSPlugin.Log.LogInfo("[TAHS] 官方服隔离：已阻止房间设为公开");
        return false;
    }

    /// <summary>当前是否连接官方服务器（社区服为 StaticHttpRegionInfo，官方服不是）</summary>
    public static bool OnOfficialServer()
    {
        var region = ServerManager.Instance?.CurrentRegion;
        if (region == null) return false;
        return region.TryCast<StaticHttpRegionInfo>() == null;
    }
}
