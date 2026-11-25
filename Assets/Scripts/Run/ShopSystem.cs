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
            for (int i = 0; i < count; i++)
            {
                ShopOffer offer = CreateOffer();
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

        // 生成单个奖励
        private ShopOffer CreateOffer()
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
                ShopOffer weaponOffer = TryCreateWeaponOffer();
                if (weaponOffer != null)
                {
                    return weaponOffer;
                }
            }
            else if (roll < shop.weightWeapon + shop.weightWall)
            {
                return CreateWallOffer();
            }

            ShopOffer upgradeOffer = TryCreateUpgradeOffer();
            return upgradeOffer ?? CreateWallOffer();
        }

        // 武器奖励：只刷当前关卡已解锁且未拥有的武器
        private ShopOffer TryCreateWeaponOffer()
        {
            List<WeaponConfigData> candidates = new List<WeaponConfigData>();
            for (int i = 0; i < config.Weapons.Count; i++)
            {
                WeaponConfigData weapon = config.Weapons[i];
                if (weapon.unlockLevel <= run.level && !run.OwnsWeapon(weapon.id))
                {
                    candidates.Add(weapon);
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            WeaponConfigData chosen = candidates[random.Range(0, candidates.Count)];
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

        // 词条奖励：只刷已拥有武器可用的、未满级的词条
        private ShopOffer TryCreateUpgradeOffer()
        {
            List<ShopOffer> candidates = new List<ShopOffer>();
            for (int i = 0; i < config.Upgrades.Count; i++)
            {
                UpgradeConfigData upgrade = config.Upgrades[i];
                if (!upgrades.CanLevelUp(upgrade, string.Empty))
                {
                    // 全局词条上面已判定；武器词条逐个武器检查
                }

                if (upgrade.applicableWeapons == null || upgrade.applicableWeapons.Length == 0)
                {
                    if (upgrades.CanLevelUp(upgrade, string.Empty))
                    {
                        candidates.Add(CreateUpgradeOffer(upgrade, string.Empty, "全局"));
                    }

                    continue;
                }

                for (int w = 0; w < upgrade.applicableWeapons.Length; w++)
                {
                    string weaponId = upgrade.applicableWeapons[w];
                    if (run.OwnsWeapon(weaponId) && upgrades.CanLevelUp(upgrade, weaponId))
                    {
                        candidates.Add(CreateUpgradeOffer(upgrade, weaponId, config.GetWeapon(weaponId).displayName));
                    }
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            return candidates[random.Range(0, candidates.Count)];
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

        // 城墙奖励
        private ShopOffer CreateWallOffer()
        {
            ShopConfigData shop = config.Tuning.shop;
            int price = random.Range(shop.wallPriceMin, Mathf.Max(shop.wallPriceMin + 1, shop.wallPriceMax));
            float amount = Mathf.Max(1f, shop.wallReinforceAmount);
            return new ShopOffer
            {
                Kind = ShopOfferKind.Wall,
                Title = $"城墙加固 +{amount:0}",
                Detail = $"城门耐久上限 +{amount:0}，并回复等量耐久",
                Price = Mathf.Max(1, price),
                Tier = 0,
                WallAmount = amount,
                IsValid = true
            };
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
