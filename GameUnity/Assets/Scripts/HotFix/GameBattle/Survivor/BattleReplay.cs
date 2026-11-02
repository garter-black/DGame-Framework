using System;
using System.Collections.Generic;
using System.IO;

namespace GameBattle
{
    /// <summary>
    /// 战斗录像 = 开局参数 + 每帧所有玩家的指令。回放结果必须和原局完全一致，
    /// 这也是服务端校验的基础：服务端用同样的数据重放，对比客户端上报的哈希。
    /// </summary>
    public sealed class BattleReplay
    {
        private const int FORMAT_VERSION = 1;

        public BattleSettings Settings;

        public readonly List<BattleInput[]> Frames = new List<BattleInput[]>();

        public BattleReplay(in BattleSettings settings)
        {
            Settings = settings;
        }

        /// <summary>记录一帧指令（会复制，调用方可以复用数组）。</summary>
        public void Record(BattleInput[] inputs)
        {
            var copy = new BattleInput[Settings.PlayerCount];
            Array.Copy(inputs, copy, copy.Length);
            Frames.Add(copy);
        }

        /// <summary>从头重放整局，返回最终状态的模拟。</summary>
        public BattleSimulation Play()
        {
            var sim = new BattleSimulation(Settings);
            foreach (var inputs in Frames)
            {
                sim.Tick(inputs);
            }

            return sim;
        }

        public byte[] ToBytes()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(FORMAT_VERSION);
            writer.Write(Settings.Seed);
            writer.Write(Settings.PlayerCount);
            writer.Write(Settings.EnemyCap);
            writer.Write(Settings.SpawnPerWave);
            writer.Write(Settings.SpawnIntervalFrames);
            writer.Write(Settings.Invincible);
            writer.Write(Frames.Count);

            foreach (var inputs in Frames)
            {
                for (var i = 0; i < Settings.PlayerCount; i++)
                {
                    writer.Write(inputs[i].MoveX);
                    writer.Write(inputs[i].MoveY);
                }
            }

            writer.Flush();
            return stream.ToArray();
        }

        public static BattleReplay FromBytes(byte[] data)
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            var version = reader.ReadInt32();
            if (version != FORMAT_VERSION)
            {
                throw new InvalidDataException($"replay version {version}, expected {FORMAT_VERSION}");
            }

            var replay = new BattleReplay(new BattleSettings
            {
                Seed = reader.ReadInt32(),
                PlayerCount = reader.ReadInt32(),
                EnemyCap = reader.ReadInt32(),
                SpawnPerWave = reader.ReadInt32(),
                SpawnIntervalFrames = reader.ReadInt32(),
                Invincible = reader.ReadBoolean(),
            });

            var playerCount = replay.Settings.PlayerCount;
            if (playerCount < 1 || playerCount > BattleConfig.MAX_PLAYERS)
            {
                throw new InvalidDataException($"bad player count {playerCount}");
            }

            var frameCount = reader.ReadInt32();
            for (var f = 0; f < frameCount; f++)
            {
                var inputs = new BattleInput[playerCount];
                for (var i = 0; i < playerCount; i++)
                {
                    inputs[i] = new BattleInput(reader.ReadSByte(), reader.ReadSByte());
                }

                replay.Frames.Add(inputs);
            }

            return replay;
        }
    }
}
