# BattleCheck

幸存者战斗核心（`GameUnity/Assets/Scripts/HotFix/GameBattle/Survivor`）的自动检查。只编译 FixedPoint 数学库和 Survivor 目录，所以它通过即说明战斗核心可以在服务端编译和运行。

```powershell
# 全部检查：禁用写法扫描、录像回放确定性、哈希敏感性、压测
dotnet run -c Release --project GameServer/Tools/BattleCheck

# 只跑压测
dotnet run -c Release --project GameServer/Tools/BattleCheck -- --bench-only

# 重放 Unity 沙盒按 F5 保存的录像，对比文件名里的哈希（验证 Unity 和 .NET 结果一致）
dotnet run -c Release --project GameServer/Tools/BattleCheck -- --replay <录像文件路径>
```

改了战斗逻辑后先跑一遍；退出码非 0 即失败。
