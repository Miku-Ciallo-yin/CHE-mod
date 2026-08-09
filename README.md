# CHE

> Among Us 职业模组 — 参考 TONE（TownOfNewEpic）架构，为游戏增加更多职业和中立阵营。
>
> 开发者：**米裤恰油**

## 环境

| 依赖 | 版本 | 说明 |
| --- | --- | --- |
| Among Us (Steam) | 2026.3.31 | 直接引用游戏目录 `BepInEx\interop` 中已生成的互操作程序集 |
| BepInEx.Unity.IL2CPP | 6.0.0-be.735 | 模组加载器 |
| HarmonyX | 2.10.2 | 方法补丁库 |
| .NET SDK | 6.0+（目标框架 net6.0） | 开发构建 |

以上版本与游戏目录 `D:\st\steamapps\common\Among Us\CNE` 中的实际运行环境一致（编译直接引用该目录 `BepInEx\interop` 的互操作程序集）。

## 构建

```bash
dotnet build
```

编译成功后 `CHE.dll` 会自动复制到 csproj 中 `AmongUsPluginsDir` 指定的
`BepInEx\plugins` 目录（留空该属性可关闭自动部署），启动游戏即可加载。

## 目录结构

```
CHE/
├── Plugin.cs                 # BepInEx 插件入口
├── Modules/
│   ├── ModConfig.cs          # BepInEx 配置（作为游戏内选项的默认值）
│   ├── CustomOption.cs       # 自定义游戏选项（大厅设置菜单，RPC 同步）
│   └── RpcSync.cs            # 自定义 RPC：职业分配 217 / 选项值 218
├── Roles/
│   ├── Faction.cs            # 阵营枚举：船员 / 内鬼 / 中立
│   ├── RoleBase.cs           # 职业基类（OnUpdate / OnExile / OnMurder 钩子）
│   ├── CustomRoleManager.cs  # 职业注册、随机分配、阵营判定
│   ├── Crewmate/Sheriff.cs   # 示例职业：警长（船员）
│   ├── Crewmate/Farmer.cs    # 佃农（船员，抢任务解锁击杀，误杀船员转中立）
│   └── Neutral/Jester.cs     # 小丑（中立，被投出即获胜）
└── Patches/
    ├── RoleAssignPatch.cs    # 对局开始分配职业（主机）、结束重置
    ├── RpcPatch.cs           # 自定义 RPC 接收入口（拦截 CHE CallId）
    ├── ModGameOptionsMenu.cs  # 设置 UI 共享状态（页签编号/布局常量）
    ├── ModSettingsMenuPatch.cs # 设置菜单页签：模板克隆、按钮、ChangeTab 拦截
    ├── ModOptionsMenuPatch.cs # 页签内容构建（参考 TOHE，Initialize/CreateSettings 接管）
    ├── RoleUpdatePatch.cs    # 职业技能驱动（每帧 OnUpdate）
    ├── MurderPatch.cs        # 击杀结算钩子（OnMurder）
    ├── NamePatch.cs          # 名字下方显示职业与状态行
    ├── ExilePatch.cs         # 放逐检测，触发职业 OnExile 钩子
    ├── ImpostorVisionPatch.cs # 内鬼不互认（名字颜色覆盖，对局+会议）
    ├── GuesserPatch.cs       # 赌怪：会议准星标记 + 猜测面板
    ├── ForceEndPatch.cs      # /end 聊天命令 + ALT+F4 强制结束（仅主机对局中）
    ├── TestModePatch.cs      # 测试模式：跳过正常结束判定
    └── EndGamePatch.cs       # 结算画面覆盖（自定义胜利者）
```

## 职业一览

| 职业 | 类型 | 能力 |
| --- | --- | --- |
| 小丑 Jester | 中立 | 被投票放逐即单独获胜 |
| 佃农 Farmer | 船员（可转化） | 靠近船员概率抢夺其任务；抢够数量并完成现有任务后按 `Q` 击杀最近玩家；误杀船员则转为中立阵营 |
| 警长 Sheriff | 船员 | 占位示例，技能未实现 |
| 赌怪 Guesser | 附加 | 会议中点击他人名牌前的准星打开猜测面板，猜中其职业则对方死亡，猜错自己死亡 |

## 配置

两种方式，游戏内选项优先：

- **大厅设置菜单（推荐）**：创建房间后打开"游戏设置 → 编辑"，顶部页签除原版的
  预设 / 游戏设置 / 职业设置外，新增 **"模组设置"** 和 **"职业设置"** 两个 CHE 页签：
  - 模组设置：全局功能开关（内鬼互认等），平铺调整。
  - 职业设置：一级为职业名称按钮（右侧显示当前生成概率），点击职业名进入该职业的配置页面，
    "← 返回"回到职业列表。
  仅主机可修改，修改后经 RPC 同步给所有客户端。
- **BepInEx 配置文件**：`BepInEx\config\com.mikuqiayou.che.cfg`（首次运行自动生成），
  仅作为游戏内选项的**默认值**，改完重启游戏生效。

| 游戏内选项 | 默认值 | 说明 |
| --- | --- | --- |
| 模组设置：内鬼互认 | 开 | 关闭后内鬼互不相识：对局和会议中内鬼看其他内鬼名字不再显示红色 |
| 模组设置：测试模式 | 关 | 开启后游戏不会正常结束，需 /end 或 ALT+F4 手动强制结束 |
| 生成概率% | 100 | 该职业在开局分配中出现的概率（0~100，步进 10） |
| 佃农：抢夺概率% | 20 | 靠近船员时每秒抢夺一个任务的概率 |
| 佃农：解锁击杀任务数 | 3 | 抢夺多少个任务后（并完成现有任务）获得击杀能力 |
| 佃农：击杀CD(秒) | 30 | 击杀能力冷却时间 |
| 佃农：抢夺范围×0.1 | 15（=1.5） | 抢夺任务的靠近范围（游戏单位） |
| 赌怪：可猜测附加职业 | 关 | 开启后赌怪的猜测列表包含附加职业（如赌怪本身） |

## 添加新职业 / 附加职业

- 主职业：在 `CHE/Roles/<阵营>/` 下新建类继承 `RoleBase`，在 `CustomRoleManager.RoleRegistry` 注册 `(新ID, () => new YourRole())`。
- 附加职业：在 `CHE/Roles/Addons/` 下新建类继承 `AddonBase`，在 `CustomRoleManager.AddonRegistry` 注册（ID 与主职业同空间，勿冲突）。

注册后自动生成"生成概率"设置项并归入对应分类页，ID 用于 RPC 同步，不要改动已有 ID。

## 联机同步说明

- **职业分配**：开局由主机随机分配（按各职业"生成概率"），通过自定义 RPC（CallId 217）广播，各端本地应用。
- **选项值**：主机在大厅设置菜单修改 CHE 选项后，通过 RPC（CallId 218）实时广播；开局分配时再全量同步一次。
- **状态同步**：放逐（小丑获胜）、击杀（佃农转中立）等事件游戏本身会在每个客户端执行，
  任务转移走官方 `RpcSetTasks`，因此这些状态无需额外 RPC 即可保持一致。
- 所有玩家都需要安装本模组才能正常联机游玩。

## TODO（参考 TONE 的完整功能）

- [x] RPC 同步职业分配结果（主机广播，客户端应用）
- [ ] 职业技能按钮与冷却系统
- [x] 中立阵营独立胜利判定（小丑被投出获胜已实现）
- [x] 职业生成概率选项（大厅设置界面，含佃农职业参数）
- [ ] 职业介绍过场动画（IntroCutscene 补丁）
- [ ] 本地化多语言支持
