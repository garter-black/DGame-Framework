using System;
using DGame.FixedPoint;

namespace GameBattle
{
    /// <summary>
    /// 幸存者战斗模拟：喂一帧所有玩家的指令，推进一个逻辑帧。
    /// 客户端（本地或帧同步）和服务端（校验）都用这个类，同样的 Settings + 同样的指令序列 = 同样的结果。
    /// 新玩法加在 Systems 目录，再在 Tick 里按顺序调用。
    /// </summary>
    public sealed class BattleSimulation
    {
        private readonly SpatialGrid m_grid = new SpatialGrid(BattleConfig.MAX_ENEMIES);

        public BattleState State { get; }

        public BattleSimulation(in BattleSettings settings)
        {
            if (settings.PlayerCount < 1 || settings.PlayerCount > BattleConfig.MAX_PLAYERS)
            {
                throw new ArgumentOutOfRangeException(nameof(settings), $"PlayerCount must be 1..{BattleConfig.MAX_PLAYERS}");
            }

            if (settings.SpawnIntervalFrames < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(settings), "SpawnIntervalFrames must be >= 1");
            }

            State = new BattleState(settings);

            var players = State.Players;
            players.Count = settings.PlayerCount;
            for (var i = 0; i < players.Count; i++)
            {
                // 玩家沿 X 轴间隔 2 排开，居中于原点。
                players.Pos[i] = new FixedPointVector2(i * 2 - (players.Count - 1), 0);
                players.PrevPos[i] = players.Pos[i];
                players.Hp[i] = BattleConfig.PLAYER_MAX_HP;
            }
        }

        /// <summary>推进一个逻辑帧。inputs 下标即玩家槽位，长度至少为 PlayerCount。</summary>
        public void Tick(BattleInput[] inputs)
        {
            if (inputs == null || inputs.Length < State.Players.Count)
            {
                throw new ArgumentException($"need {State.Players.Count} inputs", nameof(inputs));
            }

            State.Frame++;
            SavePrevious();

            if (State.IsOver)
            {
                return;
            }

            PlayerMoveSystem.Run(State, inputs);
            EnemySpawnSystem.Run(State);
            EnemyChaseSystem.Run(State);
            m_grid.Build(State.Enemies);
            EnemySeparationSystem.Run(State, m_grid);
            m_grid.Build(State.Enemies);
            WeaponSystem.Run(State);
            ProjectileSystem.Run(State, m_grid);
            ContactDamageSystem.Run(State, m_grid);
            CleanupSystem.Run(State);
        }

        public ulong ComputeHash() => StateHasher.Compute(State);

        private void SavePrevious()
        {
            Array.Copy(State.Players.Pos, State.Players.PrevPos, State.Players.Count);
            Array.Copy(State.Enemies.Pos, State.Enemies.PrevPos, State.Enemies.Count);
            Array.Copy(State.Projectiles.Pos, State.Projectiles.PrevPos, State.Projectiles.Count);
        }
    }
}
