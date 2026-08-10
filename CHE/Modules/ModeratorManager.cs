using System.IO;

namespace CHE.Modules;

/// <summary>
/// 协管名单（参考 TONE 的 Moderators.txt）：
/// - 名单文件：游戏目录 CHE-DATA/Moderators.txt，每行一个好友代码（FriendCode），# 为注释
/// - 房主输入 /addmod <玩家ID> 自动把该玩家好友代码写入名单
/// - 模组设置开启"协管名单"后，名单内玩家可使用部分房主指令（/start、/end）
/// </summary>
public static class ModeratorManager
{
    private static readonly HashSet<string> _codes = new(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>名单文件路径</summary>
    public static string FilePath { get; private set; } = string.Empty;

    /// <summary>协管名单是否启用（模组设置）</summary>
    public static bool IsEnabled => CustomOptions.ModeratorList.Value == 1;

    /// <summary>插件加载时初始化：创建模板文件并读取名单</summary>
    public static void Init()
    {
        try
        {
            var dir = Path.Combine(System.Environment.CurrentDirectory, "CHE-DATA");
            Directory.CreateDirectory(dir);
            FilePath = Path.Combine(dir, "Moderators.txt");

            if (!File.Exists(FilePath))
            {
                File.WriteAllText(FilePath,
                    "# CHE 协管名单\n" +
                    "# 每行一个好友代码（FriendCode），# 开头为注释\n" +
                    "# 房主输入 /addmod <玩家ID> 可自动添加\n" +
                    "# 示例：\n" +
                    "# ABCD#1234\n");
            }

            _codes.Clear();
            foreach (var line in File.ReadAllLines(FilePath))
            {
                var code = line.Trim();
                if (code.Length == 0 || code.StartsWith('#')) continue;
                _codes.Add(code);
            }

            CHEPlugin.Log.LogInfo($"[CHE] 协管名单已加载（{_codes.Count} 人）: {FilePath}");
        }
        catch (System.Exception e)
        {
            CHEPlugin.Log.LogError($"[CHE] 协管名单加载失败: {e}");
        }
    }

    /// <summary>玩家是否在协管名单中</summary>
    public static bool IsModerator(PlayerControl player)
    {
        var code = player?.Data?.FriendCode;
        return !string.IsNullOrEmpty(code) && _codes.Contains(code);
    }

    /// <summary>添加协管（写入文件）。返回 false 表示已在名单中或代码为空</summary>
    public static bool Add(string? friendCode)
    {
        if (string.IsNullOrWhiteSpace(friendCode)) return false;
        if (!_codes.Add(friendCode)) return false;

        File.AppendAllText(FilePath, friendCode + "\n");
        CHEPlugin.Log.LogInfo($"[CHE] 新增协管: {friendCode}");
        return true;
    }
}
