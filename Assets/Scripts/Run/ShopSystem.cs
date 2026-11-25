using System.Collections.Generic;
using Gunplay.Core;
using UnityEngine;

// ShopSystem.cs —— 关间随机商店：生成奖励、重刷、购买与生效
namespace Gunplay.Run
{
    /// <summary>商店系统：奖励随机、重刷费用与购买结算</summary>
    public sealed class ShopSystem
    {
        // 配置库
        private readonly ConfigDatabase config;
        // 本轮状态
        private readonly RunState run;
        // 事件总线
        private readonly EventBus bus;
        // 经济系统
        private readonly EconomySystem economy;
        // 城墙系统（加固类奖励）
        private readonly WallSystem wall;
        // 词条系统（升级类奖励）
        private readonly UpgradeSystem upgrades;
        // 随机源
        private readonly IRandomSource random;
        // 当前奖励列表
        private readonly List<ShopOffer> offers = new List<ShopOffer>(4);

        /// <summary>当前奖励列表（只读）</summary>
        public IReadOnlyList<ShopOffer> Offers => offers;

        /// <summary>本关已重刷次数</summary>
        public int RerollsThisLevel => run.rerollsThisLevel;

        /// <summary>构造：注入依赖</summary>
        public ShopSystem(ConfigDatabase configDatabase, RunState runState, EventBus eventBus,
            EconomySystem economySystem, WallSystem wallSystem, UpgradeSystem upgradeSystem, IRandomSource randomSource)
        {
            config = configDatabase;
            run = runState;
            bus = eventBus;
            economy = economySystem;
            wall = wallSystem;
            upgrades = upgradeSystem;
            random = randomSource;
        }

        /// <summary>下一次重刷价格（基础价 + 每次增量 × 已重刷次数）</summary>
        public int GetRerollCost()
        {
            ShopConfigData shop = config.Tuning.shop;
            return shop.rerollBaseCost + shop.rerollCostStep * run.rerollsThisLevel;
        }

        /// <summary>是否可以继续重刷（本关次数上限）</summary>
        public bool CanReroll()
        {
            return run.rerollsThisLevel < config.Tuning.shop.maxRerolls;
        }

        /// <summary>刷出一批奖励（进入商店时调用）</summary>
        public void Roll()
        {
            offers.Clear();
            int count = Mathf.Max(1, config.Tuning.shop.offerCount);
            // 本次刷新的去重键集合：避免同一批里出现两个完全相同的武器/词条奖励
            HashSet<string> usedKeys = new HashSet<string>();
            for (int i = 0; i < count; i++)
            {
                ShopOffer offer = CreateOffer(usedKeys);
                if (offer != null)
                {
                    offers.Add(offer);
                }
            }

            EnsureAffordable();
            bus.Publish(new ShopRolledEvent
            {
                Offers = offers,
                RerollCount = run.rerollsThisLevel,
                RerollCost = GetRerollCost()
            });
        }

        /// <summary>重刷（调用方负责扣费 / 免费次数）</summary>
        public void Reroll()
        {
            run.rerollsThisLevel++;
            Roll();
        }

        /// <summary>购买第 index 个奖励，成功返回 true</summary>
        public bool Purchase(int index)
        {
            if (index < 0 || index >= offers.Count)
            {
                return false;
            }

            ShopOffer offer = offers[index];
            if (offer == null || !offer.IsValid || offer.Price > run.gold)
            {
                return false;
            }

            if (!economy.TrySpend(offer.Price))
            {
                return false;
            }

            bool applied;
            switch (offer.Kind)
            {
                case ShopOfferKind.Weapon:
                    applied = BuyWeapon(offer.WeaponId);
                    break;
                case ShopOfferKind.Upgrade:
                    applied = BuyUpgrade(offer.UpgradeId, offer.UpgradeWeaponId);
                    break;
                case ShopOfferKind.Wall:
                    wall.Reinforce(offer.WallAmount);
                    applied = true;
                    break;
                default:
                    applied = false;
                    break;
            }

            if (!applied)
            {
                // 生效失败时退款，保证玩家不亏
                economy.AddGold(offer.Price);
                return false;
            }

            if (offer.Kind == ShopOfferKind.Weapon)
            {
                offer.IsValid = false;
            }

            bus.Publish(new ShopPurchasedEvent { Offer = offer });
            return true;
        }

        // 武器购买
        private bool BuyWeapon(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId) || run.OwnsWeapon(weaponId))
            {
                return false;
            }

            WeaponConfigData weapon = config.GetWeapon(weaponId);
            run.UnlockWeapon(weaponId, Mathf.Max(1, weapon.magazine), wall.GetUnlockedSlots());
            wall.SyncSlotsWithLevel();
            return true;
        }

        // 词条购买
        private bool BuyUpgrade(string upgradeId, string weaponId)
        {
            if (string.IsNullOrEmpty(upgradeId) || !config.TryGetUpgrade(upgradeId, out UpgradeConfigData upgrade))
            {
                return false;
            }

            if (!upgrades.CanLevelUp(upgrade, weaponId))
            {
                return false;
            }

            upgrades.AddLevel(upgrade, weaponId);
            return true;
        }

        // 生成单个奖励（usedKeys 用于同批去重）
        private ShopOffer CreateOffer(HashSet<string> usedKeys)
        {
            ShopConfigData shop = config.Tuning.shop;
            int total = shop.weightWeapon + shop.weightWall + shop.weightUpgrade;
            if (total <= 0)
            {
                return CreateWallOffer();
            }

            int roll = random.Range(0, total);
            if (roll < shop.weightWeapon)
            {
                ShopOffer weaponOffer = TryCreateWeaponOffer(usedKeys);
                if (weaponOffer != null)
                {
                    return weaponOffer;
                }
            }
            else if (roll < shop.weightWeapon + shop.weightWall)
            {
                return CreateWallOffer(usedKeys);
            }

            ShopOffer upgradeOffer = TryCreateUpgradeOffer(usedKeys);
            return upgradeOffer ?? CreateWallOffer(usedKeys);
        }

        // 武器奖励去重键
        private static string WeaponKey(string weaponId)
        {
            return "w:" + weaponId;
        }

        // 词条奖励去重键（同一词条对不同武器是不同奖励）
        private static string UpgradeKey(string upgradeId, string weaponId)
        {
            return "u:" + upgradeId + "|" + weaponId;
        }

        // 武器奖励：只刷当前关卡已解锁、未拥有且本批未出现过的武器
        private ShopOffer TryCreateWeaponOffer(HashSet<string> usedKeys)
        {
            List<WeaponConfigData> candidates = new List<WeaponConfigData>();
            for (int i = 0; i < config.Weapons.Count; i++)
            {
                WeaponConfigData weapon = config.Weapons[i];
                if (weapon.unlockLevel <= run.level && !run.OwnsWeapon(weapon.id) && !usedKeys.Contains(WeaponKey(weapon.id)))
                {
                    candidates.Add(weapon);
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            WeaponConfigData chosen = candidates[random.Range(0, candidates.Count)];
            usedKeys.Add(WeaponKey(chosen.id));
            return new ShopOffer
            {
                Kind = ShopOfferKind.Weapon,
                Title = chosen.displayName,
                Detail = $"{chosen.role}｜伤害 {chosen.baseDamage:0.#}｜射速 {chosen.fireRate:0.#}/秒",
                Price = Mathf.Max(1, chosen.unlockPrice),
                Tier = 2,
                WeaponId = chosen.id,
                IsValid = true
            };
        }

        // 词条奖励：只刷已拥有武器可用的、未满级且本批未出现过的词条
        private ShopOffer TryCreateUpgradeOffer(HashSet<string> usedKeys)
        {
            List<ShopOffer> candidates = new List<ShopOffer>();
            for (int i = 0; i < config.Upgrades.Count; i++)
            {
                UpgradeConfigData upgrade = config.Upgrades[i];
                if (upgrade.applicableWeapons == null || upgrade.applicableWeapons.Length == 0)
                {
                    // 全局词条：装备栏任意武器都吃收益
                    if (upgrades.CanLevelUp(upgrade, string.Empty) && !usedKeys.Contains(UpgradeKey(upgrade.id, string.Empty)))
                    {
                        candidates.Add(CreateUpgradeOffer(upgrade, string.Empty, "全局"));
                    }

                    continue;
                }

                for (int w = 0; w < upgrade.applicableWeapons.Length; w++)
                {
                    string weaponId = upgrade.applicableWeapons[w];
                    if (run.OwnsWeapon(weaponId) && upgrades.CanLevelUp(upgrade, weaponId)
                        && !usedKeys.Contains(UpgradeKey(upgrade.id, weaponId)))
                    {
                        candidates.Add(CreateUpgradeOffer(upgrade, weaponId, config.GetWeapon(weaponId).displayName));
                    }
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            ShopOffer chosenOffer = candidates[random.Range(0, candidates.Count)];
            usedKeys.Add(UpgradeKey(chosenOffer.UpgradeId, chosenOffer.UpgradeWeaponId));
            return chosenOffer;
        }

        // 构造词条奖励项
        private ShopOffer CreateUpgradeOffer(UpgradeConfigData upgrade, string weaponId, string ownerName)
        {
            int level = upgrades.GetLevel(upgrade.id, weaponId);
            int price = Mathf.Max(1, upgrade.basePrice + upgrade.basePrice / 2 * level);
            string effectText = DescribeUpgrade(upgrade);
            return new ShopOffer
            {
                Kind = ShopOfferKind.Upgrade,
                Title = $"{ownerName} · {upgrade.displayName} Lv{level + 1}",
                Detail = effectText,
                Price = price,
                Tier = level >= 2 ? 2 : 1,
                UpgradeId = upgrade.id,
                UpgradeWeaponId = weaponId,
                IsValid = true
            };
        }

        // 词条效果文案
        private static string DescribeUpgrade(UpgradeConfigData upgrade)
        {
            switch (upgrade.kind)
            {
                case UpgradeKind.Damage:
                    return $"伤害 +{upgrade.perLevelValue * 100f:0}%";
                case UpgradeKind.FireRate:
                    return $"射速 +{upgrade.perLevelValue * 100f:0}%";
                case UpgradeKind.Pellets:
                    return $"弹丸 +{upgrade.perLevelValue:0}";
                case UpgradeKind.Pierce:
                    return $"穿透 +{upgrade.perLevelValue:0}";
                case UpgradeKind.Magazine:
                    return $"弹匣 +{upgrade.perLevelValue * 100f:0}%";
                case UpgradeKind.Crit:
                    return $"暴击率 +{upgrade.perLevelValue * 100f:0}%";
                case UpgradeKind.ComboWindow:
                    return $"连杀窗口 +{upgrade.perLevelValue:0.##} 秒";
                default:
                    return upgrade.displayName;
            }
        }

        // 城墙奖励档位：小修 / 加固 / 大修（越贵加固越多，避免三张卡片完全一样）
        private static readonly float[] WALL_TIER_MULTIPLIERS = { 0.6f, 1f, 1.6f };

        // 城墙奖励档位名称（与 WALL_TIER_MULTIPLIERS 一一对应）
        private static readonly string[] WALL_TIER_NAMES = { "城墙小修", "城墙加固", "城墙大修" };

        // 城墙奖励（usedKeys 用于同批去重，可为 null）
        private ShopOffer CreateWallOffer(HashSet<string> usedKeys = null)
        {
            ShopConfigData shop = config.Tuning.shop;
            int tier = PickWallTier(usedKeys);
            float baseAmount = Mathf.Max(1f, shop.wallReinforceAmount);
            float amount = Mathf.Max(1f, Mathf.Round(baseAmount * WALL_TIER_MULTIPLIERS[tier]));
            int priceMin = Mathf.Max(1, Mathf.RoundToInt(shop.wallPriceMin * WALL_TIER_MULTIPLIERS[tier]));
            int priceMax = Mathf.Max(priceMin + 1, Mathf.RoundToInt(shop.wallPriceMax * WALL_TIER_MULTIPLIERS[tier]));
            int price = random.Range(priceMin, priceMax);
            usedKeys?.Add("wall:" + tier);
            return new ShopOffer
            {
                Kind = ShopOfferKind.Wall,
                Title = $"{WALL_TIER_NAMES[tier]} +{amount:0}",
                Detail = $"城门耐久上限 +{amount:0}，并回复等量耐久",
                Price = Mathf.Max(1, price),
                Tier = 0,
                WallAmount = amount,
                IsValid = true
            };
        }

        // 挑选一个本批还没出现过的城墙档位（都已出现时退回「加固」档）
        private int PickWallTier(HashSet<string> usedKeys)
        {
            if (usedKeys == null)
            {
                return 1;
            }

            List<int> available = new List<int>(WALL_TIER_MULTIPLIERS.Length);
            for (int i = 0; i < WALL_TIER_MULTIPLIERS.Length; i++)
            {
                if (!usedKeys.Contains("wall:" + i))
                {
                    available.Add(i);
                }
            }

            if (available.Count == 0)
            {
                return 1;
            }

            return available[random.Range(0, available.Count)];
        }

        // 保证至少有一个买得起的奖励（手册要求：不允许出现全部买不起）
        private void EnsureAffordable()
        {
            if (offers.Count == 0)
            {
                offers.Add(CreateWallOffer());
            }

            int cheapest = int.MaxValue;
            for (int i = 0; i < offers.Count; i++)
            {
                cheapest = Mathf.Min(cheapest, offers[i].Price);
            }

            if (run.gold >= cheapest)
            {
                return;
            }

            ShopOffer fallback = CreateWallOffer();
            fallback.Price = Mathf.Min(Mathf.Max(1, run.gold), config.Tuning.shop.fallbackMaxPrice);
            fallback.Title = "城墙小修 +10";
            fallback.Detail = "城门耐久上限 +10，并回复等量耐久";
            fallback.WallAmount = 10f;
            offers[0] = fallback;
        }
    }
}
