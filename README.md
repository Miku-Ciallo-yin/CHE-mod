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

以上版本与 `D:\st\steamapps\common\Among Us\TOME\TONE 200A5` 中的实际运行环境一致。

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
│   └── ModConfig.cs          # 配置项（BepInEx\config\com.mikuqiayou.che.cfg）
├── Roles/
│   ├── Faction.cs            # 阵营枚举：船员 / 内鬼 / 中立
│   ├── RoleBase.cs           # 职业基类（OnUpdate / OnExile / OnMurder 钩子）
│   ├── CustomRoleManager.cs  # 职业注册、随机分配、阵营判定
│   ├── Crewmate/Sheriff.cs   # 示例职业：警长（船员）
│   ├── Crewmate/Farmer.cs    # 佃农（船员，抢任务解锁击杀，误杀船员转中立）
│   └── Neutral/Jester.cs     # 小丑（中立，被投出即获胜）
└── Patches/
    ├── RoleAssignPatch.cs    # 对局开始分配职业、结束重置
    ├── RoleUpdatePatch.cs    # 职业技能驱动（每帧 OnUpdate）
    ├── MurderPatch.cs        # 击杀结算钩子（OnMurder）
    ├── NamePatch.cs          # 名字下方显示职业与状态行
    ├── ExilePatch.cs         # 放逐检测，触发职业 OnExile 钩子
    └── EndGamePatch.cs       # 结算画面覆盖（自定义胜利者）
```

## 职业一览

| 职业 | 阵营 | 能力 |
| --- | --- | --- |
| 小丑 Jester | 中立 | 被投票放逐即单独获胜 |
| 佃农 Farmer | 船员（可转化） | 靠近船员概率抢夺其任务；抢够数量并完成现有任务后按 `Q` 击杀最近玩家；误杀船员则转为中立阵营 |
| 警长 Sheriff | 船员 | 占位示例，技能未实现 |

## 配置

配置文件：`BepInEx\config\com.mikuqiayou.che.cfg`（首次运行自动生成，改完重启游戏生效）

| 配置项 | 默认值 | 说明 |
| --- | --- | --- |
| 抢夺概率 StealChance | 0.2 | 佃农靠近船员时每秒抢夺一个任务的概率（0~1） |
| 解锁击杀所需任务数 StealsForKill | 3 | 抢夺多少个任务后（并完成现有任务）获得击杀能力 |
| 击杀冷却 KillCooldown | 30 | 佃农击杀冷却（秒） |
| 抢夺范围 StealRange | 1.5 | 佃农抢夺任务的靠近范围（游戏单位） |

## 添加新职业

1. 在 `CHE/Roles/<阵营>/` 下新建类，继承 `RoleBase`，实现 `Name` / `NameEn` / `Faction` / `Color`。
2. 在 `CustomRoleManager.RoleFactories` 中注册一行 `() => new YourRole()`。

## TODO（参考 TONE 的完整功能）

- [ ] RPC 同步职业分配结果（当前仅本机分配，联机需广播）
- [ ] 职业技能按钮与冷却系统
- [x] 中立阵营独立胜利判定（小丑被投出获胜已实现）
- [ ] 职业生成概率 / 数量选项（ lobby 设置界面）
- [ ] 职业介绍过场动画（IntroCutscene 补丁）
- [ ] 本地化多语言支持
