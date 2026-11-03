using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GameBattle;

// 战斗核心检查：任一项失败则退出码非 0。
// 1 禁用写法扫描  2 录像回放确定性  3 哈希对种子和指令敏感  4 压测（--bench-only 只跑这项）
// --replay <文件>：重放 Unity 沙盒保存的录像，对比文件名里记录的哈希（跨运行时确定性）。
var replayIndex = Array.IndexOf(args, "--replay");
if (replayIndex >= 0)
{
    return VerifyReplayFile(args[replayIndex + 1]);
}

var failures = new List<string>();
var benchOnly = args.Contains("--bench-only");

if (!benchOnly)
{
    Check("lint", LintSurvivorSources);
    Check("determinism", CheckDeterminism);
    Check("sensitivity", CheckSensitivity);
    Check("verifier", CheckVerifier);
}

Check("bench", Benchmark);

if (failures.Count > 0)
{
    Console.WriteLine($"FAILED: {string.Join(", ", failures)}");
    return 1;
}

Console.WriteLine("ALL PASSED");
return 0;

int VerifyReplayFile(string path)
{
    var replay = BattleReplay.FromBytes(File.ReadAllBytes(path));
    var hash = replay.Play().ComputeHash();
    var expected = Regex.Match(Path.GetFileNameWithoutExtension(path), "_([0-9A-F]{16})$").Groups[1].Value;
    Console.WriteLine($"frames={replay.Frames.Count} hash={hash:X16} expected={(expected == "" ? "(none)" : expected)}");
    if (expected != "" && expected != hash.ToString("X16"))
    {
        Console.WriteLine("MISMATCH");
        return 1;
    }

    Console.WriteLine("MATCH");
    return 0;
}

void Check(string name, Func<string> run)
{
    try
    {
        var error = run();
        Console.WriteLine(error == null ? $"[PASS] {name}" : $"[FAIL] {name}: {error}");
        if (error != null)
        {
            failures.Add(name);
        }
    }
    catch (Exception e)
    {
        Console.WriteLine($"[FAIL] {name}: {e}");
        failures.Add(name);
    }
}

// 战斗逻辑里出现这些写法会让两端结果不一致，提交前在这里拦住。
string LintSurvivorSources()
{
    var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../GameUnity/Assets/Scripts/HotFix/GameBattle/Survivor"));
    if (!Directory.Exists(dir))
    {
        return $"source dir not found: {dir}";
    }

    var banned = new (string Pattern, string Why)[]
    {
        (@"\bfloat\b|\bdouble\b|\bdecimal\b|\bMathf\b|\bMath\.", "浮点数在不同平台结果可能不同，用 FixedPoint64"),
        (@"\d+\.\d+[fFdDmM]?\b", "浮点字面量会隐式转成 FixedPoint64，用整数相除表示小数"),
        (@"\bUnityEngine\b|\bTime\.", "战斗逻辑不能依赖 Unity"),
        (@"System\.Random|new\s+Random\s*\(|\bDateTime\b|\bStopwatch\b|Environment\.TickCount", "随机数和时间必须来自 BattleState"),
        (@"new\s+FixedPointRandom\s*\(\s*\)", "无参 FixedPointRandom 用系统时间做种子"),
        (@"\bDictionary<|\bHashSet<|\bGetHashCode\s*\(", "遍历顺序和哈希值不保证跨平台一致"),
        (@"\bParallel\.|\bTask\.|\bThread\b|\basync\b", "多线程执行顺序不确定"),
    };

    var problems = new List<string>();
    foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
    {
        var lines = File.ReadAllLines(file);
        for (var n = 0; n < lines.Length; n++)
        {
            var code = Regex.Replace(lines[n], @"//.*$", "");
            foreach (var (pattern, why) in banned)
            {
                if (Regex.IsMatch(code, pattern))
                {
                    problems.Add($"{Path.GetFileName(file)}:{n + 1}: {why} -> {lines[n].Trim()}");
                }
            }
        }
    }

    return problems.Count == 0 ? null : "\n  " + string.Join("\n  ", problems);
}

// 同一份录像：边录边算的每帧哈希 == 序列化→反序列化后重放的每帧哈希。
string CheckDeterminism()
{
    const int frames = 3000;
    var settings = BattleSettings.Default(seed: 12345, playerCount: 3);
    var replay = new BattleReplay(settings);
    var live = new BattleSimulation(settings);
    var liveHashes = new ulong[frames];
    var inputs = new BattleInput[settings.PlayerCount];
    var rng = new Random(7);

    for (var f = 0; f < frames; f++)
    {
        for (var i = 0; i < inputs.Length; i++)
        {
            inputs[i] = new BattleInput(rng.Next(-100, 101), rng.Next(-100, 101));
        }

        replay.Record(inputs);
        live.Tick(inputs);
        liveHashes[f] = live.ComputeHash();
    }

    var restored = BattleReplay.FromBytes(replay.ToBytes());
    var replayed = new BattleSimulation(restored.Settings);
    for (var f = 0; f < restored.Frames.Count; f++)
    {
        replayed.Tick(restored.Frames[f]);
        if (replayed.ComputeHash() != liveHashes[f])
        {
            return $"hash diverged at frame {f + 1}";
        }
    }

    var s = live.State;
    Console.WriteLine($"  {frames} frames, enemies={s.Enemies.Count}, kills={s.TotalKills}, over={s.IsOver}, hash={liveHashes[frames - 1]:X16}");

    if (s.TotalKills == 0 || restored.Frames.Count != frames)
    {
        return "scenario did not exercise combat";
    }

    return replay.Play().ComputeHash() == liveHashes[frames - 1] ? null : "BattleReplay.Play final hash differs";
}

// 换种子或改一帧指令，哈希必须变化，否则哈希漏掉了状态。
string CheckSensitivity()
{
    ulong Run(int seed, int tweakFrame)
    {
        var sim = new BattleSimulation(BattleSettings.Default(seed, 2));
        var inputs = new BattleInput[2];
        for (var f = 0; f < 600; f++)
        {
            inputs[0] = new BattleInput(f == tweakFrame ? -100 : 100, 0);
            inputs[1] = new BattleInput(0, 50);
            sim.Tick(inputs);
        }

        return sim.ComputeHash();
    }

    var baseline = Run(1, -1);
    if (Run(1, -1) != baseline) return "same run produced different hash";
    if (Run(2, -1) == baseline) return "different seed produced same hash";
    if (Run(1, 300) == baseline) return "changing one input produced same hash";
    return null;
}

// 服务端校验：诚实客户端全程通过；改指令、改状态、跳批次、超大批次都被拦下。
string CheckVerifier()
{
    const int batch = BattleConfig.TICK_RATE;
    var settings = BattleSettings.Default(seed: 4242, playerCount: 1);

    // 模拟客户端：本地跑并录像，返回 (录像, 每批末尾的哈希)。cheat 在本地模拟上作弊，录像不变。
    (BattleReplay replay, List<ulong> hashes) PlayClient(int frames, Action<BattleSimulation, int> cheat)
    {
        var sim = new BattleSimulation(settings);
        var replay = new BattleReplay(settings);
        var hashes = new List<ulong>();
        var inputs = new BattleInput[1];
        for (var f = 1; f <= frames; f++)
        {
            inputs[0] = new BattleInput((f / 40) % 2 == 0 ? 100 : -100, (f / 25) % 2 == 0 ? 60 : -60);
            replay.Record(inputs);
            sim.Tick(inputs);
            cheat?.Invoke(sim, f);
            if (f % batch == 0) hashes.Add(sim.ComputeHash());
        }

        return (replay, hashes);
    }

    // 模拟服务端：逐批校验，返回第一个非 Ok 的结果和批次号。
    (VerifyResult result, int batchIndex) Verify(BattleReplay replay, List<ulong> hashes, Func<int, byte[], byte[]> tamper = null, int skipBatch = -1)
    {
        var verifier = new BattleVerifier(settings);
        for (var b = 0; b < hashes.Count; b++)
        {
            if (b == skipBatch) continue;
            var packed = BattleInputCodec.Pack(replay.Frames, b * batch, batch, 1);
            packed = tamper?.Invoke(b, packed) ?? packed;
            var r = verifier.ApplyBatch(b * batch + 1, packed, hashes[b]);
            if (r != VerifyResult.Ok) return (r, b);
        }

        var finalHash = hashes[hashes.Count - 1];
        return (verifier.Finish(hashes.Count * batch, finalHash), hashes.Count);
    }

    var (honestReplay, honestHashes) = PlayClient(1200, null);
    var honest = Verify(honestReplay, honestHashes);
    if (honest.result != VerifyResult.Ok) return $"honest client rejected: {honest.result} at batch {honest.batchIndex}";

    // 上传的指令和本地实际用的不一样（例如篡改移动）。
    var tampered = Verify(honestReplay, honestHashes, (b, p) => { if (b == 10) p[4] = unchecked((byte)(sbyte)-p[4]); return p; });
    if (tampered.result != VerifyResult.HashMismatch) return $"tampered input not caught: {tampered.result}";

    // 修改本地内存：第 300 帧给自己加 50 击杀。
    var (cheatReplay, cheatHashes) = PlayClient(1200, (sim, f) => { if (f == 300) sim.State.TotalKills += 50; });
    var memoryCheat = Verify(cheatReplay, cheatHashes);
    if (memoryCheat.result != VerifyResult.HashMismatch || memoryCheat.batchIndex != 14) return $"memory cheat not caught at batch 14: {memoryCheat.result}@{memoryCheat.batchIndex}";

    var skipped = Verify(honestReplay, honestHashes, skipBatch: 3);
    if (skipped.result != VerifyResult.NotSequential) return $"skipped batch not caught: {skipped.result}";

    var oversize = new BattleVerifier(settings).ApplyBatch(1, new byte[(BattleInputCodec.MAX_FRAMES_PER_BATCH + 1) * 2], 0);
    if (oversize != VerifyResult.InvalidInput) return $"oversize batch not caught: {oversize}";

    var wrongFinal = new BattleVerifier(settings).Finish(5, 0);
    if (wrongFinal != VerifyResult.InvalidInput) return $"wrong final frame not caught: {wrongFinal}";

    Console.WriteLine($"  honest {honestHashes.Count} batches OK; tampered input, memory cheat, skipped/oversize batch all rejected");
    return null;
}

// 压测：刷满敌人后统计每帧逻辑耗时。玩家无敌，保证模拟一直在跑。
string Benchmark()
{
    foreach (var cap in new[] { 1000, 2000, 3000 })
    {
        var settings = new BattleSettings
        {
            Seed = 99, PlayerCount = 4, EnemyCap = cap, SpawnPerWave = 200, SpawnIntervalFrames = 1, Invincible = true,
        };
        var sim = new BattleSimulation(settings);
        var inputs = new BattleInput[4];
        var timings = new List<double>();
        var sw = new Stopwatch();

        for (var f = 0; f < 1200; f++)
        {
            for (var i = 0; i < 4; i++)
            {
                // 每个玩家绕圈走，敌人会不停追击、挤压。
                var phase = (f + i * 30) % 120;
                inputs[i] = new BattleInput(phase < 60 ? 100 : -100, phase % 60 < 30 ? 100 : -100);
            }

            sw.Restart();
            sim.Tick(inputs);
            sw.Stop();

            if (f >= 600)
            {
                timings.Add(sw.Elapsed.TotalMilliseconds);
            }
        }

        timings.Sort();
        Console.WriteLine($"  cap={cap,4} enemies={sim.State.Enemies.Count,4} kills={sim.State.TotalKills,5}  " +
                          $"avg={timings.Average():F3}ms p99={timings[(int)(timings.Count * 0.99)]:F3}ms max={timings[^1]:F3}ms hash={sim.ComputeHash():X16}");
    }

    return null;
}
