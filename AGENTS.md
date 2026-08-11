# TAHS — Among Us 职业模组

开发者：米裤恰油

## 首要原则：Host Only（客户端免安装）

**所有修改必须遵循：客户端不安装本模组也可以正常游玩。**

设计约束：

- 对局逻辑一律**主机权威**：职业分配、技能判定、胜负结算都在主机执行；
  客户端（无论是否装模组）只做表现层（UI、输入采集）。
- 需要其他玩家状态的操作，主机通过网络同步的位置/数据计算，不依赖对方客户端。
- 模组端玩家的主动输入（按键等）通过自定义 RPC 发给**主机验证后执行**
  （参考佃农击杀请求 CallId 219），不直接在客户端改变世界状态。
- 状态同步优先使用游戏官方 RPC（如 RpcSetTasks、RpcMurderPlayer），
  无模组客户端看到的效果由官方事件天然保证一致。
- 新增功能必须说明无模组客户端的体验与降级点（例如：无 UI、无法主动触发技能）。

## 环境

- 游戏版本：Among Us Steam 2026.6.5（`D:\st\steamapps\common\Among Us\CNE`）
- BepInEx 6.0.0-be.735（IL2CPP）/ HarmonyX 2.10.2 / net6.0
- 编译直接引用游戏目录 `BepInEx\interop` 的互操作程序集（csproj 的 `AmongUsDir`）
- `dotnet build` 自动部署到 `CNE\BepInEx\plugins`

## IL2CPP 互操作注意事项（已踩过的坑）

- 遍历子对象用 `for + childCount/GetChild` 或 `GetComponentsInChildren<T>`，
  **禁止** `foreach (Transform child in ...)`（InvalidCastException）
- IL2CPP 对象转型用 `.Cast<T>()` / `.TryCast<T>()`，禁止 C 风格强转 / `as`
- ScriptableObject 用 `ScriptableObject.CreateInstance<T>()`，禁止 `new`
- 克隆 UI 后先 `DestroyTranslator()` 销毁 `TextTranslatorTMP` 再设自定义文字
- 委托转换经 `System.Action/Func` 中转：`AddListener((UnityAction)(Action)(() => ...))`
- 不新建 `PassiveButton`（字段为空会 NRE），复用/克隆已装配好的按钮
- System.IO 与 Il2CppSystem.IO 冲突时全部 `global::` 限定

## 自定义 RPC 通道（PlayerControl NetObject，CallId）

| CallId | 用途 | 方向 |
| --- | --- | --- |
| 217 | 职业/附加职业分配 | 主机 → 全员 |
| 218 | 选项值同步 | 主机 → 全员 |
| 219 | 击杀请求（佃农/懦弱者/美警/忏悔者，按职业路由） | 模组端 → 主机 |
| 220 | 玩家 ID 映射 | 主机 → 全员 |
| 221 | 猜测请求（赌怪/猜测模式） | 模组端 → 主机 |
| 222 | 忏悔者变形请求 | 模组端 → 主机 |
| 223 | 向指定客户端显示聊天消息 | 主机 → 指定模组端 |
| 224 | 协管指令请求（/start、/end、/s） | 协管端 → 主机 |
| 225 | 公告广播（/s 醒目消息） | 主机 → 全模组端 |
| 226 | 附加职业赐予（使徒） | 主机 → 全员 |
| 227 | 模组握手（进房时上报，用于区分模组/无模组端） | 模组端 → 主机 |

新增 CallId 从 228 起，避免与游戏及其他模组冲突。

## 结构

- `TAHS/Plugin.cs` — BepInEx 入口
- `TAHS/Modules/` — 配置（ModConfig/CustomOption）、RPC（RpcSync）、弹窗（CustomPopup）、工具
- `TAHS/Roles/` — 职业体系（RoleBase/AddonBase/CustomRoleManager），职业按阵营分目录
- `TAHS/Patches/` — 全部 Harmony 补丁（一功能一文件）

详细说明见 README.md。
