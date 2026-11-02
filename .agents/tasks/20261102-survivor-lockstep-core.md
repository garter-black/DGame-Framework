# 幸存者帧同步战斗核心（单机阶段）

- ID: 20261102-survivor-lockstep-core
- 状态: complete（单机阶段）；联机阶段待做
- 负责人: fuyuoo（Claude Code 协助）

## 目标与验收

- [x] 前后端共用的确定性战斗核心：固定 20 帧/秒、指令驱动、定点数、SoA 数组
- [x] 录像（开局参数 + 每帧指令）序列化/重放，状态哈希
- [x] 自动检查：禁用写法扫描、逐帧哈希一致、哈希敏感性、压测
- [x] Unity 沙盒场景：本地输入、插值渲染、GPU Instancing、录像保存与重放校验
- [x] 跨运行时一致：Unity 录像在 .NET 8 重放哈希相同

## 范围与决策

- 修改范围: `GameBattle/Survivor/**`（新增）、`GameLogic/BattleSys/Survivor/**`（新增）、`GameLogic.asmdef` 增加 DGame.FixedPoint 引用、`Scenes/BattleSandbox`（新增）、`GameServer/Tools/BattleCheck`（新增）、`Docs/SurvivorBattle.md`。
- 不修改: 现有 GameBattle ECS、登录/房间流程、协议、Fantasy.config。
- 关键选择及原因:
  - 不用现有 GameBattle ECS 承载敌人：它按对象逐个分发 Update 并吞掉异常，2000 个对象实体在解释执行下开销大；改用 SoA 数组 + 固定顺序的静态系统。
  - 战斗核心不依赖 Unity/Fantasy，BattleCheck 只编译 FixedPoint/Math + Survivor 即可运行，服务端可直接复用。
  - 指令 `PlayerInput` 改名 `BattleInput`，避免与 `UnityEngine.InputSystem.PlayerInput` 冲突。
  - 无敌开关作为 `BattleSettings` 字段写入录像，避免在模拟外改状态导致重放不一致。

## 授权

- 用户原始请求或确认位置: 会话中"继续做"（压测 + 帧同步骨架 + 联机骨架 + 开发说明）。
- 允许的目标、操作及输入: 新增源码、场景、工具项目；asmdef 增加引用；编辑器临时切换 Release 编译测量后已切回 Debug。
- 高风险单独确认: 无（未改 ProjectSettings、包依赖，未构建、未推送）。

## 实施与证据

- [x] 实现
- [x] `dotnet run -c Release --project GameServer/Tools/BattleCheck` → ALL PASSED
- [x] Unity `recompile` → completed, 0 errors；`dotnet build GameServer/Server/Entity` → 0 errors
- [x] 沙盒 Play：无错误日志；F6 "重放一致"（518 帧）；F5 录像 `survivor_f540_94CB03DBD408A298.replay` 在 .NET 重放 → MATCH
- 压测（2000 怪 / 帧）: .NET 8 1.17ms；编辑器 Mono Release 4.36ms；Debug 15.3ms

## 剩余风险

- 未执行的检查及原因: HybridCLR 解释执行下的真机耗时（未安装 HybridCLR 原生部分、未打包）。
- 需要的人工动作: 决定战斗核心是否移出热更程序集（需真机数据）；数值平衡（默认难度下玩家约 90 秒阵亡）。
- 后续: 联机阶段——协议（帧指令上行/广播）、服务端房间帧推进与哈希校验、客户端网络输入源。
