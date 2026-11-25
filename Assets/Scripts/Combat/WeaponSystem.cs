using System.Collections.Generic;
using Gunplay.Core;
using Gunplay.Run;
using UnityEngine;

// WeaponSystem.cs —— 武器射击：冷却、弹匣、换弹、散布、暴击与激光束
namespace Gunplay.Combat
{
    /// <summary>武器系统：驱动所有已装备武器的开火、换弹与激光能量</summary>
    public sealed class WeaponSystem
    {
        // 配置库
        private readonly ConfigDatabase config;
        // 本轮状态
        private readonly RunState run;
        // 事件总线
        private readonly EventBus bus;
        // 子弹系统
        private readonly BulletSystem bullets;
        // 敌人系统（激光命中结算）
        private readonly EnemySystem enemies;
        // 词条系统（伤害 / 射速 / 弹丸 / 穿透 / 暴击加成）
        private readonly UpgradeSystem upgrades;
        // 随机源
        private readonly IRandomSource random;
        // 当前帧激光束（表现层读取）
        private readonly List<LaserShot> lasers = new List<LaserShot>(2);
        // 模拟时间（秒）
        private float time;

        /// <summary>当前帧的激光束（只读）</summary>
        public IReadOnlyList<LaserShot> Lasers => lasers;

        /// <summary>构造：注入依赖</summary>
        public WeaponSystem(ConfigDatabase configDatabase, RunState runState, EventBus eventBus,
            BulletSystem bulletSystem, EnemySystem enemySystem, UpgradeSystem upgradeSystem, IRandomSource randomSource)
        {
            config = configDatabase;
            run = runState;
            bus = eventBus;
            bullets = bulletSystem;
            enemies = enemySystem;
            upgrades = upgradeSystem;
            random = randomSource;
        }

        /// <summary>按词条计算弹匣容量</summary>
        public int GetMagazine(WeaponConfigData weapon)
        {
            return Mathf.Max(1, Mathf.RoundToInt(weapon.magazine * upgrades.GetMagazineMultiplier(weapon.id)));
        }

        /// <summary>新开一轮：把所有武器状态重置为满弹匣 / 满能量</summary>
        public void ResetForRun()
        {
            lasers.Clear();
            for (int i = 0; i < run.ownedWeapons.Count; i++)
            {
                ResetWeapon(run.ownedWeapons[i]);
            }
        }

        /// <summary>进入新关卡：全部武器补满（关间免费补给）</summary>
        public void RefillAll()
        {
            for (int i = 0; i < run.ownedWeapons.Count; i++)
            {
                ResetWeapon(run.ownedWeapons[i]);
            }
        }

        /// <summary>每帧推进：对每把已装备武器结算冷却与开火</summary>
        public void Update(float deltaTime, bool aiming, Vector2 muzzle, Vector2 aimDirection, Vector2 aimWorldPos)
        {
            time += deltaTime;
            lasers.Clear();

            if (!aiming)
            {
                for (int i = 0; i < run.equippedWeapons.Count; i++)
                {
                    WeaponRuntime idleState = EnsureState(run.equippedWeapons[i]);
                    WeaponConfigData idleWeapon = config.GetWeapon(run.equippedWeapons[i]);
                    if (idleWeapon.isContinuous)
                    {
                        RegenerateLaserEnergy(idleWeapon, idleState, deltaTime);
                    }
                }

                return;
            }

            for (int i = 0; i < run.equippedWeapons.Count; i++)
            {
                string weaponId = run.equippedWeapons[i];
                WeaponConfigData weapon = config.GetWeapon(weaponId);
                WeaponRuntime state = EnsureState(weaponId);

                if (weapon.isContinuous)
                {
                    UpdateLaser(weapon, state, muzzle, aimDirection, deltaTime);
                    continue;
                }

                UpdateBallisticWeapon(weapon, state, muzzle, aimDirection, aimWorldPos, deltaTime);
            }
        }

        // 常规武器：换弹 → 冷却 → 开火
        private void UpdateBallisticWeapon(WeaponConfigData weapon, WeaponRuntime state,
            Vector2 muzzle, Vector2 aimDirection, Vector2 aimWorldPos, float deltaTime)
        {
            if (state.reloading)
            {
                state.reloadRemaining -= deltaTime;
                if (state.reloadRemaining <= 0f)
                {
                    state.reloading = false;
                    state.ammo = GetMagazine(weapon);
                }

                return;
            }

            if (state.cooldown > 0f)
            {
                state.cooldown -= deltaTime;
            }

            if (state.ammo <= 0)
            {
                StartReload(weapon, state);
                return;
            }

            if (state.cooldown > 0f)
            {
                return;
            }

            Fire(weapon, state, muzzle, aimDirection, aimWorldPos);
        }

        // 开火：按弹丸数逐颗生成子弹，逐颗独立判定暴击
        private void Fire(WeaponConfigData weapon, WeaponRuntime state, Vector2 muzzle, Vector2 aimDirection, Vector2 aimWorldPos)
        {
            float fireRate = Mathf.Max(0.05f, weapon.fireRate * upgrades.GetFireRateMultiplier(weapon.id));
            state.cooldown = 1f / fireRate;
            state.ammo--;

            float damage = weapon.baseDamage * upgrades.GetDamageMultiplier(weapon.id);
            float critChance = Mathf.Clamp01(weapon.critChance + upgrades.GetExtraCritChance(weapon.id));

            if (weapon.isArcThrow)
            {
                float distance = Vector2.Distance(muzzle, aimWorldPos);
                bullets.SpawnArc(weapon, muzzle, aimDirection, damage, distance);
                run.currentLevelStats.shotsFired++;
            }
            else
            {
                int pellets = Mathf.Max(1, weapon.pellets + upgrades.GetExtraPellets(weapon.id));
                int extraPierce = upgrades.GetExtraPierce(weapon.id);
                for (int i = 0; i < pellets; i++)
                {
                    bool crit = random.NextFloat() < critChance;
                    float pelletDamage = crit ? damage * config.Tuning.economy.critMultiplier : damage;
                    bullets.SpawnStraight(weapon, muzzle, aimDirection, pelletDamage, crit, extraPierce, 0f);
                }

                run.currentLevelStats.shotsFired += pellets;
            }

            bus.Publish(new WeaponFiredEvent { WeaponId = weapon.id, Pos = muzzle, Direction = aimDirection });
        }

        // 开始换弹
        private void StartReload(WeaponConfigData weapon, WeaponRuntime state)
        {
            if (state.reloading)
            {
                return;
            }

            state.reloading = true;
            state.reloadRemaining = Mathf.Max(0.05f, weapon.reloadTime);
        }

        // 激光：持续扣能量、命中线段内敌人、能量耗尽进入过热锁定
        private void UpdateLaser(WeaponConfigData weapon, WeaponRuntime state, Vector2 muzzle, Vector2 aimDirection, float deltaTime)
        {
            if (state.overheatRemaining > 0f)
            {
                state.overheatRemaining -= deltaTime;
                RegenerateLaserEnergy(weapon, state, deltaTime);
                if (state.overheatRemaining <= 0f)
                {
                    state.energy = weapon.energyMax;
                }

                return;
            }

            state.energy -= Mathf.Max(0f, weapon.energyDrainPerSecond) * deltaTime;
            if (state.energy <= 0f)
            {
                state.energy = 0f;
                state.overheatRemaining = Mathf.Max(0f, weapon.overheatLockSeconds);
                return;
            }

            const float maxRange = 14f;
            Vector2 perpendicular = new Vector2(-aimDirection.y, aimDirection.x);
            float nearestDistance = maxRange;

            // 先找最近的被命中敌人，光束视觉止于该点
            for (int i = 0; i < enemies.All.Count; i++)
            {
                Enemy enemy = enemies.All[i];
                if (!enemy.Alive)
                {
                    continue;
                }

                Vector2 toEnemy = enemy.Position - muzzle;
                float along = Vector2.Dot(toEnemy, aimDirection);
                if (along <= 0f || along > maxRange)
                {
                    continue;
                }

                float perpendicularDistance = Mathf.Abs(Vector2.Dot(toEnemy, perpendicular));
                if (perpendicularDistance <= enemy.Radius + 0.2f && along < nearestDistance)
                {
                    nearestDistance = along;
                }
            }

            Vector2 beamEnd = muzzle + aimDirection * nearestDistance;
            lasers.Add(new LaserShot
            {
                WeaponId = weapon.id,
                From = muzzle,
                To = beamEnd,
                Width = 0.18f,
                Energy01 = weapon.energyMax <= 0.01f ? 1f : Mathf.Clamp01(state.energy / weapon.energyMax)
            });

            // 光束为范围伤害：线段沿途所有敌人按每秒伤害结算
            float damagePerSecond = Mathf.Max(0f, weapon.damagePerSecond) * upgrades.GetDamageMultiplier(weapon.id);
            if (damagePerSecond <= 0f)
            {
                return;
            }

            for (int i = 0; i < enemies.All.Count; i++)
            {
                Enemy enemy = enemies.All[i];
                if (!enemy.Alive)
                {
                    continue;
                }

                Vector2 toEnemy = enemy.Position - muzzle;
                float along = Vector2.Dot(toEnemy, aimDirection);
                if (along <= 0f || along > nearestDistance + enemy.Radius)
                {
                    continue;
                }

                float perpendicularDistance = Mathf.Abs(Vector2.Dot(toEnemy, perpendicular));
                if (perpendicularDistance > enemy.Radius + 0.25f)
                {
                    continue;
                }

                run.currentLevelStats.shotsHit++;
                enemies.ApplyDamage(enemy, new DamageInfo
                {
                    Amount = damagePerSecond * deltaTime,
                    Type = weapon.damageType,
                    SourcePos = muzzle,
                    Crit = false,
                    WeaponId = weapon.id
                });
            }
        }

        // 未开火时回充激光能量
        private void RegenerateLaserEnergy(WeaponConfigData weapon, WeaponRuntime state, float deltaTime)
        {
            if (weapon.energyMax <= 0f)
            {
                return;
            }

            state.energy = Mathf.Min(weapon.energyMax, state.energy + Mathf.Max(0f, weapon.energyRegenPerSecond) * deltaTime);
        }

        // 重置单把武器状态
        private void ResetWeapon(string weaponId)
        {
            WeaponConfigData weapon = config.GetWeapon(weaponId);
            WeaponRuntime state = EnsureState(weaponId);
            state.ammo = GetMagazine(weapon);
            state.cooldown = 0f;
            state.reloading = false;
            state.reloadRemaining = 0f;
            state.energy = Mathf.Max(0f, weapon.energyMax);
            state.overheatRemaining = 0f;
        }

        // 确保武器运行时状态存在
        private WeaponRuntime EnsureState(string weaponId)
        {
            WeaponRuntime state = run.GetWeaponRuntime(weaponId);
            if (state != null)
            {
                return state;
            }

            WeaponConfigData weapon = config.GetWeapon(weaponId);
            state = new WeaponRuntime
            {
                id = weaponId,
                ammo = GetMagazine(weapon),
                energy = Mathf.Max(0f, weapon.energyMax)
            };
            run.weaponStates.Add(state);
            return state;
        }

        /// <summary>当前模拟时间（秒）</summary>
        public float Now => time;
    }
}
