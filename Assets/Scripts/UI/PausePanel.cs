using Gunplay.Core;
using UnityEngine;
using UnityEngine.UI;

// PausePanel.cs —— 暂停界面：全屏遮罩压住战场，中心卡片提供「继续游戏 / 重新开始 / 回主菜单」
namespace Gunplay.UI
{
    /// <summary>暂停界面：半透明遮罩拦截战场点击，中心像素卡片提供继续、重新开始、回主菜单三个操作</summary>
    public sealed class PausePanel : UiPanelBase
    {
        // 当前关卡文字（显示「第 N 关」）
        private Text levelText;
        // 上次显示的关卡号（避免每帧重复赋值）
        private int lastLevel = -1;

        /// <summary>构建暂停界面（只执行一次）</summary>
        public override void Build()
        {
            RectTransform root = PanelRect;

            // 全屏半透明遮罩：alpha≈0.72，raycastTarget = true 压住下层战斗操作
            Image overlay = UiBuilder.NewImage("Overlay", root, new Color(0.02f, 0.025f, 0.04f, 0.72f), null, true);
            UiBuilder.Stretch(overlay.rectTransform);

            // 中心像素卡片（与其它界面同一套九宫格皮肤）
            Image card = UiBuilder.NewPixelCard("Card", root);
            UiBuilder.Anchor(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(720f, 560f));

            // 标题「暂停中」
            Text title = UiBuilder.NewText("Title", card.transform, "暂停中", UiTheme.FontH1, UiTheme.Gold);
            UiBuilder.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(640f, 100f));

            // 当前关卡小字（第 N 关）
            levelText = UiBuilder.NewText("LevelText", card.transform, "第 1 关", UiTheme.FontSmall, UiTheme.TextSecondary);
            UiBuilder.Anchor(levelText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -164f), new Vector2(640f, 48f));

            // 继续游戏（金色主按钮）
            Button resumeButton = UiBuilder.NewPixelButton("ResumeButton", card.transform, "继续游戏", UiTheme.Background, 40, OnResumeClicked);
            UiBuilder.Anchor((RectTransform)resumeButton.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -234f), new Vector2(520f, 96f));

            // 重新开始（次级按钮，重开整轮）
            Button restartButton = UiBuilder.NewPixelSecondaryButton("RestartButton", card.transform, "重新开始", UiTheme.TextPrimary, 38, OnRestartClicked);
            UiBuilder.Anchor((RectTransform)restartButton.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -354f), new Vector2(520f, 96f));

            // 回主菜单（次级按钮）
            Button menuButton = UiBuilder.NewPixelSecondaryButton("MenuButton", card.transform, "回主菜单", UiTheme.TextPrimary, 38, OnMenuClicked);
            UiBuilder.Anchor((RectTransform)menuButton.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -474f), new Vector2(520f, 96f));

            MarkBuilt();
        }

        /// <summary>刷新暂停界面（显示当前关卡号）</summary>
        public override void Refresh()
        {
            GameRuntime runtime = Runtime;
            if (runtime == null)
            {
                return;
            }

            RunState run = runtime.Run;
            if (run == null || levelText == null)
            {
                return;
            }

            if (lastLevel == run.level)
            {
                return;
            }

            lastLevel = run.level;
            levelText.text = "第 " + UiTheme.FormatInt(run.level) + " 关";
        }

        /// <summary>订阅游戏状态事件（用于暂停期间刷新关卡号）</summary>
        protected override void Subscribe(GameRuntime runtime)
        {
            runtime.Events.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        /// <summary>反注册游戏状态事件（与 Subscribe 成对）</summary>
        protected override void Unsubscribe(GameRuntime runtime)
        {
            runtime.Events.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        /// <summary>面板显示时立即刷新一次</summary>
        protected override void OnShown()
        {
            Refresh();
        }

        // 游戏状态变化：仅在暂停界面显示时刷新关卡号
        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            if (IsVisible)
            {
                Refresh();
            }
        }

        // 点击「继续游戏」：从暂停恢复战斗
        private void OnResumeClicked()
        {
            GameRuntime runtime = Runtime;
            if (runtime != null)
            {
                runtime.ResumeGame();
            }
        }

        // 点击「重新开始」：重开整轮（GameRuntime.RestartRun 内部会切回战斗状态）
        private void OnRestartClicked()
        {
            GameRuntime runtime = Runtime;
            if (runtime != null)
            {
                runtime.RestartRun();
            }
        }

        // 点击「回主菜单」：结束当前这一轮并回菜单
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
