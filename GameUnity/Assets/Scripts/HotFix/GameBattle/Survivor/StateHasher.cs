using DGame.FixedPoint;

namespace GameBattle
{
    /// <summary>
    /// 战斗状态哈希（FNV-1a 变体）。两端在同一帧算出的值不同，就说明不同步或有人作弊。
    /// 给 BattleState 加新字段时，记得在这里一起哈希。
    /// </summary>
    public static class StateHasher
    {
        private const ulong OFFSET = 14695981039346656037UL;
        private const ulong PRIME = 1099511628211UL;

        public static ulong Compute(BattleState state)
        {
            var h = OFFSET;
            Mix(ref h, state.Frame);
            Mix(ref h, state.IsOver ? 1 : 0);
            Mix(ref h, state.TotalKills);
            Mix(ref h, state.SpawnCooldown);

            var players = state.Players;
            Mix(ref h, players.Count);
            for (var i = 0; i < players.Count; i++)
            {
                Mix(ref h, players.Pos[i]);
                Mix(ref h, players.Hp[i]);
                Mix(ref h, players.FireCooldown[i]);
                Mix(ref h, players.Kills[i]);
            }

            var enemies = state.Enemies;
            Mix(ref h, enemies.Count);
            for (var e = 0; e < enemies.Count; e++)
            {
                Mix(ref h, enemies.Pos[e]);
                Mix(ref h, enemies.Hp[e]);
            }

            var projectiles = state.Projectiles;
            Mix(ref h, projectiles.Count);
            for (var p = 0; p < projectiles.Count; p++)
            {
                Mix(ref h, projectiles.Pos[p]);
                Mix(ref h, projectiles.Velocity[p]);
                Mix(ref h, projectiles.Life[p]);
                Mix(ref h, projectiles.Owner[p]);
            }

            // 随机数内部状态读不到；它一旦漂移，下一波刷怪坐标就会不同，仍会被上面的敌人坐标捕获。
            return h;
        }

        private static void Mix(ref ulong h, long value)
        {
            unchecked
            {
                h = (h ^ (ulong)value) * PRIME;
            }
        }

        private static void Mix(ref ulong h, FixedPointVector2 v)
        {
            Mix(ref h, v.x.RawValue);
            Mix(ref h, v.y.RawValue);
        }
    }
}
