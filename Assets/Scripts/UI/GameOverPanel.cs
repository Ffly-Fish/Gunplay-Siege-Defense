using Gunplay.Core;
using Gunplay.Run;
using UnityEngine;
using UnityEngine.UI;

// GameOverPanel.cs —— 本轮结束页（到达关卡 / 总击杀 / 最高连杀 / 再来一次 / 回主菜单）
namespace Gunplay.UI
{
    /// <summary>本轮结束页：展示本轮战果并提供「再来一次」「回主菜单」</summary>
    public sealed class GameOverPanel : UiPanelBase
    {
        // 到达关卡数值
        private Text levelValue;
        // 本轮总击杀数值
        private Text killsValue;
        // 本轮最高连杀数值
        private Text comboValue;
        // 历史最高关卡数值
        private Text bestValue;

        /// <summary>构建本轮结束界面（只执行一次）</summary>
        public override void Build()
        {
            RectTransform root = PanelRect;

            // 全屏遮罩
            Image dim = UiBuilder.NewImage("Dim", root, UiTheme.Dim, null, true);
            UiBuilder.Stretch(dim.rectTransform);

            // 战果卡片（像素九宫格卡片）
            Image card = UiBuilder.NewPixelCard("Card", root);
            UiBuilder.Anchor(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(900f, 620f));

            // 主标题
            Text title = UiBuilder.NewText("Title", root, "本轮结束", UiTheme.FontTitle, UiTheme.Danger);
            UiBuilder.Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 600f), new Vector2(1000f, 140f));

            // 副标题
            Text subtitle = UiBuilder.NewText("Subtitle", root, "城门失守，本轮结束", UiTheme.FontSmall, UiTheme.TextSecondary);
            UiBuilder.Anchor(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 500f), new Vector2(1000f, 50f));

            // 卡片标题
            Text cardTitle = UiBuilder.NewText("CardTitle", card.transform, "本轮战果", UiTheme.FontBody, UiTheme.Gold);
            UiBuilder.Anchor(cardTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(800f, 48f));

            // 四行战果
            levelValue = CreateRow(card.rectTransform, "到达关卡", 100f);
            killsValue = CreateRow(card.rectTransform, "本轮击杀", 20f);
            comboValue = CreateRow(card.rectTransform, "最高连杀", -60f);
            bestValue = CreateRow(card.rectTransform, "历史最高关卡", -170f);

            // 再来一次（主按钮）
            Button restartButton = UiBuilder.NewPixelButton("RestartButton", root, "再来一次", UiTheme.Background, 44, OnRestartClicked);
            UiBuilder.Anchor((RectTransform)restartButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -430f), new Vector2(700f, 130f));

            // 回主菜单（次按钮）
            Button menuButton = UiBuilder.NewPixelSecondaryButton("MenuButton", root, "回主菜单", UiTheme.TextPrimary, 40, OnMenuClicked);
            UiBuilder.Anchor((RectTransform)menuButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -600f), new Vector2(700f, 120f));

            MarkBuilt();
        }

        /// <summary>刷新本轮战果（显示本轮结束页时调用）</summary>
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

            levelValue.text = "第 " + run.level + " 关";
            killsValue.text = run.totalKills + " 个";
            comboValue.text = run.bestCombo + " 连杀";

            SavedStats stats = runtime.Stats;
            bestValue.text = stats != null ? "第 " + stats.bestLevel + " 关" : "第 " + run.level + " 关";
        }

        /// <summary>订阅事件（本轮结束页只读状态，不订阅额外事件）</summary>
        protected override void Subscribe(GameRuntime runtime)
        {
        }

        /// <summary>反注册事件（与 Subscribe 对应，本轮结束页无额外事件）</summary>
        protected override void Unsubscribe(GameRuntime runtime)
        {
        }

        // 创建一行「标签 + 数值」，返回数值文字组件
        private static Text CreateRow(RectTransform parent, string label, float y)
        {
            Text labelText = UiBuilder.NewText("Label_" + label, parent, label, UiTheme.FontBody, UiTheme.TextSecondary, TextAnchor.MiddleLeft);
            UiBuilder.Anchor(labelText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-370f, y), new Vector2(320f, 48f));

            Text valueText = UiBuilder.NewText("Value_" + label, parent, "--", UiTheme.FontBody, UiTheme.TextPrimary, TextAnchor.MiddleRight);
            UiBuilder.Anchor(valueText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(370f, y), new Vector2(400f, 48f));
            return valueText;
        }

        // 再来一次：直接开始新一轮
        private void OnRestartClicked()
        {
            GameRuntime runtime = Runtime;
            if (runtime != null)
            {
                runtime.RestartRun();
            }
        }

        // 回主菜单
        private void OnMenuClicked()
        {
            GameRuntime runtime = Runtime;
            if (runtime != null)
            {
                runtime.ReturnToMenu();
            }
        }
    }
}
