# CHE

> Among Us 职业模组 — 参考 TONE（TownOfNewEpic）架构，为游戏增加更多职业和中立阵营。
>
> 开发者：**米裤恰油**

## 环境

| 依赖 | 版本 | 说明 |
| --- | --- | --- |
| Among Us (Steam) | 2026.3.31 | 与 `AmongUs.GameLibs.Steam` 包锁定一致 |
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
├── Roles/
│   ├── Faction.cs            # 阵营枚举：船员 / 内鬼 / 中立
│   ├── RoleBase.cs           # 职业基类
│   ├── CustomRoleManager.cs        # 职业注册与随机分配
│   ├── Crewmate/Sheriff.cs   # 示例职业：警长（船员）
│   └── Neutral/Jester.cs     # 示例职业：小丑（中立）
└── Patches/
    ├── RoleAssignPatch.cs    # 对局开始分配职业、结束重置
    └── NamePatch.cs          # 名字下方显示本机职业
```

## 添加新职业

1. 在 `CHE/Roles/<阵营>/` 下新建类，继承 `RoleBase`，实现 `Name` / `NameEn` / `Faction` / `Color`。
2. 在 `CustomRoleManager.RoleFactories` 中注册一行 `() => new YourRole()`。

## TODO（参考 TONE 的完整功能）

- [ ] RPC 同步职业分配结果（当前仅本机分配，联机需广播）
- [ ] 职业技能按钮与冷却系统
- [ ] 中立阵营独立胜利判定（接管游戏结算流程）
- [ ] 职业生成概率 / 数量选项（ lobby 设置界面）
- [ ] 职业介绍过场动画（IntroCutscene 补丁）
- [ ] 本地化多语言支持
