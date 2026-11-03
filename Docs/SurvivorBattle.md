# 幸存者战斗开发说明

## 结构

| 位置 | 内容 | 谁用 |
|---|---|---|
| `GameUnity/Assets/Scripts/HotFix/GameBattle/Survivor/` | 战斗核心：状态、系统、录像、哈希 | 客户端 + 服务端 + BattleCheck 共用同一份源码 |
| `GameUnity/Assets/Scripts/HotFix/GameLogic/BattleSys/Survivor/` | 表现层：本地驱动、沙盒渲染 | 仅客户端 |
| `GameServer/Server/Entity/Entity.csproj` | 链接 FixedPoint 和 GameBattle 源码 | 服务端 |
| `GameServer/Tools/BattleCheck/` | 确定性检查和压测 | 开发时运行 |
| `Assets/Scenes/BattleSandbox/BattleSandbox.unity` | 沙盒场景，直接进战斗，不走登录 | 调玩法、压测 |

数据流：`BattleInput[]`（每帧每人 2 字节）→ `BattleSimulation.Tick` → `BattleState`（数组存放）→ 表现层按 `PrevPos`/`Pos` 插值渲染。表现层只读状态，绝不回写。

## 单人战斗服务端校验

入口：登录后主界面不在房间时，"进入战斗"按钮开始单人战斗（`MainPage.OnClickStartBtn`）。

```
客户端 SoloBattleMgr                       服务端 Gate（SoloBattleComponent 挂在 Session 上）
C2G_SoloBattleStartRequest      ──────▶   生成种子和难度参数，创建 BattleVerifier
                                ◀──────   BattleId / Seed / EnemyCap / ...
本地跑模拟，每 20 帧（1 秒）：
C2G_SoloBattleInputsRequest     ──────▶   检查帧数 ≤ 真实经过时间×20 + 60（防加速）
  起始帧 + 打包指令 + 末帧哈希              重放这批指令，核对哈希
                                ◀──────   已校验帧号（失败则作废本局）
死亡或按 Esc：补传剩余指令，然后
C2G_SoloBattleEndRequest        ──────▶   核对最终帧和哈希，结果取服务端状态
                                ◀──────   帧数 / 击杀 / 是否阵亡   ← 发奖、排行榜接在服务端这里
```

| 位置 | 内容 |
|---|---|
| `GameBattle/Survivor/BattleVerifier.cs` | 校验核心（共享）：按序吃批次、重放、核对哈希。联机房间复用它 |
| `GameBattle/Survivor/BattleInputCodec.cs` | 指令打包，每帧每人 2 字节，单批上限 200 帧 |
| `GameServer/.../Gate/System/SoloBattleComponentSystem.cs` | 开局、防加速、失败作废 |
| `GameServer/.../Gate/Handler/C2G_SoloBattle*Handler.cs` | 三个请求的处理 |
| `GameLogic/BattleSys/Survivor/SoloBattleMgr.cs` | 客户端流程（数据中心模块，自动注册） |
| `GameLogic/BattleSys/Survivor/SurvivorBattleView.cs` | 表现层，沙盒和联网战斗共用 |

能拦住的作弊（BattleCheck 离线测试 + 实机测试）：篡改上传指令、修改本地内存（如击杀数）、跳批次、超大批次、加速。错误码 5001~5005 见 `ErrorCode.cs`。

断线即作废（组件挂在 Session 上）；断线续战以后在这里补。

以后做联机：服务端把"一个玩家的批次"换成"房间每帧收集所有人的指令并广播"，仍用 `BattleVerifier` 重放；客户端把 `LocalBattleDriver` 换成按广播帧推进的驱动，表现层不变。

## 协议

- 协议源：`GameServer/Tools/NetworkProtocol/Outer/BattleMessage.proto`；导出：在 `GameServer/Tools/ProtocolExportTool` 执行 `dotnet Fantasy.ProtocolExportTool.dll export --silent`，生成文件不要手改。
- 项目自带导出工具是 2025.2.1414 版：**不支持 `bytes`/`byte[]`**，字节数组写成 `repeatedArray byte`。
- 导出会按文件顺序重新编号，新增消息会让后面消息的 opcode 后移；协议改动必须客户端和服务端同时发布。

## 加一个新玩法

1. 在 `Survivor/Systems/` 新建静态类 `XxxSystem.Run(BattleState state, ...)`。
2. 需要新数据就加到 `BattleState`（或对应的 Buffer），**同时在 `StateHasher` 里哈希它**。
3. 在 `BattleSimulation.Tick` 里按顺序调用。
4. 数值放 `BattleConfig`；会影响结果的开局开关放 `BattleSettings`，并在 `BattleReplay` 读写。
5. 跑 `dotnet run -c Release --project GameServer/Tools/BattleCheck`，全部 PASS 再提交。

## 确定性规则（BattleCheck 会扫描拦截）

Survivor 目录里禁止：

- `float` / `double` / `Mathf` / 小数字面量（`0.4f` 会被隐式转成 `FixedPoint64`）。小数写成 `(FixedPoint64)4 / 10`。
- `UnityEngine`、`Time`、`DateTime`、`Stopwatch`、`System.Random`、无参 `new FixedPointRandom()`。随机数只用 `state.Random`。
- 遍历 `Dictionary` / `HashSet`，调用 `GetHashCode()`。
- 多线程、`async`。
- 在模拟外修改 `BattleState`（例如表现层直接改血量）。需要的开关做成 `BattleSettings` 字段。

## 验证

- `BattleCheck`：禁用写法扫描、3000 帧录像往返逐帧哈希一致、哈希对种子/指令敏感、压测。
- 沙盒：WASD 移动，F6 用当前录像重放并对比哈希，F5 把录像存到 `persistentDataPath/Replays/`（文件名带哈希）。
- 跨运行时：`dotnet run -c Release --project GameServer/Tools/BattleCheck -- --replay <录像>`，验证 Unity 录的局在 .NET 下结果一致。

## 性能数据（4 玩家，单帧逻辑耗时，逻辑 20 帧/秒即预算 50ms）

测试机 AMD Ryzen 7 5800X3D（桌面高端 CPU）。场景与 `BattleCheck --bench-only` 相同。

| 运行环境 | 1000 怪 | 2000 怪 | 3000 怪 |
|---|---|---|---|
| .NET 8（服务端） | 0.47ms | 1.08ms | 1.76ms |
| Unity 编辑器 Mono，Release 编译 | — | 4.36ms | — |
| Unity 编辑器 Mono，Debug 编译（默认） | — | 15.3ms | — |
| **Windows 包，IL2CPP + HybridCLR 解释执行（当前）** | **6.26ms** | **15.2ms** | **25.6ms** |

- 三个数量下 IL2CPP 包的哈希与 .NET 完全一致（`5D8917DC…` / `D16ABD42…` / `CE558ED6…`），客户端真机运行和服务端校验结果相同。
- 解释执行比 .NET 慢约 14 倍。2000 怪时每秒逻辑占用约 300ms（单核 30%），这还是桌面高端 CPU；中端手机通常慢 3~5 倍，单帧会超过 50ms 预算。
- 复测：打包后运行 `Release_Windows.exe -battleBench`，结果写入 `%USERPROFILE%/AppData/LocalLow/DefaultCompany/GameUnity/battle_bench.txt`。

## 从上游同步时的坑

作者仓库：`upstream` = https://github.com/AmaniDawn/DGame ，同步 `DGame_Fantasy` 和 `DGame_Unity6+` 两个分支。

- `DGame_Unity6+` 的 "Merge main" 提交会把 `ProjectSettings/ProjectSettings.asset` 第 3 行 `--- !u!129 &1` 拆成两行，Unity 读不了会重置全部设置（宏、GUID 全丢）。合并后检查第 3 行；坏了就用该分支最后一个格式正确的版本修。
- 不要删 `Packages/packages-lock.json`：HybridCLR/Obfuz 是 git 包，删了会被升到最新提交，HybridCLR 9.x 与 DGame 编辑器代码不兼容。
- `DGame_Fantasy` 曾提交过带冲突标记（`<<<<<<<<`）的 `.meta` 文件，合并后用 `git grep -l '^<<<<<<<' -- GameUnity` 检查。
- `.agents/scripts/workflow.py` 在中文 Windows 上要加 `PYTHONUTF8=1`，否则报 GBK 解码错误。
- 服务端依赖本机 MongoDB（`127.0.0.1:27017`）。
- 上游默认关闭 HybridCLR（`HybridCLRSettings.enable: 0`）、Obfuz 构建期混淆开启但运行时宏 `ENABLE_OBFUZ` 未定义，且在 Unity 6.3 下混淆会空引用崩溃。本仓库已开启 HybridCLR（菜单 DGame Tools/HybridCLR/启用HybridCLR）、关闭 Obfuz 构建期混淆（Project Settings → Obfuz → Build Pipeline Settings）。同步上游后检查这两项。
- 打包流程：HybridCLR/Generate/All → DGame Tools/Build/AutoBuildWindow，产物在 `GameUnity/Build/Windows/`。
