using DGame.FixedPoint;

namespace GameBattle
{
    /// <summary>按指令移动玩家。</summary>
    public static class PlayerMoveSystem
    {
        public static void Run(BattleState state, BattleInput[] inputs)
        {
            var players = state.Players;
            var step = BattleConfig.PlayerSpeed * BattleConfig.DeltaTime;

            for (var i = 0; i < players.Count; i++)
            {
                if (!players.IsAlive(i))
                {
                    continue;
                }

                var dir = new FixedPointVector2(
                    (FixedPoint64)(int)inputs[i].MoveX / BattleConfig.INPUT_SCALE,
                    (FixedPoint64)(int)inputs[i].MoveY / BattleConfig.INPUT_SCALE);

                // 斜向输入长度会超过 1，归一化避免斜走更快。
                var lengthSquared = dir.LengthSquared();
                if (lengthSquared > FixedPoint64.One)
                {
                    dir /= FixedPoint64.Sqrt(lengthSquared);
                }

                players.Pos[i] += dir * step;
            }
        }
    }

    /// <summary>自动攻击：冷却好了就向最近的敌人发射子弹。</summary>
    public static class WeaponSystem
    {
        public static void Run(BattleState state)
        {
            var players = state.Players;
            var enemies = state.Enemies;
            var projectiles = state.Projectiles;

            for (var i = 0; i < players.Count; i++)
            {
                if (!players.IsAlive(i) || --players.FireCooldown[i] > 0)
                {
                    continue;
                }

                var target = FindNearestEnemy(enemies, players.Pos[i], out var distanceSquared);
                if (target < 0 || projectiles.Count >= BattleConfig.MAX_PROJECTILES || distanceSquared == FixedPoint64.Zero)
                {
                    continue;
                }

                players.FireCooldown[i] = BattleConfig.WEAPON_COOLDOWN_FRAMES;

                var dir = (enemies.Pos[target] - players.Pos[i]) / FixedPoint64.Sqrt(distanceSquared);
                var p = projectiles.Count++;
                projectiles.Pos[p] = players.Pos[i];
                projectiles.PrevPos[p] = players.Pos[i];
                projectiles.Velocity[p] = dir * (BattleConfig.ProjectileSpeed * BattleConfig.DeltaTime);
                projectiles.Life[p] = BattleConfig.PROJECTILE_LIFE_FRAMES;
                projectiles.Owner[p] = i;
            }
        }

        // shortcut: 全量遍历找最近敌人，每个玩家每 WEAPON_COOLDOWN_FRAMES 帧一次；武器多了改用网格按圈搜索。
        private static int FindNearestEnemy(EnemyBuffer enemies, FixedPointVector2 from, out FixedPoint64 bestDistanceSquared)
        {
            var best = -1;
            bestDistanceSquared = FixedPoint64.MaxValue;

            for (var e = 0; e < enemies.Count; e++)
            {
                if (enemies.Hp[e] <= 0)
                {
                    continue;
                }

                var d = FixedPointVector2.DistanceSquared(enemies.Pos[e], from);
                if (d < bestDistanceSquared)
                {
                    bestDistanceSquared = d;
                    best = e;
                }
            }

            return best;
        }
    }

    /// <summary>敌人接触伤害，每秒结算一次。全员阵亡则战斗结束。</summary>
    public static class ContactDamageSystem
    {
        public static void Run(BattleState state, SpatialGrid grid)
        {
            if (state.Frame % BattleConfig.TICK_RATE != 0 || state.Settings.Invincible)
            {
                return;
            }

            var players = state.Players;
            var enemies = state.Enemies;
            var radius = BattleConfig.PlayerRadius + BattleConfig.EnemyRadius;
            var radiusSquared = radius * radius;
            var anyAlive = false;

            for (var i = 0; i < players.Count; i++)
            {
                if (!players.IsAlive(i))
                {
                    continue;
                }

                var cx = SpatialGrid.Cell(players.Pos[i].x);
                var cy = SpatialGrid.Cell(players.Pos[i].y);
                var touching = 0;

                for (var dx = -1; dx <= 1; dx++)
                for (var dy = -1; dy <= 1; dy++)
                for (var e = grid.First(cx + dx, cy + dy); e >= 0; e = grid.Next(e))
                {
                    if (enemies.Hp[e] > 0 && FixedPointVector2.DistanceSquared(enemies.Pos[e], players.Pos[i]) < radiusSquared)
                    {
                        touching++;
                    }
                }

                players.Hp[i] -= touching * BattleConfig.ENEMY_CONTACT_DAMAGE;
                if (players.Hp[i] < 0)
                {
                    players.Hp[i] = 0;
                }

                anyAlive |= players.IsAlive(i);
            }

            state.IsOver = !anyAlive;
        }
    }
}
