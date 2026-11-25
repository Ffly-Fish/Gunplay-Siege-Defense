using System.Collections;
using Gunplay.Core;
using Gunplay.Run;
using UnityEngine;
using UnityEngine.UI;

// ResultPanel.cs —— 过关结算页（关卡通过 / 本关数据 / 广告金币翻倍 / 进入商店）
namespace Gunplay.UI
{
    /// <summary>过关结算页：本关金币滚动计数、击杀 / 最高连杀 / 命中率、广告金币 ×2、进入商店</summary>
    public sealed class ResultPanel : UiPanelBase
    {
        // 标题文字（第 N 关通过）
        private Text titleText;
        // 副标题（通关奖励说明）
        private Text subtitleText;
        // 本关金币数值
        private Text goldValue;
        // 击杀数数值
        private Text killsValue;
        // 最高连杀数值
        private Text comboValue;
        // 命中率数值
        private Text accuracyValue;
        // 广告金币翻倍按钮
        private Button adButton;
        // 广告金币翻倍按钮文字
        private Text adLabel;
        // 今日剩余翻倍次数文字
        private Text adCountText;
        // 本关统计快照（LevelClearedEvent 缓存）
        private LevelStats lastStats;
        // 本关关卡号（事件缓存）
        private int lastLevel;
        // 本关通关奖励金币（事件缓存）
        private int lastReward;
        // 金币滚动计数协程
        private Coroutine goldCountRoutine;

        /// <summary>构建结算页界面（只执行一次）</summary>
        public override void Build()
        {
            RectTransform root = PanelRect;

            // 全屏遮罩（接收点击，压暗战场）
            Image dim = UiBuilder.NewImage("Dim", root, UiTheme.Dim, null, true);
            UiBuilder.Stretch(dim.rectTransform);

            // 结算卡片
            Image card = UiBuilder.NewPanel("Card", root, UiTheme.Panel);
            UiBuilder.Anchor(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(920f, 760f));

            // 主视觉标题
            titleText = UiBuilder.NewText("Title", root, "第 1 关通过", UiTheme.FontH1, UiTheme.Gold);
            UiBuilder.Anchor(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 620f), new Vector2(1000f, 120f));

            // 副标题
            subtitleText = UiBuilder.NewText("Subtitle", root, "通关奖励已入账", UiTheme.FontSmall, UiTheme.TextSecondary);
            UiBuilder.Anchor(subtitleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 534f), new Vector2(1000f, 48f));

            // 卡片标题
            Text cardTitle = UiBuilder.NewText("CardTitle", card.transform, "本关结算", UiTheme.FontBody, UiTheme.Gold);
            UiBuilder.Anchor(cardTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(800f, 48f));

            // 四行数据
            goldValue = CreateRow(card.rectTransform, "本关金币", 120f);
            killsValue = CreateRow(card.rectTransform, "击杀数", 30f);
            comboValue = CreateRow(card.rectTransform, "最高连杀", -60f);
            accuracyValue = CreateRow(card.rectTransform, "命中率", -150f);

            // 广告金币翻倍按钮
            adButton = UiBuilder.NewButton("AdButton", root, "看广告 · 金币 ×2", UiTheme.PanelLight, UiTheme.Gold, 38, OnAdClicked);
            UiBuilder.Anchor((RectTransform)adButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -510f), new Vector2(700f, 110f));
            adLabel = adButton.GetComponentInChildren<Text>();

            // 广告剩余次数
            adCountText = UiBuilder.NewText("AdCount", root, "今日剩余 3 次", UiTheme.FontTiny, UiTheme.TextSecondary);
            UiBuilder.Anchor(adCountText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -592f), new Vector2(700f, 40f));

            // 进入商店（主按钮）
            Button continueButton = UiBuilder.NewButton("ContinueButton", root, "进入商店", UiTheme.Gold, UiTheme.Background, 44, OnContinueClicked);
            UiBuilder.Anchor((RectTransform)continueButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -700f), new Vector2(700f, 120f));

            MarkBuilt();
        }

        /// <summary>刷新结算数据（从本关统计快照或 RunState 读取）</summary>
        public override void Refresh()
        {
            GameRuntime runtime = Runtime;
            if (runtime == null)
            {
                return;
            }

            LevelStats stats = ResolveStats();
            if (stats == null)
            {
                return;
            }

            int level = lastLevel > 0 ? lastLevel : (runtime.Run != null ? runtime.Run.level : 1);
            titleText.text = "第 " + level + " 关通过";
            subtitleText.text = lastReward > 0 ? "通关奖励 +" + UiTheme.FormatInt(lastReward) + " 金" : "本关奖励已入账";
            killsValue.text = stats.kills + " 个";
            comboValue.text = stats.bestCombo + " 连杀";
            accuracyValue.text = (stats.Accuracy * 100f).ToString("0.#") + "%";

            // 有滚动动画时不覆盖金币文字，避免跳动
            if (goldCountRoutine == null)
            {
                goldValue.text = UiTheme.FormatInt(stats.goldEarned);
            }

            RefreshAdButton(runtime);
        }

        /// <summary>订阅关卡通过事件（缓存本关数据）</summary>
        protected override void Subscribe(GameRuntime runtime)
        {
            runtime.Events.Subscribe<LevelClearedEvent>(OnLevelCleared);
        }

        /// <summary>反注册关卡通过事件</summary>
        protected override void Unsubscribe(GameRuntime runtime)
        {
            runtime.Events.Unsubscribe<LevelClearedEvent>(OnLevelCleared);
        }

        /// <summary>显示时刷新并开始金币滚动计数</summary>
        protected override void OnShown()
        {
            if (!Built)
            {
                return;
            }

            Refresh();
            StartGoldCountUp();
        }

        /// <summary>隐藏时停止滚动计数</summary>
        protected override void OnHidden()
        {
            if (goldCountRoutine != null)
            {
                StopCoroutine(goldCountRoutine);
                goldCountRoutine = null;
            }
        }

        // 关卡通过：缓存数据，若正在显示则立即刷新
        private void OnLevelCleared(LevelClearedEvent evt)
        {
            lastStats = evt.Stats;
            lastLevel = evt.Level;
            lastReward = evt.Reward;
            if (IsVisible)
            {
                Refresh();
                StartGoldCountUp();
            }
        }

        // 取本关统计：优先事件快照，其次当前 RunState
        private LevelStats ResolveStats()
        {
            if (lastStats != null)
            {
                return lastStats;
            }

            GameRuntime runtime = Runtime;
            if (runtime == null || runtime.Run == null)
            {
                return null;
            }

            return runtime.Run.currentLevelStats;
        }

        // 刷新广告按钮（按今日剩余次数置灰）
        private void RefreshAdButton(GameRuntime runtime)
        {
            SavedStats stats = runtime.Stats;
            int left = stats != null ? stats.goldDoubleLeft : 0;
            bool interactable = left > 0;
            adButton.interactable = interactable;
            adLabel.text = interactable ? "看广告 · 金币 ×2" : "金币 ×2（今日已用完）";
            adLabel.color = interactable ? UiTheme.Gold : UiTheme.TextMuted;
            adCountText.text = "今日剩余 " + left + " 次";
        }

        // 创建一行「标签 + 数值」，返回数值文字组件
        private static Text CreateRow(RectTransform parent, string label, float y)
        {
            Text labelText = UiBuilder.NewText("Label_" + label, parent, label, UiTheme.FontBody, UiTheme.TextSecondary, TextAnchor.MiddleLeft);
            UiBuilder.Anchor(labelText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-370f, y), new Vector2(300f, 48f));

            Text valueText = UiBuilder.NewText("Value_" + label, parent, "--", UiTheme.FontBody, UiTheme.TextPrimary, TextAnchor.MiddleRight);
            UiBuilder.Anchor(valueText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(370f, y), new Vector2(420f, 48f));
            return valueText;
        }

        // 开始金币滚动计数
        private void StartGoldCountUp()
        {
            if (goldCountRoutine != null)
            {
                StopCoroutine(goldCountRoutine);
                goldCountRoutine = null;
            }

            LevelStats stats = ResolveStats();
            if (stats == null)
            {
                return;
            }

            goldCountRoutine = StartCoroutine(GoldCountRoutine(stats.goldEarned));
        }

        // 金币从 0 滚动到本关所得
        private IEnumerator GoldCountRoutine(int target)
        {
            const float duration = 0.55f;
            if (target <= 0)
            {
                goldValue.text = UiTheme.FormatInt(0);
                goldCountRoutine = null;
                yield break;
            }

            float elapsed = 0f;
            int shown = 0;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - progress) * (1f - progress);
                int value = Mathf.RoundToInt(target * eased);
                if (value != shown)
                {
                    shown = value;
                    goldValue.text = UiTheme.FormatInt(shown);
                }

                yield return null;
            }

            goldValue.text = UiTheme.FormatInt(target);
            goldCountRoutine = null;
        }

        // 广告金币翻倍按钮
        private void OnAdClicked()
        {
            GameRuntime runtime = Runtime;
            if (runtime == null)
            {
                return;
            }

            if (runtime.WatchAdDoubleGold())
            {
                Refresh();
            }
        }

        // 继续按钮：进入关间商店
        private void OnContinueClicked()
        {
            GameRuntime runtime = Runtime;
            if (runtime != null)
            {
                runtime.ContinueFromResult();
            }
        }
    }
}
