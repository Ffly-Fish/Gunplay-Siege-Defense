using Gunplay.Core;
using Gunplay.Run;
using UnityEngine;
using UnityEngine.UI;

// MenuPanel.cs —— 主菜单界面（标题 / 开始新一轮 / 生涯统计）
namespace Gunplay.UI
{
    /// <summary>主菜单：游戏标题、开始按钮、最高关卡 / 最高连杀 / 累计击杀</summary>
    public sealed class MenuPanel : UiPanelBase
    {
        // 最高关卡数值文字
        private Text bestLevelValue;
        // 最高连杀数值文字
        private Text bestComboValue;
        // 累计击杀数值文字
        private Text totalKillsValue;

        /// <summary>构建主菜单界面（只执行一次）</summary>
        public override void Build()
        {
            RectTransform root = PanelRect;

            // 全屏底色（接收点击，避免透过界面误触战场）
            Image background = UiBuilder.NewImage("Background", root, UiTheme.Background, null, true);
            UiBuilder.Stretch(background.rectTransform);

            // 顶部金色装饰带
            Image accentTop = UiBuilder.NewImage("AccentTop", root, new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, 0.12f), null, false);
            UiBuilder.Anchor(accentTop.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1080f, 14f));

            // 底部金色装饰带
            Image accentBottom = UiBuilder.NewImage("AccentBottom", root, new Color(UiTheme.Gold.r, UiTheme.Gold.g, UiTheme.Gold.b, 0.08f), null, false);
            UiBuilder.Anchor(accentBottom.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(1080f, 10f));

            // 游戏标题
            Text title = UiBuilder.NewText("Title", root, "枪法守城闯大关", UiTheme.FontTitle, UiTheme.Gold);
            UiBuilder.Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 540f), new Vector2(1020f, 150f));

            // 副标题
            Text subtitle = UiBuilder.NewText("Subtitle", root, "单指瞄准 · 无限闯关 · 关间随机商店", UiTheme.FontSmall, UiTheme.TextSecondary);
            UiBuilder.Anchor(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 442f), new Vector2(1020f, 50f));

            // 生涯统计卡片（像素九宫格面板）
            Image statsCard = UiBuilder.NewPixelPanel("StatsCard", root);
            UiBuilder.Anchor(statsCard.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(880f, 330f));

            Text statsTitle = UiBuilder.NewText("StatsTitle", statsCard.transform, "生涯战绩", UiTheme.FontBody, UiTheme.Gold);
            UiBuilder.Anchor(statsTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(800f, 48f));

            bestLevelValue = CreateStatRow(statsCard.rectTransform, "最高关卡", 55f);
            bestComboValue = CreateStatRow(statsCard.rectTransform, "最高连杀", -15f);
            totalKillsValue = CreateStatRow(statsCard.rectTransform, "累计击杀", -85f);

            // 开始按钮（像素立体主按钮）
            Button startButton = UiBuilder.NewPixelButton("StartButton", root, "开始新一轮", UiTheme.Background, 46, OnStartClicked);
            UiBuilder.Anchor((RectTransform)startButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -230f), new Vector2(680f, 130f));

            // 底部操作提示
            Text hint = UiBuilder.NewText("Hint", root, "按住屏幕瞄准，松手停火", UiTheme.FontTiny, UiTheme.TextMuted);
            UiBuilder.Anchor(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -380f), new Vector2(900f, 40f));

            MarkBuilt();
        }

        /// <summary>刷新生涯统计（显示主菜单时调用）</summary>
        public override void Refresh()
        {
            GameRuntime runtime = Runtime;
            if (runtime == null)
            {
                return;
            }

            SavedStats stats = runtime.Stats;
            if (stats == null)
            {
                return;
            }

            bestLevelValue.text = "第 " + stats.bestLevel + " 关";
            bestComboValue.text = stats.bestCombo + " 连杀";
            totalKillsValue.text = UiTheme.FormatInt(stats.totalKills);
        }

        /// <summary>订阅事件（主菜单只读状态，不订阅额外事件）</summary>
        protected override void Subscribe(GameRuntime runtime)
        {
        }

        /// <summary>反注册事件（与 Subscribe 对应，主菜单无额外事件）</summary>
        protected override void Unsubscribe(GameRuntime runtime)
        {
        }

        // 创建一行「标签 + 数值」，返回数值文字组件
        private static Text CreateStatRow(RectTransform parent, string label, float y)
        {
            Text labelText = UiBuilder.NewText("Label_" + label, parent, label, UiTheme.FontBody, UiTheme.TextSecondary, TextAnchor.MiddleLeft);
            UiBuilder.Anchor(labelText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-370f, y), new Vector2(300f, 48f));

            Text valueText = UiBuilder.NewText("Value_" + label, parent, "--", UiTheme.FontBody, UiTheme.TextPrimary, TextAnchor.MiddleRight);
            UiBuilder.Anchor(valueText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(370f, y), new Vector2(380f, 48f));
            return valueText;
        }

        // 开始按钮点击：进入战斗
        private void OnStartClicked()
        {
            GameRuntime runtime = Runtime;
            if (runtime != null)
            {
                runtime.StartNewRun();
            }
        }
    }
}
