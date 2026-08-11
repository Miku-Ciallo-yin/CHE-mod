using System.IO;

namespace TAHS.Modules;

/// <summary>
/// 黑名单（参考 TONE 的 BanList.txt）：
/// 名单内好友代码的玩家进房即被踢出并封禁。
/// 文件：游戏目录 TAHS-DATA/BanList.txt，每行一个好友代码，# 为注释。
/// </summary>
public static class BanManager
{
    private static readonly HashSet<string> _codes = new(System.StringComparer.OrdinalIgnoreCase);

    public static string FilePath { get; private set; } = string.Empty;

    /// <summary>插件加载时初始化：创建模板文件并读取名单</summary>
    public static void Init()
    {
        try
        {
            var dir = Path.Combine(System.Environment.CurrentDirectory, "TAHS-DATA");
            Directory.CreateDirectory(dir);
            FilePath = Path.Combine(dir, "BanList.txt");

            if (!File.Exists(FilePath))
            {
                File.WriteAllText(FilePath,
                    "# TAHS 黑名单\n" +
                    "# 每行一个好友代码（FriendCode），# 开头为注释\n" +
                    "# 名单内的玩家将无法进入游戏（进房即踢出封禁）\n" +
                    "# 反作弊处理方式为'加入黑名单'时会自动写入\n" +
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

            TAHSPlugin.Log.LogInfo($"[TAHS] 黑名单已加载（{_codes.Count} 人）: {FilePath}");
        }
        catch (System.Exception e)
        {
            TAHSPlugin.Log.LogError($"[TAHS] 黑名单加载失败: {e}");
        }
    }

    /// <summary>好友代码是否在黑名单中</summary>
    public static bool IsBanned(string? friendCode)
    {
        return !string.IsNullOrEmpty(friendCode) && _codes.Contains(friendCode);
    }

    /// <summary>加入黑名单（写入文件）。返回 false 表示已存在或代码为空</summary>
    public static bool Add(string? friendCode)
    {
        if (string.IsNullOrWhiteSpace(friendCode)) return false;
        if (!_codes.Add(friendCode)) return false;

        File.AppendAllText(FilePath, friendCode + "\n");
        TAHSPlugin.Log.LogInfo($"[TAHS] 已加入黑名单: {friendCode}");
        return true;
    }
}
