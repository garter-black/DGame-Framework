# GameConfig/CLAUDE.md

本文件补充仓库根目录 [CLAUDE.md](../CLAUDE.md)，只适用于 `GameConfig/` 配置源及导表工具。

## 技能路由

编辑 `Datas/`、`Defines/`、`CustomTemplate/`、`luban.conf` 或 `GenerateTool_Binary/` 时，必须读取 [luban-dev skill](../.agents/skills/luban-dev/SKILL.md) 及对应 reference。客户端运行时消费见 `../.agents/skills/dgame-dev/references/luban-config.md`。

## 约束

- 生成产物不手改：客户端 `GameUnity/Assets/Scripts/HotFix/GameProto/LubanConfig/`、`GameUnity/Assets/BundleAssets/Configs/`、`GameUnity/Configs/Json/`，服务端 `GameServer/Server/Entity/Generate/Config/`、`GameServer/Configs/`。
- 导表同时影响客户端和服务端；生产导表走 `workflow.py luban validate` → `preview` → 用户本人 `approve` → `apply`。
- `Tools/LubanTools/` 与 `GenerateTool_Binary/path_define.*` 被 `.gitignore` 忽略；缺失时按 [Docs/SurvivorBattle.md](../Docs/SurvivorBattle.md) 的“配置表转表”一节准备。
