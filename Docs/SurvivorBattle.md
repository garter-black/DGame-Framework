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

## 性能数据（2026-11-02，本机桌面 CPU，4 玩家，单帧逻辑耗时，预算 50ms）

| 运行环境 | 1000 怪 | 2000 怪 | 3000 怪 |
|---|---|---|---|
| .NET 8（服务端同款） | 0.48ms | 1.17ms | 1.81ms |
| Unity 编辑器 Mono，Release 编译 | — | 4.36ms | — |
| Unity 编辑器 Mono，Debug 编译（默认） | — | 15.3ms | — |
| HybridCLR 解释执行（真机） | 未测 | 未测 | 未测 |

GameBattle 在 HybridCLR 热更列表里，真机上会被解释执行，比上表都慢，移动端还要再乘设备差距。上线前必须在目标手机上实测，再决定战斗核心是否移出热更程序集。编辑器里调性能时把右下角代码优化切到 Release，否则数字偏大 3 倍以上。

## 从上游同步时的坑

作者仓库：`upstream` = https://github.com/AmaniDawn/DGame ，同步 `DGame_Fantasy` 和 `DGame_Unity6+` 两个分支。

- `DGame_Unity6+` 的 "Merge main" 提交会把 `ProjectSettings/ProjectSettings.asset` 第 3 行 `--- !u!129 &1` 拆成两行，Unity 读不了会重置全部设置（宏、GUID 全丢）。合并后检查第 3 行；坏了就用该分支最后一个格式正确的版本修。
- 不要删 `Packages/packages-lock.json`：HybridCLR/Obfuz 是 git 包，删了会被升到最新提交，HybridCLR 9.x 与 DGame 编辑器代码不兼容。
- `DGame_Fantasy` 曾提交过带冲突标记（`<<<<<<<<`）的 `.meta` 文件，合并后用 `git grep -l '^<<<<<<<' -- GameUnity` 检查。
- `.agents/scripts/workflow.py` 在中文 Windows 上要加 `PYTHONUTF8=1`，否则报 GBK 解码错误。
- 服务端依赖本机 MongoDB（`127.0.0.1:27017`）。
