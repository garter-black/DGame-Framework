using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using GameBattle;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GameLogic
{
    /// <summary>
    /// 战斗压测：用启动参数 -battleBench 运行打包后的游戏，跑与 BattleCheck 相同的场景，
    /// 结果写到 persistentDataPath/battle_bench.txt 后退出。用来测 HybridCLR 解释执行下的真实耗时。
    /// 示例：Release_Windows.exe -battleBench
    /// </summary>
    public static class BattleBench
    {
        /// <summary>带 -battleBench 启动时跑压测并返回 true（打包后会自动退出），否则什么都不做。</summary>
        public static bool TryRun()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-battleBench") < 0)
            {
                return false;
            }

            var lines = new List<string>
            {
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {SystemInfo.processorType} editor={Application.isEditor} backend={(Application.isEditor ? "Mono" : "IL2CPP")}",
            };

            foreach (var cap in new[] { 1000, 2000, 3000 })
            {
                lines.Add(Run(cap));
            }

            var path = Path.Combine(Application.persistentDataPath, "battle_bench.txt");
            File.AppendAllLines(path, lines);
            foreach (var line in lines)
            {
                Debug.Log($"[BattleBench] {line}");
            }

            Debug.Log($"[BattleBench] written to {path}");
            if (!Application.isEditor)
            {
                Application.Quit();
            }

            return true;
        }

        // 与 GameServer/Tools/BattleCheck 的 Benchmark 相同：4 人绕圈、无敌、每帧刷 200 只直到上限，统计后 600 帧。
        private static string Run(int cap)
        {
            var sim = new BattleSimulation(new BattleSettings
            {
                Seed = 99, PlayerCount = 4, EnemyCap = cap, SpawnPerWave = 200, SpawnIntervalFrames = 1, Invincible = true,
            });
            var inputs = new BattleInput[4];
            var timings = new List<double>();
            var sw = new Stopwatch();

            for (var f = 0; f < 1200; f++)
            {
                for (var i = 0; i < 4; i++)
                {
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
            double sum = 0;
            foreach (var t in timings) sum += t;
            return $"cap={cap} enemies={sim.State.Enemies.Count} kills={sim.State.TotalKills} " +
                   $"avg={sum / timings.Count:F3}ms p99={timings[(int)(timings.Count * 0.99)]:F3}ms max={timings[timings.Count - 1]:F3}ms hash={sim.ComputeHash():X16}";
        }
    }
}
