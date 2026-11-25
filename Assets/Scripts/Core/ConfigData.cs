using System;
using UnityEngine;

// ConfigData.cs —— 配置数据结构（由 Resources/Config/*.json 反序列化，代码内不硬编码数值）
namespace Gunplay.Core
{
    /// <summary>单把武器配置</summary>
    [Serializable]
    public class WeaponConfigData
    {
        /// <summary>唯一 Id</summary>
        public string id;
        /// <summary>中文显示名</summary>
        public string displayName;
        /// <summary>单发基础伤害</summary>
        public float baseDamage;
        /// <summary>每秒射击次数</summary>
        public float fireRate;
        /// <summary>弹匣容量</summary>
        public int magazine;
        /// <summary>换弹耗时（秒）</summary>
        public float reloadTime;
        /// <summary>单次发射的弹丸数</summary>
        public int pellets;
        /// <summary>散布角度（度，0 = 精准）</summary>
        public float spreadDegrees;
        /// <summary>可穿透敌人数</summary>
        public int pierce;
        /// <summary>伤害类型</summary>
        public DamageType damageType;
        /// <summary>解锁价格</summary>
        public int unlockPrice;
        /// <summary>投放关卡（达到该关才可能刷出）</summary>
        public int unlockLevel;
        /// <summary>基础暴击率（0–1）</summary>
        public float critChance;
        /// <summary>弹丸速度（世界单位/秒）</summary>
        public float projectileSpeed;
        /// <summary>是否抛物线投掷（手雷）</summary>
        public bool isArcThrow;
        /// <summary>爆炸半径（世界单位）</summary>
        public float explosionRadius;
        /// <summary>同屏最多存在几发（手雷）</summary>
        public int maxInFlight;
        /// <summary>是否持续光束（激光枪）</summary>
        public bool isContinuous;
        /// <summary>持续光束每秒伤害</summary>
        public float damagePerSecond;
        /// <summary>能量上限（激光）</summary>
        public float energyMax;
        /// <summary>每秒能量消耗</summary>
        public float energyDrainPerSecond;
        /// <summary>每秒能量回复</summary>
        public float energyRegenPerSecond;
        /// <summary>过热锁定时间（秒）</summary>
        public float overheatLockSeconds;
        /// <summary>武器定位说明（用于商店文案）</summary>
        public string role;
    }

    /// <summary>武器配置集合（JSON 根对象）</summary>
    [Serializable]
    public class WeaponConfigList
    {
        /// <summary>全部武器</summary>
        public WeaponConfigData[] weapons;
    }

    /// <summary>单个敌人配置</summary>
    [Serializable]
    public class EnemyConfigData
    {
        /// <summary>唯一 Id</summary>
        public string id;
        /// <summary>敌人种类</summary>
        public EnemyKind kind;
        /// <summary>中文显示名</summary>
        public string displayName;
        /// <summary>基础血量</summary>
        public float baseHp;
        /// <summary>移动速度（世界单位/秒）</summary>
        public float speed;
        /// <summary>护甲减伤比例（0.2 = 减伤 20%）</summary>
        public float armor;
        /// <summary>击杀基础金币</summary>
        public int baseGold;
        /// <summary>到达城门时对城门造成的伤害</summary>
        public float gateDamage;
        /// <summary>是否拥有环绕护盾（盾兵）</summary>
        public bool hasOrbitShield;
        /// <summary>护盾环绕周期（秒）</summary>
        public float shieldPeriod;
        /// <summary>护盾正面扇区半角（度）</summary>
        public float shieldFrontArc;
        /// <summary>碰撞半径（世界单位）</summary>
        public float radius;
        /// <summary>初始出现波次（1 起）</summary>
        public int minWave;
        /// <summary>波次权重基数</summary>
        public float weightBase;
        /// <summary>每波权重增长量</summary>
        public float weightGrowth;
        /// <summary>Boss 阶段数（0 = 非 Boss）</summary>
        public int bossPhases;
    }

    /// <summary>敌人配置集合（JSON 根对象）</summary>
    [Serializable]
    public class EnemyConfigList
    {
        /// <summary>全部敌人</summary>
        public EnemyConfigData[] enemies;
    }

    /// <summary>单个升级词条配置</summary>
    [Serializable]
    public class UpgradeConfigData
    {
        /// <summary>唯一 Id</summary>
        public string id;
        /// <summary>词条种类</summary>
        public UpgradeKind kind;
        /// <summary>中文显示名</summary>
        public string displayName;
        /// <summary>基础价格</summary>
        public int basePrice;
        /// <summary>等级上限</summary>
        public int maxLevel;
        /// <summary>每级效果数值（伤害 / 射速为比例，弹丸 / 穿透为个数，连杀为秒）</summary>
        public float perLevelValue;
        /// <summary>适用武器 Id（空数组 = 全部武器）</summary>
        public string[] applicableWeapons;
    }

    /// <summary>升级词条配置集合（JSON 根对象）</summary>
    [Serializable]
    public class UpgradeConfigList
    {
        /// <summary>全部词条</summary>
        public UpgradeConfigData[] upgrades;
    }

    /// <summary>连杀倍率档位</summary>
    [Serializable]
    public class ComboTierData
    {
        /// <summary>达成该档位所需最小连杀数</summary>
        public int minCombo;
        /// <summary>金币倍率</summary>
        public float multiplier;
        /// <summary>屏幕反馈强度（0–4，表现层使用）</summary>
        public int feedbackLevel;
    }

    /// <summary>波次配置</summary>
    [Serializable]
    public class WaveConfigData
    {
        /// <summary>每关波数</summary>
        public int wavesPerLevel;
        /// <summary>波内敌人数量基数（count = baseCount + perWaveCount × 波序号 + levelDivisor 项）</summary>
        public int baseCount;
        /// <summary>每波递增敌人数量</summary>
        public int perWaveCount;
        /// <summary>每多少关额外 +1 敌人（floor(L / levelCountDivisor)）</summary>
        public int levelCountDivisor;
        /// <summary>波内刷怪间隔下限（秒）</summary>
        public float spawnIntervalMin;
        /// <summary>波内刷怪间隔上限（秒）</summary>
        public float spawnIntervalMax;
        /// <summary>波间空档（秒）</summary>
        public float waveGap;
        /// <summary>多少关一个 Boss 关（0 = 无）</summary>
        public int bossEveryLevels;
        /// <summary>Boss 波在前面的普通波数量（0 = 直接 Boss 波）</summary>
        public int bossWaveIndex;
        /// <summary>敌人出生 X 范围（世界单位）</summary>
        public float spawnXRange;
    }

    /// <summary>难度配置</summary>
    [Serializable]
    public class DifficultyConfigData
    {
        /// <summary>基础血量系数：1 + baseHpPerLevel × (L-1)</summary>
        public float baseHpPerLevel;
        /// <summary>额外难度系数：extraHpPow ^ (L-1)</summary>
        public float extraHpPow;
        /// <summary>速度系数：1 + speedPerLevel × (L-1)</summary>
        public float speedPerLevel;
        /// <summary>血量上限钳制</summary>
        public float hpCap;
    }

    /// <summary>经济配置</summary>
    [Serializable]
    public class EconomyConfigData
    {
        /// <summary>连杀窗口（秒）</summary>
        public float comboWindow;
        /// <summary>清波奖励：每波序号 × 该值</summary>
        public int waveClearGoldPerWave;
        /// <summary>通关奖励基数</summary>
        public int levelClearGoldBase;
        /// <summary>通关奖励每关增量</summary>
        public int levelClearGoldPerLevel;
        /// <summary>暴击伤害倍率</summary>
        public float critMultiplier;
        /// <summary>聚焦火力伤害倍率（0.2 秒内多武器命中同目标）</summary>
        public float focusFireMultiplier;
        /// <summary>聚焦火力判定窗口（秒）</summary>
        public float focusFireWindow;
        /// <summary>连杀倍率档位</summary>
        public ComboTierData[] comboTiers;
        /// <summary>连杀窗口每级增量（秒）</summary>
        public float comboWindowPerLevel;
    }

    /// <summary>商店配置</summary>
    [Serializable]
    public class ShopConfigData
    {
        /// <summary>每次刷新的奖励位数量</summary>
        public int offerCount;
        /// <summary>武器类权重</summary>
        public int weightWeapon;
        /// <summary>城墙类权重</summary>
        public int weightWall;
        /// <summary>词条类权重</summary>
        public int weightUpgrade;
        /// <summary>重刷基础价格</summary>
        public int rerollBaseCost;
        /// <summary>每次重刷价格增量</summary>
        public int rerollCostStep;
        /// <summary>单关最大重刷次数</summary>
        public int maxRerolls;
        /// <summary>城墙加固价格下限</summary>
        public int wallPriceMin;
        /// <summary>城墙加固价格上限</summary>
        public int wallPriceMax;
        /// <summary>城墙每级加固量</summary>
        public float wallReinforceAmount;
        /// <summary>买不起任何奖励时的兜底价格上限</summary>
        public int fallbackMaxPrice;
    }

    /// <summary>城门配置</summary>
    [Serializable]
    public class GateConfigData
    {
        /// <summary>初始城门耐久上限</summary>
        public float initialMaxHp;
        /// <summary>每级加固后的上限增量（与商店加固叠加）</summary>
        public float reinforceAmount;
        /// <summary>城门耐久上限的绝对上限</summary>
        public float maxHpCap;
    }

    /// <summary>战斗配置</summary>
    [Serializable]
    public class CombatConfigData
    {
        /// <summary>同屏子弹上限</summary>
        public int maxBullets;
        /// <summary>同屏敌人上限</summary>
        public int maxEnemies;
        /// <summary>漂浮伤害数字上限</summary>
        public int maxFloatingTexts;
        /// <summary>同帧最多播放的开火音效数</summary>
        public int maxFireSoundsPerFrame;
        /// <summary>子弹存活时间上限（秒）</summary>
        public float bulletLifetime;
        /// <summary>敌人到达城门后的攻击间隔（秒）</summary>
        public float gateAttackInterval;
        /// <summary>装备槽解锁关卡（第 i 个元素 = 第几关解锁 i+1 个槽）</summary>
        public int[] slotUnlockLevels;
    }

    /// <summary>模拟广告配置</summary>
    [Serializable]
    public class AdsConfigData
    {
        /// <summary>金币翻倍每日次数</summary>
        public int dailyDoubleGoldCount;
        /// <summary>免费重刷每日次数</summary>
        public int dailyFreeRerollCount;
        /// <summary>金币翻倍倍率</summary>
        public float doubleGoldMultiplier;
    }

    /// <summary>全部数值配置根对象（tuning.json）</summary>
    [Serializable]
    public class TuningConfigData
    {
        /// <summary>波次配置</summary>
        public WaveConfigData waves;
        /// <summary>难度配置</summary>
        public DifficultyConfigData difficulty;
        /// <summary>经济 / 连杀配置</summary>
        public EconomyConfigData economy;
        /// <summary>商店配置</summary>
        public ShopConfigData shop;
        /// <summary>城门配置</summary>
        public GateConfigData gate;
        /// <summary>战斗配置</summary>
        public CombatConfigData combat;
        /// <summary>广告配置</summary>
        public AdsConfigData ads;
    }
}
