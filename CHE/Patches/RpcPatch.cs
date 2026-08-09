using CHE.Modules;
using HarmonyLib;
using Hazel;

namespace CHE.Patches;

/// <summary>
/// 自定义 RPC 接收入口：拦截 CHE 的 CallId，其余交给游戏原逻辑。
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
public static class RpcPatch
{
    public static bool Prefix(byte callId, MessageReader reader)
    {
        // 已处理的自定义 RPC 不再进入游戏原始处理
        return !RpcSync.Handle(callId, reader);
    }
}
