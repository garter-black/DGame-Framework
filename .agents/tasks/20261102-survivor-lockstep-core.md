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

---

# 单人战斗服务端校验（2026-11-02）

- 状态: complete

## 目标与验收

- [x] 服务端下发种子开局；客户端每秒上传指令批次 + 检查点哈希；服务端增量重放核对；结算以服务端为准
- [x] 共享 `BattleVerifier`，联机阶段复用
- [x] 实机端到端：登录 → 主界面"进入战斗" → 战斗 → 服务端日志 `verified: frame=520 kills=23 died=True`
- [x] 作弊拦截：改内存击杀数 → 5005（frame 100 检查点）；客户端跳帧加速 → 5004（frame 260 > allowed 154）；离线测试覆盖篡改指令、跳批次、超大批次

## 范围与决策

- 修改: `BattleMessage.proto` 新增 3 对请求并重新导出；`ErrorCode.cs` 新增 5001~5005；Gate 新增 SoloBattleComponent / System / 3 个 Handler；`MainPage` 不在房间时显示"进入战斗"并走单人战斗；沙盒渲染抽成 `SurvivorBattleView`。
- 决策: 组件挂 Session（断线即作废）；BattleId 用 RuntimeId（组件 Id 与 Session 相同，无法区分前后两局）；检查点哈希 0 表示该批不校验（积压拆批时），后续检查点和结算仍覆盖；战斗用独立透视相机 + 自发光材质（GameStart 场景无灯光）。

## 实施与证据

- `dotnet run -c Release --project GameServer/Tools/BattleCheck` → ALL PASSED（新增 verifier 项）
- `dotnet build GameServer/Server/Server.sln` → 0 错误；Unity recompile → 0 错误
- 实机 4 局：2 局正常校验通过，1 局改内存被拒（5005），1 局加速被拒（5004）

## 剩余风险

- 导出工具 2025.2.1414 与 `define-common.md` 冲突：文档写 `bytes`→`byte[]`，工具不认，需用 `repeatedArray byte`。
- 上游 `PlayerFuncOpenComponentSystem.cs:148` 用 `FromUnixTimeSeconds(TimeHelper.Now)`，但实测 `TimeHelper.Now` 是毫秒，疑似上游 bug，未改。
- 新增错误码没有客户端文本配置，提示里直接显示错误码。
- 编辑器里 `Time.timeScale` 被 DGame 接管，无法用它模拟加速；加速测试改为直接推进驱动。

---

# HybridCLR 安装与 Windows 包性能实测（2026-11-03）

- 状态: complete

## 目标与验收

- [x] 安装 Unity 6000.3.10f1 Windows IL2CPP 模块（Hub CLI）、HybridCLR 原生部分（Installer v8.9.0）
- [x] HybridCLR/Generate/All 成功（85 个裁剪 AOT DLL）；AutoBuildWindow 出包 `Build/Windows/Release_Windows.exe`
- [x] 包内压测：1000/2000/3000 怪 6.26/15.2/25.6ms，哈希与 .NET 一致

## 范围与决策

- 授权: 用户"做 1 和 2，打 windows 就可以"、"你来装"。
- 开启 HybridCLR（上游默认关闭，否则 Generate All 不复制裁剪 AOT DLL，报 no aot assembly found）。
- 关闭 Obfuz 构建期混淆：上游构建期开启但运行时宏未定义，且 Unity 6.3 下 CallObfus 空引用崩溃；混淆也会干扰性能数据。正式启用需单独处理。
- 压测入口 `BattleBench.TryRun()` 接在 `GameStart.StartGame` 开头：DataCenterSys 懒初始化，登录前不会执行数据中心模块的 OnInit。
- 未转表：配置无改动，避免写生产目录。

## 剩余风险

- 未测 AOT（非热更）下的耗时；按 .NET 结果估计接近 1~2ms，需移动程序集后实测。
- 未测手机。
- Obfuz 目前关闭，发布前如需混淆要修复。

## 补充：关闭 HybridCLR 的 AOT 实测（2026-11-03）

- 菜单"禁用HybridCLR"后直接 AutoBuildWindow 成功；正常启动进入 GameStart、Fantasy 初始化，无异常。
- 压测：1000/2000/3000 怪 0.88/2.06/3.45ms（解释执行 6.26/15.2/25.6ms，约快 7.4 倍），哈希与 .NET 一致。
- 菜单只移除当前平台（Standalone）的 ENABLE_HYBRIDCLR，Android/iOS 等平台宏仍在。
- HybridCLR 开关状态待用户决定，设置改动未提交。
