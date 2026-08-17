namespace TAHS.Modules;

/// <summary>
/// RPC 发送限流器：把同一时刻的 RPC 突发（开局身份下发、定向私聊/标签等）
/// 摊到多帧发送，防止打包成超大包被官方服踢出/被大包守卫拦截
/// （8 人以上开局突发会触发黑屏的根因）。
/// </summary>
public static class RateLimiter
{
    /// <summary>每帧最多发出的消息数（打包器按帧组包，控制在安全大小内）</summary>
    private const int MaxPerFrame = 4;

    private static readonly System.Collections.Generic.Queue<System.Action> _queue = new();

    /// <summary>把一个发送动作排入队列（下一帧起按限流执行）</summary>
    public static void Enqueue(System.Action action)
    {
        if (action != null) _queue.Enqueue(action);
    }

    /// <summary>每帧驱动（AnnouncementPatch 调用）</summary>
    public static void Tick()
    {
        for (var i = 0; i < MaxPerFrame && _queue.Count > 0; i++)
        {
            try
            {
                _queue.Dequeue().Invoke();
            }
            catch (System.Exception e)
            {
                TAHSPlugin.Log.LogWarning($"[TAHS] 限流队列执行失败: {e.Message}");
            }
        }
    }

    /// <summary>队列是否已清空（调试用）</summary>
    public static bool Idle => _queue.Count == 0;

    /// <summary>清空未执行的队列（开新一局/回大厅时调用，防止旧局动作泄漏到新局）</summary>
    public static void Clear() => _queue.Clear();
}
