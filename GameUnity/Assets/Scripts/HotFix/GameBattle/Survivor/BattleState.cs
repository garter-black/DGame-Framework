using DGame.FixedPoint;

namespace GameBattle
{
    /// <summary>
    /// 一局战斗的全部状态。数据按数组存放（SoA），便于大量单位的遍历和哈希；
    /// 表现层只读这里的数据，绝不回写。Prev* 是上一逻辑帧的位置，用于渲染插值。
    /// </summary>
    public sealed class BattleState
    {
        public int Frame;

        public bool IsOver;

        public int TotalKills;

        public int SpawnCooldown;

        public readonly BattleSettings Settings;

        public readonly FixedPointRandom Random;

        public readonly PlayerBuffer Players = new PlayerBuffer(BattleConfig.MAX_PLAYERS);

        public readonly EnemyBuffer Enemies = new EnemyBuffer(BattleConfig.MAX_ENEMIES);

        public readonly ProjectileBuffer Projectiles = new ProjectileBuffer(BattleConfig.MAX_PROJECTILES);

        public BattleState(in BattleSettings settings)
        {
            Settings = settings;
            Random = new FixedPointRandom(settings.Seed);
        }
    }

    public sealed class PlayerBuffer
    {
        public int Count;
        public readonly FixedPointVector2[] Pos;
        public readonly FixedPointVector2[] PrevPos;
        public readonly int[] Hp;
        public readonly int[] FireCooldown;
        public readonly int[] Kills;

        public PlayerBuffer(int capacity)
        {
            Pos = new FixedPointVector2[capacity];
            PrevPos = new FixedPointVector2[capacity];
            Hp = new int[capacity];
            FireCooldown = new int[capacity];
            Kills = new int[capacity];
        }

        public bool IsAlive(int i) => Hp[i] > 0;
    }

    public sealed class EnemyBuffer
    {
        public int Count;
        public readonly FixedPointVector2[] Pos;
        public readonly FixedPointVector2[] PrevPos;
        public readonly int[] Hp;

        public EnemyBuffer(int capacity)
        {
            Pos = new FixedPointVector2[capacity];
            PrevPos = new FixedPointVector2[capacity];
            Hp = new int[capacity];
        }

        /// <summary>交换删除：顺序变化在每台机器上一致，所以仍然确定。</summary>
        public void RemoveAt(int i)
        {
            var last = --Count;
            Pos[i] = Pos[last];
            PrevPos[i] = PrevPos[last];
            Hp[i] = Hp[last];
        }
    }

    public sealed class ProjectileBuffer
    {
        public int Count;
        public readonly FixedPointVector2[] Pos;
        public readonly FixedPointVector2[] PrevPos;
        public readonly FixedPointVector2[] Velocity;
        public readonly int[] Life;
        public readonly int[] Owner;

        public ProjectileBuffer(int capacity)
        {
            Pos = new FixedPointVector2[capacity];
            PrevPos = new FixedPointVector2[capacity];
            Velocity = new FixedPointVector2[capacity];
            Life = new int[capacity];
            Owner = new int[capacity];
        }

        public void RemoveAt(int i)
        {
            var last = --Count;
            Pos[i] = Pos[last];
            PrevPos[i] = PrevPos[last];
            Velocity[i] = Velocity[last];
            Life[i] = Life[last];
            Owner[i] = Owner[last];
        }
    }
}
