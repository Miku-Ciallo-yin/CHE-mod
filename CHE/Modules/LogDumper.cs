namespace CHE.Modules;

/// <summary>
/// /dump 命令：把当前 BepInEx 日志复制到桌面，并在聊天栏显示最近几行。
/// 注意：System.IO 与游戏的 Il2CppSystem.IO 冲突，全部用 global:: 限定。
/// </summary>
public static class LogDumper
{
    /// <summary>聊天栏显示的日志行数</summary>
    private const int TailLines = 8;

    public static void Dump()
    {
        try
        {
            var logPath = global::System.IO.Path.Combine(
                global::System.Environment.CurrentDirectory, "BepInEx", "LogOutput.log");

            // BepInEx 持有日志写入句柄，需要共享读
            string text;
            using (var fs = new global::System.IO.FileStream(logPath, global::System.IO.FileMode.Open,
                       global::System.IO.FileAccess.Read, global::System.IO.FileShare.ReadWrite))
            using (var reader = new global::System.IO.StreamReader(fs))
                text = reader.ReadToEnd();

            // 保存到桌面
            var timestamp = global::System.DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var fileName = $"CHE-Log-{timestamp}.txt";
            var desktop = global::System.Environment.GetFolderPath(
                global::System.Environment.SpecialFolder.Desktop);
            var dest = global::System.IO.Path.Combine(desktop, fileName);
            global::System.IO.File.WriteAllText(dest, text);

            // 聊天栏显示最近几行
            var lines = text.Split('\n');
            var tail = new List<string>();
            for (var i = lines.Length - 1; i >= 0 && tail.Count < TailLines; i--)
            {
                var line = lines[i].TrimEnd('\r');
                if (!string.IsNullOrWhiteSpace(line))
                    tail.Insert(0, line);
            }

            ShowInChat($"[CHE] 日志已保存到桌面: {fileName}");
            foreach (var line in tail)
                ShowInChat(line);

            CHEPlugin.Log.LogInfo($"[CHE] 日志已导出: {dest}");
        }
        catch (System.Exception e)
        {
            CHEPlugin.Log.LogError($"[CHE] 日志导出失败: {e}");
            ShowInChat($"[CHE] 日志导出失败: {e.Message}");
        }
    }

    private static void ShowInChat(string message) => ChatHelper.Show(message);
}
