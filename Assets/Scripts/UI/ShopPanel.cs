using System.Collections.Generic;
using Gunplay.Core;
using Gunplay.Run;
using UnityEngine;
using UnityEngine.UI;

// ShopPanel.cs —— 关间随机商店（奖励卡 / 购买 / 付费重刷 / 免费重刷 / 继续下一关）
namespace Gunplay.UI
{
    /// <summary>关间商店：展示奖励列表与购买按钮、重刷与免费重刷、当前金币与已拥有武器</summary>
    public sealed class ShopPanel : UiPanelBase
    {
        // 当前金币文字
        private Text goldText;
        // 已拥有武器文字
        private Text ownedText;
        // 三张奖励卡
        private readonly ShopCard[] cards = new ShopCard[3];
        // 付费重刷按钮
        private Button rerollButton;
        // 付费重刷按钮文字
        private Text rerollLabel;
        // 免费重刷（广告）按钮
        private Button freeRerollButton;
        // 免费重刷按钮文字
        private Text freeRerollLabel;
        // 上次显示的金币（避免每帧改文本）
        private int lastGold = -1;
        // 上次显示的已拥有武器文字（避免重复拼接）
        private string lastOwned = string.Empty;

        /// <summary>单张奖励卡的界面引用</summary>
        private sealed class ShopCard
        {
            /// <summary>卡片底图（整卡显隐用）</summary>
            public Image Root;
            /// <summary>左侧类别色标</summary>
            public Image Stripe;
            /// <summary>奖励标题</summary>
            public Text Title;
            /// <summary>奖励说明</summary>
            public Text Detail;
            /// <summary>稀有度 / 售出状态标签</summary>
            public Text TierTag;
            /// <summary>购买按钮</summary>
            public Button BuyButton;
            /// <summary>购买按钮上的价格文字</summary>
            public Text PriceLabel;
        }

        /// <summary>构建商店界面（只执行一次）</summary>
        public override void Build()
        {
            RectTransform root = PanelRect;

            // 全屏遮罩
            Image dim = UiBuilder.NewImage("Dim", root, UiTheme.Dim, null, true);
            UiBuilder.Stretch(dim.rectTransform);

            // 标题（左上）
            Text title = UiBuilder.NewText("Title", root, "关间商店", UiTheme.FontH1, UiTheme.Gold, TextAnchor.MiddleLeft);
            UiBuilder.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -44f), new Vector2(600f, 100f));

            // 当前金币（右上）
            goldText = UiBuilder.NewText("GoldText", root, "金币 0", UiTheme.FontBody, UiTheme.Gold, TextAnchor.MiddleRight);
            UiBuilder.Anchor(goldText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -64f), new Vector2(560f, 56f));

            // 已拥有武器（标题下方一行）
            ownedText = UiBuilder.NewText("OwnedText", root, "已拥有：无", UiTheme.FontTiny, UiTheme.TextSecondary, TextAnchor.MiddleLeft);
            UiBuilder.Anchor(ownedText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -160f), new Vector2(960f, 60f));

            // 三张奖励卡
            for (int i = 0; i < cards.Length; i++)
            {
                float y = 400f - i * 330f;
                cards[i] = CreateCard(root, i, y);
            }

            // 付费重刷（左下）
            rerollButton = UiBuilder.NewButton("RerollButton", root, "重刷商店", UiTheme.PanelLight, UiTheme.TextPrimary, 30, OnRerollClicked);
            UiBuilder.Anchor((RectTransform)rerollButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-240f, -610f), new Vector2(440f, 110f));
            rerollLabel = rerollButton.GetComponentInChildren<Text>();

            // 免费重刷（右下）
            freeRerollButton = UiBuilder.NewButton("FreeRerollButton", root, "免费重刷", UiTheme.PanelLight, UiTheme.Success, 30, OnFreeRerollClicked);
            UiBuilder.Anchor((RectTransform)freeRerollButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(240f, -610f), new Vector2(440f, 110f));
            freeRerollLabel = freeRerollButton.GetComponentInChildren<Text>();

            // 继续下一关（主按钮）
            Button continueButton = UiBuilder.NewButton("ContinueButton", root, "出征 · 下一关", UiTheme.Gold, UiTheme.Background, 42, OnContinueClicked);
            UiBuilder.Anchor((RectTransform)continueButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -770f), new Vector2(700f, 120f));

            MarkBuilt();
        }

        /// <summary>刷新商店（金币 / 已拥有武器 / 奖励卡 / 按钮可用性）</summary>
        public override void Refresh()
        {
            GameRuntime runtime = Runtime;
            if (runtime == null)
            {
                return;
            }

            RunState run = runtime.Run;
            if (run == null)
            {
                return;
            }

            ShopSystem shop = runtime.Shop;
            RefreshGold(run);
            RefreshOwnedWeapons(runtime, run);
            RefreshCards(runtime, run, shop);
            RefreshButtons(runtime, run, shop);
        }

        /// <summary>订阅商店与金币事件</summary>
        protected override void Subscribe(GameRuntime runtime)
        {
            runtime.Events.Subscribe<ShopRolledEvent>(OnShopRolled);
            runtime.Events.Subscribe<ShopPurchasedEvent>(OnShopPurchased);
            runtime.Events.Subscribe<GoldChangedEvent>(OnGoldChanged);
        }

        /// <summary>反注册商店与金币事件</summary>
        protected override void Unsubscribe(GameRuntime runtime)
        {
            runtime.Events.Unsubscribe<ShopRolledEvent>(OnShopRolled);
            runtime.Events.Unsubscribe<ShopPurchasedEvent>(OnShopPurchased);
            runtime.Events.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
        }

        /// <summary>显示时刷新一次（进入商店时自动读取最新奖励）</summary>
        protected override void OnShown()
        {
            if (Built)
            {
                Refresh();
            }
        }

        // 创建一张奖励卡
        private ShopCard CreateCard(RectTransform root, int index, float y)
        {
            ShopCard card = new ShopCard();
            card.Root = UiBuilder.NewPanel("OfferCard" + index, root, UiTheme.Panel);
            UiBuilder.Anchor(card.Root.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(960f, 290f));

            // 左侧类别色标（拉满卡片高度）
            card.Stripe = UiBuilder.NewImage("Stripe", card.Root.transform, UiTheme.Gold, null, false);
            RectTransform stripeRect = card.Stripe.rectTransform;
            stripeRect.anchorMin = new Vector2(0f, 0f);
            stripeRect.anchorMax = new Vector2(0f, 1f);
            stripeRect.pivot = new Vector2(0f, 0.5f);
            stripeRect.anchoredPosition = new Vector2(10f, 0f);
            stripeRect.sizeDelta = new Vector2(10f, -36f);

            // 标题
            card.Title = UiBuilder.NewText("Title", card.Root.transform, "奖励", UiTheme.FontH2, UiTheme.TextPrimary, TextAnchor.MiddleLeft);
            UiBuilder.Anchor(card.Title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -30f), new Vector2(520f, 60f));

            // 说明（可换行）
            card.Detail = UiBuilder.NewText("Detail", card.Root.transform, string.Empty, UiTheme.FontTiny, UiTheme.TextSecondary, TextAnchor.UpperLeft);
            UiBuilder.Anchor(card.Detail.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -100f), new Vector2(560f, 160f));

            // 稀有度 / 状态标签
            card.TierTag = UiBuilder.NewText("TierTag", card.Root.transform, string.Empty, UiTheme.FontTiny, UiTheme.TierCommon, TextAnchor.MiddleRight);
            UiBuilder.Anchor(card.TierTag.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-290f, -26f), new Vector2(240f, 36f));

            // 购买按钮（点击按列表下标购买）
            card.BuyButton = UiBuilder.NewButton("BuyButton", card.Root.transform, string.Empty, UiTheme.PanelLight, UiTheme.Gold, 32, () => OnBuyClicked(index));
            UiBuilder.Anchor((RectTransform)card.BuyButton.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(250f, 110f));
            card.PriceLabel = card.BuyButton.GetComponentInChildren<Text>();
            if (card.PriceLabel != null)
            {
                card.PriceLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            return card;
        }

        // 刷新金币文字
        private void RefreshGold(RunState run)
        {
            if (run.gold == lastGold)
            {
                return;
            }

            lastGold = run.gold;
            goldText.text = "金币 " + UiTheme.FormatInt(run.gold);
        }

        // 刷新「已拥有武器」一行
        private void RefreshOwnedWeapons(GameRuntime runtime, RunState run)
        {
            string owned = "已拥有：";
            if (run.ownedWeapons.Count == 0)
            {
                owned += "无";
            }
            else
            {
                for (int i = 0; i < run.ownedWeapons.Count; i++)
                {
                    if (i > 0)
                    {
                        owned += "、";
                    }

                    owned += GetWeaponName(runtime, run.ownedWeapons[i]);
                }
            }

            if (owned != lastOwned)
            {
                lastOwned = owned;
                ownedText.text = owned;
            }
        }

        // 刷新三张奖励卡（不可售出的卡片隐藏）
        private void RefreshCards(GameRuntime runtime, RunState run, ShopSystem shop)
        {
            IReadOnlyList<ShopOffer> offers = shop != null ? shop.Offers : null;
            for (int i = 0; i < cards.Length; i++)
            {
                ShopCard card = cards[i];
                bool hasOffer = offers != null && i < offers.Count && offers[i] != null;
                if (card.Root.gameObject.activeSelf != hasOffer)
                {
                    card.Root.gameObject.SetActive(hasOffer);
                }

                if (!hasOffer)
                {
                    continue;
                }

                ShopOffer offer = offers[i];
                card.Title.text = offer.Title;
                card.Detail.text = offer.Detail;
                card.Stripe.color = GetOfferColor(offer.Kind);
                card.Title.color = UiTheme.TierColor(offer.Tier);

                bool available = offer.IsValid;
                card.TierTag.text = available ? GetTierLabel(offer.Tier) : "已售出";
                card.TierTag.color = available ? UiTheme.TierColor(offer.Tier) : UiTheme.TextMuted;

                bool affordable = available && run.gold >= offer.Price;
                card.BuyButton.interactable = affordable;
                card.PriceLabel.text = available ? UiTheme.FormatInt(offer.Price) + " 金" : "已购买";
                card.PriceLabel.color = affordable ? UiTheme.Gold : UiTheme.TextMuted;
            }
        }

        // 刷新重刷 / 免费重刷 / 继续按钮的可用性与文字
        private void RefreshButtons(GameRuntime runtime, RunState run, ShopSystem shop)
        {
            int maxRerolls = 0;
            if (runtime.Config != null && runtime.Config.Tuning != null && runtime.Config.Tuning.shop != null)
            {
                maxRerolls = runtime.Config.Tuning.shop.maxRerolls;
            }

            int cost = shop != null ? shop.GetRerollCost() : 0;
            int rerolls = shop != null ? shop.RerollsThisLevel : run.rerollsThisLevel;
            bool canPaid = shop != null && shop.CanReroll() && run.gold >= cost;
            rerollButton.interactable = canPaid;
            rerollLabel.text = "重刷 · " + UiTheme.FormatInt(cost) + " 金（" + rerolls + "/" + maxRerolls + "）";
            rerollLabel.color = canPaid ? UiTheme.TextPrimary : UiTheme.TextMuted;

            int freeLeft = runtime.Stats != null ? runtime.Stats.freeRerollLeft : 0;
            bool canFree = freeLeft > 0 && shop != null && shop.CanReroll();
            freeRerollButton.interactable = canFree;
            freeRerollLabel.text = freeLeft > 0 ? "免费重刷 · 剩 " + freeLeft + " 次" : "免费重刷 · 今日已用完";
            freeRerollLabel.color = canFree ? UiTheme.Success : UiTheme.TextMuted;
        }

        // 按 offer 类别取色标颜色
        private static Color GetOfferColor(ShopOfferKind kind)
        {
            switch (kind)
            {
                case ShopOfferKind.Weapon:
                    return UiTheme.Gold;
                case ShopOfferKind.Wall:
                    return UiTheme.Success;
                case ShopOfferKind.Upgrade:
                    return UiTheme.TierRare;
                default:
                    return UiTheme.TextMuted;
            }
        }

        // 按稀有度取标签文字
        private static string GetTierLabel(int tier)
        {
            switch (tier)
            {
                case 1:
                    return "稀有";
                case 2:
                    return "史诗";
                default:
                    return "普通";
            }
        }

        // 取武器中文名（取不到时回退 id）
        private static string GetWeaponName(GameRuntime runtime, string weaponId)
        {
            WeaponConfigData config = null;
            if (runtime.Config != null)
            {
                runtime.Config.TryGetWeapon(weaponId, out config);
            }

            return config != null ? config.displayName : weaponId;
        }

        // 购买按钮：按列表下标调用 BuyOffer
        private void OnBuyClicked(int index)
        {
            GameRuntime runtime = Runtime;
            if (runtime == null)
            {
                return;
            }

            runtime.BuyOffer(index);
        }

        // 付费重刷
        private void OnRerollClicked()
        {
            GameRuntime runtime = Runtime;
            if (runtime != null)
            {
                runtime.RerollShop();
            }
        }

        // 免费重刷（模拟广告）
        private void OnFreeRerollClicked()
        {
            GameRuntime runtime = Runtime;
            if (runtime != null)
            {
                runtime.WatchAdFreeReroll();
            }
        }

        // 继续下一关
        private void OnContinueClicked()
        {
            GameRuntime runtime = Runtime;
            if (runtime != null)
            {
                runtime.ContinueFromShop();
            }
        }

        // 商店刷新（Roll / Reroll 后广播）
        private void OnShopRolled(ShopRolledEvent evt)
        {
            lastGold = -1;
            if (IsVisible)
            {
                Refresh();
            }
        }

        // 购买成功：刷新价格与可用性
        private void OnShopPurchased(ShopPurchasedEvent evt)
        {
            lastGold = -1;
            if (IsVisible)
            {
                Refresh();
            }
        }

        // 金币变化：刷新按钮可用性
        private void OnGoldChanged(GoldChangedEvent evt)
        {
            if (IsVisible)
            {
                Refresh();
            }
        }
    }
}
