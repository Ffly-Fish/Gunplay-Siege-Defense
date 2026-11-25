using System;
using System.Collections.Generic;
using UnityEngine;

// ConfigDatabase.cs —— 读取 Resources/Config/*.json 的全部数值配置（代码内不硬编码数值）
namespace Gunplay.Core
{
    /// <summary>配置数据库：武器 / 敌人 / 词条 / 全局调参</summary>
    public sealed class ConfigDatabase
    {
        // 按 Id 索引的武器
        private readonly Dictionary<string, WeaponConfigData> weaponMap = new Dictionary<string, WeaponConfigData>();
        // 按 Id 索引的敌人
        private readonly Dictionary<string, EnemyConfigData> enemyMap = new Dictionary<string, EnemyConfigData>();
        // 按 Id 索引的词条
        private readonly Dictionary<string, UpgradeConfigData> upgradeMap = new Dictionary<string, UpgradeConfigData>();
        // 按解锁关卡排序的武器列表
        private readonly List<WeaponConfigData> weaponList = new List<WeaponConfigData>();
        // 按出现波次排序的敌人列表
        private readonly List<EnemyConfigData> enemyList = new List<EnemyConfigData>();
        // 词条列表
        private readonly List<UpgradeConfigData> upgradeList = new List<UpgradeConfigData>();

        /// <summary>全局调参配置</summary>
        public TuningConfigData Tuning { get; private set; }

        /// <summary>全部武器（按解锁关卡升序）</summary>
        public IReadOnlyList<WeaponConfigData> Weapons => weaponList;

        /// <summary>全部敌人（按出现波次升序）</summary>
        public IReadOnlyList<EnemyConfigData> Enemies => enemyList;

        /// <summary>全部词条</summary>
        public IReadOnlyList<UpgradeConfigData> Upgrades => upgradeList;

        /// <summary>取武器配置（不存在时抛异常，便于尽早暴露配置错误）</summary>
        public WeaponConfigData GetWeapon(string id)
        {
            if (!weaponMap.TryGetValue(id, out WeaponConfigData cfg))
            {
                throw new KeyNotFoundException($"[Config] 找不到武器配置: {id}");
            }

            return cfg;
        }

        /// <summary>尝试取武器配置</summary>
        public bool TryGetWeapon(string id, out WeaponConfigData cfg) => weaponMap.TryGetValue(id, out cfg);

        /// <summary>取敌人配置</summary>
        public EnemyConfigData GetEnemy(string id)
        {
            if (!enemyMap.TryGetValue(id, out EnemyConfigData cfg))
            {
                throw new KeyNotFoundException($"[Config] 找不到敌人配置: {id}");
            }

            return cfg;
        }

        /// <summary>取词条配置</summary>
        public UpgradeConfigData GetUpgrade(string id)
        {
            if (!upgradeMap.TryGetValue(id, out UpgradeConfigData cfg))
            {
                throw new KeyNotFoundException($"[Config] 找不到词条配置: {id}");
            }

            return cfg;
        }

        /// <summary>尝试取词条配置</summary>
        public bool TryGetUpgrade(string id, out UpgradeConfigData cfg) => upgradeMap.TryGetValue(id, out cfg);

        /// <summary>从 Resources/Config 加载全部配置（运行时入口）</summary>
        public static ConfigDatabase LoadFromResources()
        {
            TextAsset weaponJson = Resources.Load<TextAsset>("Config/weapons");
            TextAsset enemyJson = Resources.Load<TextAsset>("Config/enemies");
            TextAsset upgradeJson = Resources.Load<TextAsset>("Config/upgrades");
            TextAsset tuningJson = Resources.Load<TextAsset>("Config/tuning");
            if (weaponJson == null || enemyJson == null || upgradeJson == null || tuningJson == null)
            {
                throw new InvalidOperationException("[Config] Resources/Config 下的 JSON 配置缺失");
            }

            return FromJson(weaponJson.text, enemyJson.text, upgradeJson.text, tuningJson.text);
        }

        /// <summary>从 JSON 字符串构建（测试入口）</summary>
        public static ConfigDatabase FromJson(string weaponsJson, string enemiesJson, string upgradesJson, string tuningJson)
        {
            ConfigDatabase db = new ConfigDatabase();
            db.Build(
                JsonUtility.FromJson<WeaponConfigList>(weaponsJson),
                JsonUtility.FromJson<EnemyConfigList>(enemiesJson),
                JsonUtility.FromJson<UpgradeConfigList>(upgradesJson),
                JsonUtility.FromJson<TuningConfigData>(tuningJson));
            return db;
        }

        // 校验并建立索引
        private void Build(WeaponConfigList weapons, EnemyConfigList enemies, UpgradeConfigList upgrades, TuningConfigData tuning)
        {
            if (weapons == null || weapons.weapons == null || weapons.weapons.Length == 0)
            {
                throw new InvalidOperationException("[Config] 武器配置为空");
            }

            if (enemies == null || enemies.enemies == null || enemies.enemies.Length == 0)
            {
                throw new InvalidOperationException("[Config] 敌人配置为空");
            }

            if (upgrades == null || upgrades.upgrades == null || upgrades.upgrades.Length == 0)
            {
                throw new InvalidOperationException("[Config] 词条配置为空");
            }

            Tuning = tuning ?? throw new InvalidOperationException("[Config] tuning 配置为空");
            ValidateTuning(Tuning);

            foreach (WeaponConfigData weapon in weapons.weapons)
            {
                if (string.IsNullOrEmpty(weapon.id))
                {
                    throw new InvalidOperationException("[Config] 武器 id 不能为空");
                }

                if (weaponMap.ContainsKey(weapon.id))
                {
                    throw new InvalidOperationException($"[Config] 武器 id 重复: {weapon.id}");
                }

                weaponMap.Add(weapon.id, weapon);
                weaponList.Add(weapon);
            }

            foreach (EnemyConfigData enemy in enemies.enemies)
            {
                if (string.IsNullOrEmpty(enemy.id))
                {
                    throw new InvalidOperationException("[Config] 敌人 id 不能为空");
                }

                if (enemyMap.ContainsKey(enemy.id))
                {
                    throw new InvalidOperationException($"[Config] 敌人 id 重复: {enemy.id}");
                }

                enemyMap.Add(enemy.id, enemy);
                enemyList.Add(enemy);
            }

            foreach (UpgradeConfigData upgrade in upgrades.upgrades)
            {
                if (string.IsNullOrEmpty(upgrade.id))
                {
                    throw new InvalidOperationException("[Config] 词条 id 不能为空");
                }

                if (upgradeMap.ContainsKey(upgrade.id))
                {
                    throw new InvalidOperationException($"[Config] 词条 id 重复: {upgrade.id}");
                }

                upgradeMap.Add(upgrade.id, upgrade);
                upgradeList.Add(upgrade);
            }

            weaponList.Sort((a, b) => a.unlockLevel.CompareTo(b.unlockLevel));
            enemyList.Sort((a, b) => a.minWave.CompareTo(b.minWave));
        }

        // 调参配置的基本合法性校验
        private static void ValidateTuning(TuningConfigData tuning)
        {
            if (tuning.waves == null || tuning.difficulty == null || tuning.economy == null ||
                tuning.shop == null || tuning.gate == null || tuning.combat == null || tuning.ads == null)
            {
                throw new InvalidOperationException("[Config] tuning 配置分组缺失");
            }

            if (tuning.economy.comboTiers == null || tuning.economy.comboTiers.Length == 0)
            {
                throw new InvalidOperationException("[Config] 连杀档位配置缺失");
            }

            if (tuning.combat.slotUnlockLevels == null || tuning.combat.slotUnlockLevels.Length == 0)
            {
                throw new InvalidOperationException("[Config] 装备槽解锁配置缺失");
            }
        }
    }
}
