using System.Diagnostics;
using GameBattle;

namespace GameLogic
{
    /// <summary>
    /// 单机驱动：把 Unity 的可变帧时间换算成固定的逻辑帧，喂本地指令给 BattleSimulation，并录像。
    /// 接入帧同步后，只需把"本地指令"换成"服务端广播的每帧指令"，模拟和表现层都不用改。
    /// </summary>
    public sealed class LocalBattleDriver
    {
        private const float TICK_SECONDS = 1f / BattleConfig.TICK_RATE;

        // 卡顿后最多补追的逻辑帧数，避免越卡越追不上。
        private const int MAX_TICKS_PER_UPDATE = 5;

        private readonly BattleInput[] m_inputs;
        private readonly Stopwatch m_stopwatch = new Stopwatch();
        private float m_accumulator;

        public BattleSimulation Simulation { get; }

        public BattleReplay Replay { get; }

        /// <summary>当前渲染时刻在上一逻辑帧和当前逻辑帧之间的位置，0~1，用于插值。</summary>
        public float Alpha { get; private set; }

        public double LastTickMs { get; private set; }

        public double MaxTickMs { get; private set; }

        public LocalBattleDriver(in BattleSettings settings)
        {
            Simulation = new BattleSimulation(settings);
            Replay = new BattleReplay(settings);
            m_inputs = new BattleInput[settings.PlayerCount];
        }

        /// <param name="deltaTime">Unity 帧时间（秒）。</param>
        /// <param name="localInput">本机玩家（0 号位）的指令；其他位置单机下为空指令。</param>
        public void Update(float deltaTime, BattleInput localInput)
        {
            m_accumulator += deltaTime;
            var ticks = 0;

            while (m_accumulator >= TICK_SECONDS && ticks < MAX_TICKS_PER_UPDATE)
            {
                m_inputs[0] = localInput;
                Replay.Record(m_inputs);

                m_stopwatch.Restart();
                Simulation.Tick(m_inputs);
                m_stopwatch.Stop();

                LastTickMs = m_stopwatch.Elapsed.TotalMilliseconds;
                if (LastTickMs > MaxTickMs)
                {
                    MaxTickMs = LastTickMs;
                }

                m_accumulator -= TICK_SECONDS;
                ticks++;
            }

            if (ticks == MAX_TICKS_PER_UPDATE)
            {
                m_accumulator = 0;
            }

            Alpha = m_accumulator / TICK_SECONDS;
        }
    }
}
