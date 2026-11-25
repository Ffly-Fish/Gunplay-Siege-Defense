using System;
using System.Collections.Generic;
using UnityEngine;

// RunState.cs —— 单轮（一次闯关）的运行时状态，整轮结束即清零
namespace Gunplay.Core
{
    /// <summary>单个武器的运行时状态</summary>
    [Serializable]
    public class WeaponRuntime
    {
        /// <summary>武器 Id</summary>
        public string id;
        /// <summary>当前弹匣内弹药</summary>
        public int ammo;
        /// <summary>射击冷却剩余（秒）</summary>
        public float cooldown;
        /// <summary>是否正在换弹</summary>
        public bool reloading;
        /// <summary>换弹剩余时间（秒）</summary>
        public float reloadRemaining;
        /// <summary>激光能量剩余</summary>
        public float energy;
        /// <summary>过热锁定剩余时间</summary>
        public float overheatRemaining;
    }

    /// <summary>单条升级词条的运行时等级</summary>
    [Serializable]
    public class UpgradeRuntime
    {
        /// <summary>武器 Id（空字符串 = 全局词条，如连杀时长）</summary>
        public string weaponId;
        /// <summary>词条配置 Id</summary>
        public string upgradeId;
        /// <summary>当前等级</summary>
        public int level;
    }

    /// <summary>单关统计（结算页展示）</summary>
    [Serializable]
    public class LevelStats
    {
        /// <summary>关卡编号</summary>
        public int level;
        /// <summary>本关获得金币</summary>
        public int goldEarned;
        /// <summary>本关击杀数</summary>
        public int kills;
        /// <summary>本关最高连杀</summary>
        public int bestCombo;
        /// <summary>本关开火次数（子弹数）</summary>
        public int shotsFired;
        /// <summary>本关命中次数</summary>
        public int shotsHit;
        /// <summary>本关耗时（秒）</summary>
        public float duration;

        /// <summary>命中率（0–1，未开火时为 0）</summary>
        public float Accuracy => shotsFired <= 0 ? 0f : Mathf.Clamp01((float)shotsHit / shotsFired);
    }

    /// <summary>单轮状态：关卡、金币、武器、词条、城门耐久（仅轮内有效）</summary>
    public sealed class RunState
    {
        /// <summary>当前关卡（1 起）</summary>
        public int level = 1;
        /// <summary>当前波序号（1 起）</summary>
        public int wave = 1;
        /// <summary>城门当前耐久</summary>
        public float gateHp;
        /// <summary>城门耐久上限</summary>
        public float gateMaxHp;
        /// <summary>本轮金币</summary>
        public int gold;
        /// <summary>当前连杀数</summary>
        public int combo;
        /// <summary>最后一次击杀时间（秒）</summary>
        public float lastKillTime;
        /// <summary>本轮总击杀</summary>
        public int totalKills;
        /// <summary>本轮最高连杀</summary>
        public int bestCombo;
        /// <summary>已拥有的武器 Id</summary>
        public List<string> ownedWeapons = new List<string>();
        /// <summary>已装备的武器 Id（顺序即槽位顺序）</summary>
        public List<string> equippedWeapons = new List<string>();
        /// <summary>各武器运行时状态</summary>
        public List<WeaponRuntime> weaponStates = new List<WeaponRuntime>();
        /// <summary>全部升级词条</summary>
        public List<UpgradeRuntime> upgrades = new List<UpgradeRuntime>();
        /// <summary>城墙加固次数（每次 +reinforceAmount 上限）</summary>
        public int wallReinforceLevel;
        /// <summary>本关已重刷商店次数</summary>
        public int rerollsThisLevel;
        /// <summary>本关统计</summary>
        public LevelStats currentLevelStats = new LevelStats();

        /// <summary>是否拥有指定武器</summary>
        public bool OwnsWeapon(string weaponId) => ownedWeapons.Contains(weaponId);

        /// <summary>是否装备指定武器</summary>
        public bool IsEquipped(string weaponId) => equippedWeapons.Contains(weaponId);

        /// <summary>取武器运行时状态（不存在时返回 null）</summary>
        public WeaponRuntime GetWeaponRuntime(string weaponId)
        {
            for (int i = 0; i < weaponStates.Count; i++)
            {
                if (weaponStates[i].id == weaponId)
                {
                    return weaponStates[i];
                }
            }

            return null;
        }

        /// <summary>取词条等级（weaponId 为空表示全局词条）</summary>
        public int GetUpgradeLevel(string upgradeId, string weaponId)
        {
            for (int i = 0; i < upgrades.Count; i++)
            {
                UpgradeRuntime runtime = upgrades[i];
                if (runtime.upgradeId == upgradeId && runtime.weaponId == weaponId)
                {
                    return runtime.level;
                }
            }

            return 0;
        }

        /// <summary>增加或更新一条词条等级，返回新等级</summary>
        public int AddUpgradeLevel(string upgradeId, string weaponId, int maxLevel)
        {
            for (int i = 0; i < upgrades.Count; i++)
            {
                UpgradeRuntime runtime = upgrades[i];
                if (runtime.upgradeId == upgradeId && runtime.weaponId == weaponId)
                {
                    runtime.level = Mathf.Min(runtime.level + 1, maxLevel);
                    return runtime.level;
                }
            }

            UpgradeRuntime created = new UpgradeRuntime
            {
                upgradeId = upgradeId,
                weaponId = weaponId,
                level = 1
            };
            upgrades.Add(created);
            return created.level;
        }

        /// <summary>解锁（并尝试装备）一把武器</summary>
        public void UnlockWeapon(string weaponId, int magazine, int slots)
        {
            if (!ownedWeapons.Contains(weaponId))
            {
                ownedWeapons.Add(weaponId);
            }

            if (!IsEquipped(weaponId) && equippedWeapons.Count < slots)
            {
                equippedWeapons.Add(weaponId);
            }

            if (GetWeaponRuntime(weaponId) == null)
            {
                weaponStates.Add(new WeaponRuntime { id = weaponId, ammo = magazine, cooldown = 0f });
            }
        }

        /// <summary>开始新一轮：清空全部轮内成长，只保留统计（统计在 SaveManager 中）</summary>
        public void ResetForNewRun(float gateMaxHp, string starterWeaponId, int starterMagazine)
        {
            level = 1;
            wave = 1;
            gold = 0;
            combo = 0;
            lastKillTime = -999f;
            totalKills = 0;
            bestCombo = 0;
            wallReinforceLevel = 0;
            rerollsThisLevel = 0;
            this.gateMaxHp = gateMaxHp;
            gateHp = gateMaxHp;
            ownedWeapons.Clear();
            equippedWeapons.Clear();
            weaponStates.Clear();
            upgrades.Clear();
            currentLevelStats = new LevelStats { level = 1 };

            ownedWeapons.Add(starterWeaponId);
            equippedWeapons.Add(starterWeaponId);
            weaponStates.Add(new WeaponRuntime { id = starterWeaponId, ammo = starterMagazine, cooldown = 0f });
        }

        /// <summary>进入下一关：重置波次、重刷次数与本关统计，城门耐久保留</summary>
        public void StartNextLevel()
        {
            level++;
            wave = 1;
            rerollsThisLevel = 0;
            currentLevelStats = new LevelStats { level = level };
        }
    }
}
