using DGame.FixedPoint;

namespace GameBattle
{
    /// <summary>
    /// 幸存者战斗的固定参数。
    /// Survivor 目录下的逻辑前后端共用，必须确定性：只用定点数和整数，禁止 float/double、UnityEngine、
    /// System.Random、DateTime、Dictionary/HashSet 遍历。BattleCheck 会扫描本目录拦截这些写法。
    /// </summary>
    public static class BattleConfig
    {
        /// <summary>逻辑帧率。</summary>
        public const int TICK_RATE = 20;

        public const int MAX_PLAYERS = 4;

        public const int MAX_ENEMIES = 4096;

        public const int MAX_PROJECTILES = 1024;

        public static readonly FixedPoint64 DeltaTime = FixedPoint64.One / TICK_RATE;

        /// <summary>摇杆输入的满量程，BattleInput.MoveX/MoveY 取值 [-INPUT_SCALE, INPUT_SCALE]。</summary>
        public const int INPUT_SCALE = 100;

        public static readonly FixedPoint64 PlayerSpeed = 5;

        public static readonly FixedPoint64 PlayerRadius = FixedPoint64.Half;

        public const int PLAYER_MAX_HP = 100;

        public static readonly FixedPoint64 EnemySpeed = 2;

        public static readonly FixedPoint64 EnemyRadius = (FixedPoint64)4 / 10;

        public const int ENEMY_HP = 3;

        /// <summary>每个接触的敌人每秒造成的伤害。</summary>
        public const int ENEMY_CONTACT_DAMAGE = 1;

        /// <summary>敌人在玩家周围这个半径的圆环上刷出。</summary>
        public static readonly FixedPoint64 SpawnRadius = 14;

        public const int WEAPON_COOLDOWN_FRAMES = 6;

        public const int PROJECTILE_DAMAGE = 1;

        public const int PROJECTILE_LIFE_FRAMES = 30;

        public static readonly FixedPoint64 ProjectileSpeed = 14;

        public static readonly FixedPoint64 ProjectileRadius = (FixedPoint64)2 / 10;
    }

    /// <summary>
    /// 一局战斗的开局参数。客户端和服务端用同一份参数创建模拟，才能得到同样的结果。
    /// </summary>
    public struct BattleSettings
    {
        /// <summary>随机种子，由服务端下发。</summary>
        public int Seed;

        public int PlayerCount;

        /// <summary>场上敌人数量上限（压测时调大）。</summary>
        public int EnemyCap;

        /// <summary>每波刷怪数量。</summary>
        public int SpawnPerWave;

        /// <summary>两波刷怪之间的帧数。</summary>
        public int SpawnIntervalFrames;

        /// <summary>玩家不受伤（压测用）。属于开局参数，会写进录像。</summary>
        public bool Invincible;

        public static BattleSettings Default(int seed, int playerCount) => new BattleSettings
        {
            Seed = seed,
            PlayerCount = playerCount,
            EnemyCap = 300,
            SpawnPerWave = 8,
            SpawnIntervalFrames = BattleConfig.TICK_RATE,
        };
    }
}
