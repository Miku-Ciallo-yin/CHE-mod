namespace TAHS.Modules;

/// <summary>
/// 对局归档：记录本局的初始分配、身份转换、击杀记录；
/// 对局结束时归档为"上一局"，供大厅中 /l 查看。
/// </summary>
public static class GameArchive
{
    // 本局进行中
    private static readonly List<string> _assignments = new();
    private static readonly List<string> _transitions = new();
    private static readonly List<string> _kills = new();

    // 上一局归档
    private static readonly List<string> _lastAssignments = new();
    private static readonly List<string> _lastTransitions = new();
    private static readonly List<string> _lastKills = new();

    public static void RecordAssignment(string text) => _assignments.Add(text);
    public static void RecordTransition(string text) => _transitions.Add(text);
    public static void RecordKill(string text) => _kills.Add(text);

    /// <summary>对局结束/重开时调用：归档本局记录并清空</summary>
    public static void ArchiveAndReset()
    {
        // 只有真正进行过对局（有记录）才覆盖归档
        if (_assignments.Count > 0 || _kills.Count > 0 || _transitions.Count > 0)
        {
            Copy(_assignments, _lastAssignments);
            Copy(_transitions, _lastTransitions);
            Copy(_kills, _lastKills);
        }

        _assignments.Clear();
        _transitions.Clear();
        _kills.Clear();
    }

    private static void Copy(List<string> from, List<string> to)
    {
        to.Clear();
        to.AddRange(from);
    }

    /// <summary>输出上一局回顾（仅本机聊天栏）</summary>
    public static void ShowLast()
    {
        ChatHelper.ShowMany(BuildLastLines());
    }

    /// <summary>上一局回顾内容（本地显示与主机私信无模组端共用）</summary>
    public static List<string> BuildLastLines()
    {
        if (_lastAssignments.Count == 0 && _lastKills.Count == 0 && _lastTransitions.Count == 0)
            return new List<string> { "[TAHS] 暂无上一局记录" };

        var lines = new List<string> { "<color=#4FC3F7>===== 上一局回顾 =====</color>" };

        if (_lastAssignments.Count > 0)
        {
            lines.Add("-- 初始分配 --");
            lines.AddRange(_lastAssignments);
        }
        if (_lastTransitions.Count > 0)
        {
            lines.Add("-- 身份转换 --");
            lines.AddRange(_lastTransitions);
        }
        if (_lastKills.Count > 0)
        {
            lines.Add("-- 击杀记录 --");
            lines.AddRange(_lastKills);
        }

        return lines;
    }
}
