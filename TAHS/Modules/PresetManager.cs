using System.IO;
using BepInEx;

namespace TAHS.Modules;

/// <summary>
/// 预设方案（参考 TONE）：把全部自定义选项值保存到 TAHS-DATA\Preset{n}.txt，
/// 大厅模组设置页可切换/保存，免去每次开游戏重新调整。
/// 切换预设只改主机本地值，随后经 RPC 广播给客户端（客户端不读自己的预设文件）。
/// </summary>
public static class PresetManager
{
    /// <summary>预设数量</summary>
    public const int PresetCount = 5;

    /// <summary>当前预设编号（1~PresetCount）</summary>
    public static int Current { get; private set; } = 1;

    private static string Dir => Path.Combine(Paths.GameRootPath, "TAHS-DATA");
    private static string FilePath(int preset) => Path.Combine(Dir, $"Preset{preset}.txt");

    /// <summary>插件加载时调用（在 CustomOptions.Init 之后）：恢复预设编号并应用其值</summary>
    public static void Init()
    {
        Current = System.Math.Clamp(ModConfig.CurrentPreset.Value, 1, PresetCount);
        Load();
    }

    /// <summary>切换到下一个预设（循环）并应用其值</summary>
    public static void SwitchNext()
    {
        Current = Current % PresetCount + 1;
        ModConfig.CurrentPreset.Value = Current;
        Load();
    }

    /// <summary>把当前预设文件里的值应用到选项（越界自动夹取，文件缺失则保持现值）</summary>
    public static void Load()
    {
        var path = FilePath(Current);
        if (!File.Exists(path)) return;

        try
        {
            foreach (var line in File.ReadAllLines(path))
            {
                var parts = line.Split('=');
                if (parts.Length != 2) continue;
                if (!byte.TryParse(parts[0], out var id)) continue;
                if (!int.TryParse(parts[1], out var value)) continue;

                var opt = CustomOption.Get(id);
                if (opt == null) continue;
                opt.Value = System.Math.Clamp(value, opt.Min, opt.Max);
            }
            TAHSPlugin.Log.LogInfo($"[TAHS] 已读取预设 {Current}（{path}）");
        }
        catch (System.Exception e)
        {
            TAHSPlugin.Log.LogWarning($"[TAHS] 读取预设失败: {e.Message}");
        }
    }

    /// <summary>把当前全部选项值保存到当前预设</summary>
    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllLines(FilePath(Current), CustomOption.All.Select(o => $"{o.Id}={o.Value}"));
            TAHSPlugin.Log.LogInfo($"[TAHS] 已保存到预设 {Current}（{CustomOption.All.Count} 项）");
        }
        catch (System.Exception e)
        {
            TAHSPlugin.Log.LogWarning($"[TAHS] 保存预设失败: {e.Message}");
        }
    }
}
