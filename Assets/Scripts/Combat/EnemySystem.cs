using System.Collections.Generic;
using Gunplay.Core;
using Gunplay.Run;
using UnityEngine;

// EnemySystem.cs —— 敌人生命周期、移动、环绕护盾与伤害结算
namespace Gunplay.Combat
{
    /// <summary>敌人系统：生成、推进、护盾判定、受伤与死亡</summary>
    public sealed class EnemySystem
    {
        // 配置库
        private readonly ConfigDatabase config;
        // 本轮状态
        private readonly RunState run;
        // 事件总线
        private readonly EventBus bus;
        // 连杀系统
        private readonly ComboSystem combo;
        // 经济系统
        private readonly EconomySystem economy;
        // 城墙系统（敌人漏过时扣城门耐久）
        private readonly WallSystem wall;
        // 敌人对象池
        private readonly SimplePool<Enemy> pool;
        // 活跃敌人
        private readonly List<Enemy> active = new List<Enemy>(128);
        // 模拟时间（秒）
        private float time;
        // 下一个实例 Id
        private int nextId = 1;

        /// <summary>全部活跃敌人（只读）</summary>
        public IReadOnlyList<Enemy> All => active;

        /// <summary>活跃敌人数量</summary>
        public int AliveCount => active.Count;

        /// <summary>构造：注入依赖</summary>
        public EnemySystem(ConfigDatabase configDatabase, RunState runState, EventBus eventBus,
            ComboSystem comboSystem, EconomySystem economySystem, WallSystem wallSystem)
        {
            config = configDatabase;
            run = runState;
            bus = eventBus;
            combo = comboSystem;
            economy = economySystem;
            wall = wallSystem;
            pool = new SimplePool<Enemy>(() => new Enemy(), e => e.Reset(), 64);
        }

        /// <summary>当前模拟时间（秒）</summary>
        public float Now => time;

        /// <summary>生成一个敌人</summary>
        public Enemy Spawn(EnemyConfigData cfg, float hpMultiplier, float speedMultiplier, Vector2 position)
        {
            if (active.Count >= config.Tuning.combat.maxEnemies)
            {
                return null;
            }

            Enemy enemy = pool.Get();
            enemy.Id = nextId++;
            enemy.Config = cfg;
            enemy.Position = position;
            enemy.MaxHp = Mathf.Max(1f, cfg.baseHp * hpMultiplier);
            enemy.Hp = enemy.MaxHp;
            enemy.Speed = Mathf.Max(0.05f, cfg.speed * speedMultiplier);
            enemy.Radius = Mathf.Max(0.1f, cfg.radius);
            enemy.Armor = Mathf.Clamp01(cfg.armor);
            enemy.Gold = Mathf.Max(1, cfg.baseGold);
            enemy.GateDamage = Mathf.Max(0f, cfg.gateDamage);
            enemy.Alive = true;
            enemy.IsBoss = cfg.bossPhases > 0;
            enemy.BossPhase = enemy.IsBoss ? 1 : 0;
            enemy.ShieldAngle = 0f;
            enemy.HitFlash = 0f;
            enemy.SpawnTime = time;
            enemy.LastHitTime = -999f;
            enemy.LastHitWeaponId = string.Empty;
            active.Add(enemy);

            bus.Publish(new EnemySpawnedEvent
            {
                Id = enemy.Id,
                Kind = enemy.Kind,
                Pos = enemy.Position,
                MaxHp = enemy.MaxHp,
                IsBoss = enemy.IsBoss
            });
            return enemy;
        }

        /// <summary>每帧推进：护盾旋转、向下移动、到达城门</summary>
        public void Update(float deltaTime)
        {
            time += deltaTime;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                Enemy enemy = active[i];
                if (!enemy.Alive)
                {
                    active.RemoveAt(i);
                    pool.Release(enemy);
                    continue;
                }

                if (enemy.HitFlash > 0f)
                {
                    enemy.HitFlash = Mathf.Max(0f, enemy.HitFlash - deltaTime);
                }

                // 环绕护盾：按配置周期匀速旋转
                if (enemy.Config.hasOrbitShield && enemy.Config.shieldPeriod > 0.01f)
                {
                    enemy.ShieldAngle += 360f / enemy.Config.shieldPeriod * deltaTime;
                    enemy.ShieldAngle = MathUtil.NormalizeDegrees(enemy.ShieldAngle);
                }

                // 向城门（下方）直线推进
                enemy.Position.y -= enemy.Speed * deltaTime;

                // 到达城门受击线：扣城门耐久后消失
                if (enemy.Position.y <= BattleField.GateLineY)
                {
                    wall.ApplyGateDamage(enemy.GateDamage);
                    enemy.Alive = false;
                    active.RemoveAt(i);
                    pool.Release(enemy);
                }
            }
        }

        /// <summary>对敌人结算一次伤害（护盾 → 护甲 → 扣血 → 死亡）</summary>
        public HitOutcome ApplyDamage(Enemy enemy, in DamageInfo info)
        {
            if (enemy == null || !enemy.Alive)
            {
                return HitOutcome.Hit;
            }

            // 盾兵护盾：仅普通弹会被正面挡下；爆炸 / 贯穿无视盾牌方向
            if (enemy.HasShield)
            {
                if (info.Type == DamageType.Normal && enemy.IsShieldFacing(info.SourcePos))
                {
                    bus.Publish(new ShieldBlockedEvent { Id = enemy.Id, Pos = enemy.Position });
                    return HitOutcome.Blocked;
                }

                if (info.Type != DamageType.Normal)
                {
                    bus.Publish(new ShieldBrokenEvent { Id = enemy.Id, Pos = enemy.Position });
                }
            }

            float finalDamage = Mathf.Max(0f, info.Amount) * (1f - Mathf.Clamp01(enemy.Armor));
            enemy.Hp -= finalDamage;
            enemy.HitFlash = 0.1f;
            enemy.LastHitTime = time;
            enemy.LastHitWeaponId = info.WeaponId ?? string.Empty;

            bus.Publish(new EnemyDamagedEvent
            {
                Id = enemy.Id,
                Damage = finalDamage,
                Crit = info.Crit,
                Pos = enemy.Position
            });

            if (enemy.Hp > 0f)
            {
                return HitOutcome.Hit;
            }

            Kill(enemy);
            return HitOutcome.Killed;
        }

        /// <summary>清理全部敌人（重开 / 回主菜单时调用，不结算击杀）</summary>
        public void ClearAll()
        {
            for (int i = 0; i < active.Count; i++)
            {
                active[i].Alive = false;
                pool.Release(active[i]);
            }

            active.Clear();
        }

        /// <summary>按 Id 查找活跃敌人</summary>
        public Enemy FindById(int id)
        {
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i].Id == id)
                {
                    return active[i];
                }
            }

            return null;
        }

        // 击杀结算：连杀、金币、事件
        private void Kill(Enemy enemy)
        {
            enemy.Alive = false;
            enemy.Hp = 0f;
            combo.OnKill();
            economy.AddKillGold(enemy.Gold);
            run.totalKills++;
            run.currentLevelStats.kills++;
            bus.Publish(new EnemyKilledEvent
            {
                Id = enemy.Id,
                Kind = enemy.Kind,
                Pos = enemy.Position,
                Gold = enemy.Gold,
                Combo = run.combo
            });
        }
    }
}
