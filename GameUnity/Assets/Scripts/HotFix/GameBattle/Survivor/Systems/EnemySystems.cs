using DGame.FixedPoint;

namespace GameBattle
{
    /// <summary>按波次在随机一名存活玩家周围的圆环上刷怪。</summary>
    public static class EnemySpawnSystem
    {
        public static void Run(BattleState state)
        {
            if (--state.SpawnCooldown > 0)
            {
                return;
            }

            var settings = state.Settings;
            state.SpawnCooldown = settings.SpawnIntervalFrames;

            var players = state.Players;
            var enemies = state.Enemies;
            var cap = settings.EnemyCap < BattleConfig.MAX_ENEMIES ? settings.EnemyCap : BattleConfig.MAX_ENEMIES;

            for (var n = 0; n < settings.SpawnPerWave && enemies.Count < cap; n++)
            {
                var target = PickAlivePlayer(state);
                if (target < 0)
                {
                    return;
                }

                var angle = state.Random.Next(FixedPoint64.Zero, FixedPoint64.Pi * 2);
                var offset = new FixedPointVector2(FixedPoint64.Cos(angle), FixedPoint64.Sin(angle)) * BattleConfig.SpawnRadius;

                var e = enemies.Count++;
                enemies.Pos[e] = players.Pos[target] + offset;
                enemies.PrevPos[e] = enemies.Pos[e];
                enemies.Hp[e] = BattleConfig.ENEMY_HP;
            }
        }

        private static int PickAlivePlayer(BattleState state)
        {
            var players = state.Players;
            var start = state.Random.Next(0, players.Count);

            for (var k = 0; k < players.Count; k++)
            {
                var i = (start + k) % players.Count;
                if (players.IsAlive(i))
                {
                    return i;
                }
            }

            return -1;
        }
    }

    /// <summary>敌人追向最近的存活玩家。</summary>
    public static class EnemyChaseSystem
    {
        public static void Run(BattleState state)
        {
            var players = state.Players;
            var enemies = state.Enemies;
            var step = BattleConfig.EnemySpeed * BattleConfig.DeltaTime;

            for (var e = 0; e < enemies.Count; e++)
            {
                var bestDistanceSquared = FixedPoint64.MaxValue;
                var delta = FixedPointVector2.zero;

                for (var i = 0; i < players.Count; i++)
                {
                    if (!players.IsAlive(i))
                    {
                        continue;
                    }

                    var d = players.Pos[i] - enemies.Pos[e];
                    var distanceSquared = d.LengthSquared();
                    if (distanceSquared < bestDistanceSquared)
                    {
                        bestDistanceSquared = distanceSquared;
                        delta = d;
                    }
                }

                // 已贴脸（或没有存活玩家）就不再移动，避免除以 0。
                if (bestDistanceSquared > step * step && bestDistanceSquared != FixedPoint64.MaxValue)
                {
                    enemies.Pos[e] += delta / FixedPoint64.Sqrt(bestDistanceSquared) * step;
                }
            }
        }
    }

    /// <summary>敌人互相推开，避免叠成一个点。只用平方距离，不开方。</summary>
    public static class EnemySeparationSystem
    {
        private static readonly FixedPoint64 PushStrength = FixedPoint64.Half;

        public static void Run(BattleState state, SpatialGrid grid)
        {
            var enemies = state.Enemies;
            var minDistance = BattleConfig.EnemyRadius * 2;
            var minDistanceSquared = minDistance * minDistance;
            var nudge = FixedPoint64.FromRaw(1L << 20);

            for (var e = 0; e < enemies.Count; e++)
            {
                var pos = enemies.Pos[e];
                var cx = SpatialGrid.Cell(pos.x);
                var cy = SpatialGrid.Cell(pos.y);
                var push = FixedPointVector2.zero;

                for (var dx = -1; dx <= 1; dx++)
                for (var dy = -1; dy <= 1; dy++)
                for (var o = grid.First(cx + dx, cy + dy); o >= 0; o = grid.Next(o))
                {
                    if (o == e)
                    {
                        continue;
                    }

                    var d = pos - enemies.Pos[o];
                    var distanceSquared = d.LengthSquared();
                    if (distanceSquared >= minDistanceSquared)
                    {
                        continue;
                    }

                    if (distanceSquared == FixedPoint64.Zero)
                    {
                        // 完全重叠时按下标决定方向，保证两边结果一致。
                        push.x += e < o ? nudge : -nudge;
                        continue;
                    }

                    push += d * ((minDistanceSquared - distanceSquared) / minDistanceSquared);
                }

                enemies.Pos[e] = pos + push * PushStrength;
            }
        }
    }

    /// <summary>子弹飞行、命中和寿命。</summary>
    public static class ProjectileSystem
    {
        public static void Run(BattleState state, SpatialGrid grid)
        {
            var projectiles = state.Projectiles;
            var enemies = state.Enemies;
            var radius = BattleConfig.EnemyRadius + BattleConfig.ProjectileRadius;
            var radiusSquared = radius * radius;

            for (var p = 0; p < projectiles.Count; p++)
            {
                projectiles.Pos[p] += projectiles.Velocity[p];
                projectiles.Life[p]--;

                var pos = projectiles.Pos[p];
                var cx = SpatialGrid.Cell(pos.x);
                var cy = SpatialGrid.Cell(pos.y);
                var hit = -1;

                for (var dx = -1; dx <= 1 && hit < 0; dx++)
                for (var dy = -1; dy <= 1 && hit < 0; dy++)
                for (var e = grid.First(cx + dx, cy + dy); e >= 0; e = grid.Next(e))
                {
                    if (enemies.Hp[e] > 0 && FixedPointVector2.DistanceSquared(enemies.Pos[e], pos) < radiusSquared)
                    {
                        hit = e;
                        break;
                    }
                }

                if (hit < 0)
                {
                    continue;
                }

                projectiles.Life[p] = 0;
                enemies.Hp[hit] -= BattleConfig.PROJECTILE_DAMAGE;
                if (enemies.Hp[hit] <= 0)
                {
                    state.Players.Kills[projectiles.Owner[p]]++;
                    state.TotalKills++;
                }
            }
        }
    }

    /// <summary>帧末统一移除死亡敌人和过期子弹（遍历中删除会打乱网格下标）。</summary>
    public static class CleanupSystem
    {
        public static void Run(BattleState state)
        {
            var enemies = state.Enemies;
            for (var e = enemies.Count - 1; e >= 0; e--)
            {
                if (enemies.Hp[e] <= 0)
                {
                    enemies.RemoveAt(e);
                }
            }

            var projectiles = state.Projectiles;
            for (var p = projectiles.Count - 1; p >= 0; p--)
            {
                if (projectiles.Life[p] <= 0)
                {
                    projectiles.RemoveAt(p);
                }
            }
        }
    }
}
