using System.Collections.Generic;
using Gunplay.Core;
using UnityEngine;

// BulletSystem.cs —— 子弹推进、命中判定、穿透与爆炸
namespace Gunplay.Combat
{
    /// <summary>子弹系统：发射、飞行、碰撞、穿透与爆炸伤害</summary>
    public sealed class BulletSystem
    {
        // 配置库
        private readonly ConfigDatabase config;
        // 本轮状态（统计命中数）
        private readonly RunState run;
        // 事件总线
        private readonly EventBus bus;
        // 敌人系统（命中结算）
        private readonly EnemySystem enemies;
        // 随机源（散布）
        private readonly IRandomSource random;
        // 子弹对象池
        private readonly SimplePool<Bullet> pool;
        // 活跃子弹
        private readonly List<Bullet> active = new List<Bullet>(256);

        /// <summary>全部活跃子弹（只读）</summary>
        public IReadOnlyList<Bullet> All => active;

        /// <summary>活跃子弹数量</summary>
        public int ActiveCount => active.Count;

        /// <summary>构造：注入依赖</summary>
        public BulletSystem(ConfigDatabase configDatabase, RunState runState, EventBus eventBus,
            EnemySystem enemySystem, IRandomSource randomSource)
        {
            config = configDatabase;
            run = runState;
            bus = eventBus;
            enemies = enemySystem;
            random = randomSource;
            pool = new SimplePool<Bullet>(() => new Bullet(), b => b.Reset(), 128);
        }

        /// <summary>发射一发直射弹（含散布与穿透）</summary>
        public Bullet SpawnStraight(WeaponConfigData weapon, Vector2 origin, Vector2 direction,
            float damage, bool crit, int extraPierce, float extraSpreadDegrees)
        {
            if (active.Count >= config.Tuning.combat.maxBullets)
            {
                return null;
            }

            float spread = weapon.spreadDegrees + Mathf.Max(0f, extraSpreadDegrees);
            float radians = MathUtil.RadiansFromDirection(direction) +
                            random.Range(-spread * 0.5f, spread * 0.5f) * Mathf.Deg2Rad;

            Bullet bullet = pool.Get();
            bullet.WeaponId = weapon.id;
            bullet.Origin = origin;
            bullet.Position = origin;
            bullet.Velocity = MathUtil.DirectionFromRadians(radians) * Mathf.Max(1f, weapon.projectileSpeed);
            bullet.Damage = damage;
            bullet.Crit = crit;
            bullet.Type = weapon.damageType;
            bullet.Radius = 0.12f;
            bullet.PierceRemaining = Mathf.Max(0, weapon.pierce + extraPierce);
            bullet.ExplosionRadius = weapon.damageType == DamageType.Explosive ? Mathf.Max(weapon.explosionRadius, BattleField.DefaultExplosionRadius) : 0f;
            bullet.Flight = BulletFlight.Straight;
            bullet.Lifetime = BattleField.BulletMaxLifetime;
            bullet.HitIds.Clear();
            bullet.Alive = true;
            active.Add(bullet);
            return bullet;
        }

        /// <summary>投掷一枚手雷（抛物线至目标点后爆炸）</summary>
        public Bullet SpawnArc(WeaponConfigData weapon, Vector2 origin, Vector2 direction, float damage, float distance)
        {
            if (active.Count >= config.Tuning.combat.maxBullets)
            {
                return null;
            }

            float clamped = Mathf.Clamp(distance, BattleField.MinArcThrowDistance, BattleField.MaxArcThrowDistance);
            Bullet bullet = pool.Get();
            bullet.WeaponId = weapon.id;
            bullet.Origin = origin;
            bullet.Position = origin;
            bullet.ArcTargetPos = origin + direction.normalized * clamped;
            bullet.Velocity = direction.normalized * Mathf.Max(2f, weapon.projectileSpeed);
            bullet.Damage = damage;
            bullet.Crit = false;
            bullet.Type = DamageType.Explosive;
            bullet.Radius = 0.22f;
            bullet.PierceRemaining = 0;
            bullet.ExplosionRadius = Mathf.Max(weapon.explosionRadius, BattleField.DefaultExplosionRadius);
            bullet.Flight = BulletFlight.Arc;
            bullet.Lifetime = BattleField.BulletMaxLifetime;
            bullet.HitIds.Clear();
            bullet.Alive = true;
            active.Add(bullet);
            return bullet;
        }

        /// <summary>每帧推进全部子弹并结算命中</summary>
        public void Update(float deltaTime)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Bullet bullet = active[i];
                if (!bullet.Alive)
                {
                    active.RemoveAt(i);
                    pool.Release(bullet);
                    continue;
                }

                bullet.Lifetime -= deltaTime;
                if (bullet.Flight == BulletFlight.Arc)
                {
                    if (UpdateArcBullet(bullet, deltaTime))
                    {
                        continue; // 已爆炸并回收
                    }
                }
                else
                {
                    UpdateStraightBullet(bullet, deltaTime);
                }

                if (!bullet.Alive)
                {
                    active.RemoveAt(i);
                    pool.Release(bullet);
                }
            }
        }

        /// <summary>清空全部子弹（重开 / 回主菜单）</summary>
        public void ClearAll()
        {
            for (int i = 0; i < active.Count; i++)
            {
                active[i].Alive = false;
                pool.Release(active[i]);
            }

            active.Clear();
        }

        // 直线子弹：移动 + 命中 + 越界回收
        private void UpdateStraightBullet(Bullet bullet, float deltaTime)
        {
            Vector2 previous = bullet.Position;
            bullet.Position += bullet.Velocity * deltaTime;

            // 穿透型子弹用线段扫掠，避免高速穿透目标漏判
            for (int i = 0; i < enemies.All.Count; i++)
            {
                Enemy enemy = enemies.All[i];
                if (!enemy.Alive || bullet.HasHit(enemy.Id))
                {
                    continue;
                }

                if (!MathUtil.CirclesOverlap(bullet.Position, bullet.Radius, enemy.Position, enemy.Radius) &&
                    MathUtil.DistancePointToSegment(enemy.Position, previous, bullet.Position) > enemy.Radius + bullet.Radius)
                {
                    continue;
                }

                bullet.HitIds.Add(enemy.Id);
                run.currentLevelStats.shotsHit++;

                if (bullet.ExplosionRadius > 0f)
                {
                    Explode(bullet.Position, bullet.ExplosionRadius, bullet.Damage, bullet.WeaponId);
                    bullet.Alive = false;
                    return;
                }

                enemies.ApplyDamage(enemy, new DamageInfo
                {
                    Amount = bullet.Damage,
                    Type = bullet.Type,
                    SourcePos = bullet.Origin,
                    Crit = bullet.Crit,
                    WeaponId = bullet.WeaponId
                });

                if (bullet.PierceRemaining > 0)
                {
                    bullet.PierceRemaining--;
                    continue;
                }

                bullet.Alive = false;
                return;
            }

            if (bullet.Lifetime <= 0f || bullet.Position.y > BattleField.BulletDespawnY ||
                Mathf.Abs(bullet.Position.x) > BattleField.HalfWidth + 3f)
            {
                bullet.Alive = false;
            }
        }

        // 手雷：飞向目标点，到达即爆炸；返回 true 表示已回收
        private bool UpdateArcBullet(Bullet bullet, float deltaTime)
        {
            Vector2 toTarget = bullet.ArcTargetPos - bullet.Position;
            float step = Mathf.Max(2f, bullet.Velocity.magnitude) * deltaTime;
            float remaining = toTarget.magnitude;

            if (remaining <= step || bullet.Lifetime <= 0f)
            {
                Explode(bullet.ArcTargetPos, bullet.ExplosionRadius, bullet.Damage, bullet.WeaponId);
                bullet.Alive = false;
                return true;
            }

            Vector2 direction = toTarget / remaining;
            bullet.Position += direction * step;
            // 抛射视觉高度（表现层叠加在 y 上，模拟层不参与判定）
            float progress = 1f - remaining / Mathf.Max(0.001f, Mathf.Max(remaining, remaining + step));
            bullet.VisualArcHeight = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI) * 1.6f;
            return false;
        }

        // 爆炸：范围内敌人按距离衰减结算爆炸伤害
        private void Explode(Vector2 center, float radius, float damage, string weaponId)
        {
            bus.Publish(new ExplosionEvent { Pos = center, Radius = radius });

            for (int i = 0; i < enemies.All.Count; i++)
            {
                Enemy enemy = enemies.All[i];
                if (!enemy.Alive)
                {
                    continue;
                }

                float distance = Vector2.Distance(center, enemy.Position);
                if (distance > radius + enemy.Radius)
                {
                    continue;
                }

                float falloff = radius <= 0.01f ? 1f : Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(distance / radius));
                enemies.ApplyDamage(enemy, new DamageInfo
                {
                    Amount = damage * falloff,
                    Type = DamageType.Explosive,
                    SourcePos = center,
                    Crit = false,
                    WeaponId = weaponId
                });
            }
        }
    }
}
