using Gunplay.Core;
using UnityEngine;

// UpgradeSystem.cs —— 词条加成的统一查询入口（所有系统只从这里取加成）
namespace Gunplay.Run
{
    /// <summary>装备升级词条系统：把词条等级换算成具体倍率/数量</summary>
    public sealed class UpgradeSystem
    {
        // 配置库（取词条上限与每级数值）
        private readonly ConfigDatabase config;
        // 本轮状态（存词条等级）
        private readonly RunState run;

        /// <summary>构造：注入配置与轮状态</summary>
        public UpgradeSystem(ConfigDatabase configDatabase, RunState runState)
        {
            config = configDatabase;
            run = runState;
        }

        /// <summary>取某武器某词条的等级</summary>
        public int GetLevel(string upgradeId, string weaponId) => run.GetUpgradeLevel(upgradeId, weaponId);

        /// <summary>词条是否还能升级（未达上限）</summary>
        public bool CanLevelUp(UpgradeConfigData upgrade, string weaponId)
        {
            if (upgrade == null)
            {
                return false;
            }

            return GetLevel(upgrade.id, weaponId) < Mathf.Max(1, upgrade.maxLevel);
        }

        /// <summary>增加一级词条，返回新等级</summary>
        public int AddLevel(UpgradeConfigData upgrade, string weaponId)
        {
            return run.AddUpgradeLevel(upgrade.id, weaponId, Mathf.Max(1, upgrade.maxLevel));
        }

        /// <summary>伤害倍率（1 + 每级比例 × 等级）</summary>
        public float GetDamageMultiplier(string weaponId)
        {
            UpgradeConfigData cfg = FindConfig(UpgradeKind.Damage);
            return cfg == null ? 1f : 1f + cfg.perLevelValue * GetLevel(cfg.id, weaponId);
        }

        /// <summary>射速倍率（1 + 每级比例 × 等级）</summary>
        public float GetFireRateMultiplier(string weaponId)
        {
            UpgradeConfigData cfg = FindConfig(UpgradeKind.FireRate);
            return cfg == null ? 1f : 1f + cfg.perLevelValue * GetLevel(cfg.id, weaponId);
        }

        /// <summary>额外弹丸数（每级 +N）</summary>
        public int GetExtraPellets(string weaponId)
        {
            UpgradeConfigData cfg = FindConfig(UpgradeKind.Pellets);
            return cfg == null ? 0 : Mathf.RoundToInt(cfg.perLevelValue * GetLevel(cfg.id, weaponId));
        }

        /// <summary>额外穿透数（每级 +N）</summary>
        public int GetExtraPierce(string weaponId)
        {
            UpgradeConfigData cfg = FindConfig(UpgradeKind.Pierce);
            return cfg == null ? 0 : Mathf.RoundToInt(cfg.perLevelValue * GetLevel(cfg.id, weaponId));
        }

        /// <summary>弹匣倍率（1 + 每级比例 × 等级）</summary>
        public float GetMagazineMultiplier(string weaponId)
        {
            UpgradeConfigData cfg = FindConfig(UpgradeKind.Magazine);
            return cfg == null ? 1f : 1f + cfg.perLevelValue * GetLevel(cfg.id, weaponId);
        }

        /// <summary>额外暴击率（每级 +N，直接相加到武器基础暴击率）</summary>
        public float GetExtraCritChance(string weaponId)
        {
            UpgradeConfigData cfg = FindConfig(UpgradeKind.Crit);
            return cfg == null ? 0f : cfg.perLevelValue * GetLevel(cfg.id, weaponId);
        }

        /// <summary>连杀窗口额外时长（秒，全局词条）</summary>
        public float GetComboWindowBonus()
        {
            UpgradeConfigData cfg = FindConfig(UpgradeKind.ComboWindow);
            return cfg == null ? 0f : cfg.perLevelValue * GetLevel(cfg.id, string.Empty);
        }

        /// <summary>按种类查词条配置（配置缺失时返回 null）</summary>
        private UpgradeConfigData FindConfig(UpgradeKind kind)
        {
            for (int i = 0; i < config.Upgrades.Count; i++)
            {
                if (config.Upgrades[i].kind == kind)
                {
                    return config.Upgrades[i];
                }
            }

            return null;
        }
    }
}
