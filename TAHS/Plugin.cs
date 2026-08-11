using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace TAHS;

[BepInPlugin(Id, Name, Version)]
[BepInProcess("Among Us.exe")]
public class TAHSPlugin : BasePlugin
{
    public const string Id = "com.mikuqiayou.che";
    public const string Name = "TAHS";
    public const string Version = "1.0.0";

    public static TAHSPlugin Instance { get; private set; } = null!;
    public static new ManualLogSource Log => Instance.log;

    private ManualLogSource log = null!;
    private Harmony? _harmony;

    public override void Load()
    {
        Instance = this;
        log = base.Log;

        _harmony = new Harmony(Id);
        _harmony.PatchAll();

        Modules.ModConfig.Init(Config);
        Modules.CustomOptions.Init();
        Modules.ModeratorManager.Init();
        Modules.BanManager.Init();
        Patches.ForceEndPatch.Init();

        log.LogInfo($"{Name} v{Version} 已加载 — by 米裤恰油");
    }
}
