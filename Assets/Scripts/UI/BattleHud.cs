using System.Collections;
using System.Collections.Generic;
using Gunplay.Combat;
using Gunplay.Core;
using UnityEngine;
using UnityEngine.UI;

// BattleHud.cs —— 战斗 HUD：城门耐久、关卡波次、金币、连杀、Boss 血条、武器槽
namespace Gunplay.UI
{
    /// <summary>战斗 HUD：顶部城门耐久条与关卡波次、右上金币、连杀反馈、Boss 血条、底部武器槽</summary>
    public sealed class BattleHud : UiPanelBase
    {
        // 城门耐久条
        private UiBar gateBar;
        // 城门耐久文字
        private Text gateText;
        // 关卡 / 波次文字
        private Text waveText;
        // 金币文字
        private Text goldText;
        // Boss 分组根节点
        private RectTransform bossGroup;
        // Boss 名称文字
        private Text bossNameText;
        // Boss 血量文字
        private Text bossHpText;
        // Boss 血条
        private UiBar bossBar;
        // 连杀分组根节点
        private RectTransform comboGroup;
        // 连杀数文字
        private Text comboText;
        // 连杀倍率文字
        private Text multiplierText;
        // 武器槽最大数量（7 把武器 + 1 个余量；已取消装备槽上限，拥有即全部同时开火）
        private const int MaxWeaponChips = 8;
        // 每行武器槽数量（超出换行显示）
        private const int WeaponChipsPerRow = 4;
        // 底部武器槽底图（按已拥有武器动态显示）
        private readonly List<Image> weaponChips = new List<Image>();
        // 底部武器槽文字
        private readonly List<Text> weaponLabels = new List<Text>();

        // 当前追踪的 Boss（null = 场上没有 Boss）
        private Enemy trackedBoss;
        // Boss 血量轮询计时（0.1 秒一次）
        private float bossPollTimer;
        // 连杀缩放动画协程
        private Coroutine comboPopRoutine;
        // 上次显示的金币
        private int lastGold = -1;
        // 上次显示的城门耐久
        private int lastGateHp = -1;
        // 上次显示的城门耐久上限
        private int lastGateMaxHp = -1;
        // 上次显示的连杀数
        private int lastCombo = -1;
        // 上次显示的连杀反馈等级
        private int lastComboLevel = -1;
        // 上次显示的倍率（百分比取整）
        private int lastMultiplierPercent = -1;
        // 上次显示的关卡
        private int lastLevel = -1;
        // 上次显示的波序号
        private int lastWave = -1;
        // 上次显示的本关总波数
        private int lastWaveCount = -1;
        // 上次显示的 Boss 血量
        private int lastBossHp = -1;
        // 上次显示的 Boss 血量上限
        private int lastBossMaxHp = -1;

        /// <summary>构建战斗 HUD 界面（只执行一次）</summary>
        public override void Build()
        {
            RectTransform root = PanelRect;

            // 城门耐久条（贴安全区顶部）
            gateBar = UiBuilder.NewPixelBar(root, "GateBar", UiTheme.Success, 940f, 40f);
            UiBuilder.Anchor(gateBar.Background.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(940f, 40f));
            gateText = UiBuilder.NewText("GateText", gateBar.Background.transform, "城门 100 / 100", UiTheme.FontTiny, UiTheme.TextPrimary);
            UiBuilder.Stretch(gateText.rectTransform);

            // 关卡 / 波次（左上）
            waveText = UiBuilder.NewText("WaveText", root, "第 1 关 · 第 1/5 波", UiTheme.FontSmall, UiTheme.TextPrimary, TextAnchor.MiddleLeft);
            UiBuilder.Anchor(waveText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f, -74f), new Vector2(640f, 48f));

            // 金币（右上，图标 + 数字）
            RectTransform coinRoot = UiBuilder.NewRect("CoinRoot", root);
            UiBuilder.Anchor(coinRoot, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-48f, -74f), new Vector2(420f, 48f));
            goldText = UiBuilder.NewText("GoldText", coinRoot, "金币 0", UiTheme.FontBody, UiTheme.Gold, TextAnchor.MiddleRight);
            UiBuilder.Stretch(goldText.rectTransform);

            // Boss 血条（默认隐藏，出现 Boss 时显示）
            bossGroup = UiBuilder.NewRect("BossGroup", root);
            UiBuilder.Anchor(bossGroup, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -136f), new Vector2(940f, 96f));
            bossNameText = UiBuilder.NewText("BossName", bossGroup, "Boss 攻城兽", UiTheme.FontTiny, UiTheme.Danger, TextAnchor.MiddleLeft);
            UiBuilder.Anchor(bossNameText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(600f, 32f));
            bossHpText = UiBuilder.NewText("BossHp", bossGroup, "0 / 0", UiTheme.FontTiny, UiTheme.TextSecondary, TextAnchor.MiddleRight);
            UiBuilder.Anchor(bossHpText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(340f, 32f));
            bossBar = UiBuilder.NewPixelBar(bossGroup, "BossBar", UiTheme.Danger, 940f, 30f);
            UiBuilder.Anchor(bossBar.Background.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(940f, 30f));
            bossGroup.gameObject.SetActive(false);

            // 连杀提示（屏幕上方，半透明覆盖，不遮挡准星区域）
            comboGroup = UiBuilder.NewRect("ComboGroup", root);
            UiBuilder.Anchor(comboGroup, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 430f), new Vector2(900f, 200f));
            comboText = UiBuilder.NewText("ComboText", comboGroup, "0 连杀", 88, UiTheme.Gold);
            UiBuilder.Anchor(comboText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 24f), new Vector2(900f, 120f));
            multiplierText = UiBuilder.NewText("MultiplierText", comboGroup, "金币 ×1.0", UiTheme.FontSmall, UiTheme.TextPrimary);
            UiBuilder.Anchor(multiplierText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -64f), new Vector2(900f, 48f));
            comboGroup.gameObject.SetActive(false);

            // 底部武器槽（拥有几把就显示几个，每行 4 个，超过换行）
            for (int i = 0; i < MaxWeaponChips; i++)
            {
                int row = i / WeaponChipsPerRow;
                int column = i % WeaponChipsPerRow;
                float x = -300f + column * 200f;
                float y = 200f + row * 68f;
                Image chip = UiBuilder.NewPixelPanel("WeaponChip" + i, root, UiPixelArt.Slot, new Color(1f, 1f, 1f, 0.85f));
                UiBuilder.Anchor(chip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(x, y), new Vector2(184f, 56f));
                Text chipLabel = UiBuilder.NewText("Label", chip.transform, "", UiTheme.FontTiny, UiTheme.TextPrimary);
                UiBuilder.Stretch(chipLabel.rectTransform, 8f, 4f, 8f, 4f);
                chip.gameObject.SetActive(false);
                weaponChips.Add(chip);
                weaponLabels.Add(chipLabel);
            }

            // 左下角测试按钮：暂停（锚点左下，偏移 24,96，尺寸 168×64）
            Button pauseButton = UiBuilder.NewPixelSecondaryButton("PauseButton", root, "暂停", UiTheme.TextPrimary, 30, OnPauseClicked);
            UiBuilder.Anchor((RectTransform)pauseButton.transform, Vector2.zero, Vector2.zero, new Vector2(24f, 96f), new Vector2(168f, 64f));

            // 左下角测试按钮：一键通关（锚点左下，偏移 24,20，尺寸 168×64）
            Button skipButton = UiBuilder.NewPixelSecondaryButton("SkipButton", root, "一键通关", UiTheme.TextPrimary, 30, OnSkipLevelClicked);
            UiBuilder.Anchor((RectTransform)skipButton.transform, Vector2.zero, Vector2.zero, new Vector2(24f, 20f), new Vector2(168f, 64f));

            MarkBuilt();
        }

        /// <summary>刷新 HUD（进入战斗 / 外部强制刷新时调用）</summary>
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

            UpdateGold(run.gold);
            UpdateWave(run.level, run.wave, ResolveWaveCount(runtime));
            UpdateGate(run.gateHp, run.gateMaxHp);
            UpdateCombo(run.combo, GetComboMultiplier(runtime, run.combo), 0, false);
            RefreshWeapons(runtime, run);
            RefreshBoss();
        }

        /// <summary>订阅战斗相关事件</summary>
        protected override void Subscribe(GameRuntime runtime)
        {
            EventBus events = runtime.Events;
            events.Subscribe<GoldChangedEvent>(OnGoldChanged);
            events.Subscribe<WaveStartedEvent>(OnWaveStarted);
            events.Subscribe<GateDamagedEvent>(OnGateDamaged);
            events.Subscribe<ComboChangedEvent>(OnComboChanged);
            events.Subscribe<EnemySpawnedEvent>(OnEnemySpawned);
            events.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
            events.Subscribe<LevelClearedEvent>(OnLevelCleared);
        }

        /// <summary>反注册战斗相关事件</summary>
        protected override void Unsubscribe(GameRuntime runtime)
        {
            EventBus events = runtime.Events;
            events.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
            events.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
            events.Unsubscribe<GateDamagedEvent>(OnGateDamaged);
            events.Unsubscribe<ComboChangedEvent>(OnComboChanged);
            events.Unsubscribe<EnemySpawnedEvent>(OnEnemySpawned);
            events.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
            events.Unsubscribe<LevelClearedEvent>(OnLevelCleared);
        }

        /// <summary>每帧轮询 Boss 血量（只在显示且已绑定时执行）</summary>
        protected override void OnTick(float deltaTime)
        {
            bossPollTimer += deltaTime;
            if (bossPollTimer < 0.1f)
            {
                return;
            }

            bossPollTimer = 0f;
            if (trackedBoss != null && !trackedBoss.Alive)
            {
                SetBossVisible(false);
            }

            if (trackedBoss == null)
            {
                Enemy boss = FindBoss();
                if (boss == null)
                {
                    if (bossGroup.gameObject.activeSelf)
                    {
                        SetBossVisible(false);
                    }

                    return;
                }

                trackedBoss = boss;
                SetBossVisible(true);
                ApplyBossName(boss);
                UpdateBossHp(boss.Hp, boss.MaxHp);
                return;
            }

            UpdateBossHp(trackedBoss.Hp, trackedBoss.MaxHp);
        }

        // 点击「暂停」：切到暂停状态（GameRuntime.Tick 在非 Battle 状态冻结）
        private void OnPauseClicked()
        {
            GameRuntime runtime = Runtime;
            if (runtime != null)
            {
                runtime.PauseGame();
            }
        }

        // 点击「一键通关」：直接判定本关通过并进入结算（测试 / 调试用）
        private void OnSkipLevelClicked()
        {
            GameRuntime runtime = Runtime;
            if (runtime != null)
            {
                runtime.SkipCurrentLevel();
            }
        }

        // 金币变化
        private void OnGoldChanged(GoldChangedEvent evt)
        {
            UpdateGold(evt.Gold);
        }

        // 新一波开始
        private void OnWaveStarted(WaveStartedEvent evt)
        {
            UpdateWave(evt.Level, evt.Wave, evt.WaveCount);
        }

        // 城门受击
        private void OnGateDamaged(GateDamagedEvent evt)
        {
            UpdateGate(evt.GateHp, evt.GateMaxHp);
        }

        // 连杀变化：刷新文字并播放缩放动画
        private void OnComboChanged(ComboChangedEvent evt)
        {
            UpdateCombo(evt.Combo, evt.Multiplier, evt.FeedbackLevel, true);
        }

        // 敌人出生：Boss 出现时立即显示血条
        private void OnEnemySpawned(EnemySpawnedEvent evt)
        {
            if (!evt.IsBoss)
            {
                return;
            }

            RefreshBoss();
        }

        // 敌人被击杀：Boss 死亡时隐藏血条
        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            if (trackedBoss != null && trackedBoss.Id == evt.Id)
            {
                SetBossVisible(false);
            }
        }

        // 本关通过：隐藏 Boss 血条
        private void OnLevelCleared(LevelClearedEvent evt)
        {
            SetBossVisible(false);
        }

        // 更新金币显示（值未变化时跳过）
        private void UpdateGold(int gold)
        {
            if (gold == lastGold)
            {
                return;
            }

            lastGold = gold;
            goldText.text = "金币 " + UiTheme.FormatInt(gold);
        }

        // 更新关卡 / 波次显示（值未变化时跳过）
        private void UpdateWave(int level, int wave, int waveCount)
        {
            if (level == lastLevel && wave == lastWave && waveCount == lastWaveCount)
            {
                return;
            }

            lastLevel = level;
            lastWave = wave;
            lastWaveCount = waveCount;
            waveText.text = "第 " + level + " 关 · 第 " + wave + "/" + waveCount + " 波";
        }

        // 更新城门耐久条与文字
        private void UpdateGate(float hp, float maxHp)
        {
            float safeMax = Mathf.Max(1f, maxHp);
            float ratio = Mathf.Clamp01(hp / safeMax);
            gateBar.SetRatio(ratio);
            gateBar.Fill.color = UiTheme.HealthColor(ratio);

            int hpInt = Mathf.CeilToInt(Mathf.Max(0f, hp));
            int maxInt = Mathf.CeilToInt(safeMax);
            if (hpInt == lastGateHp && maxInt == lastGateMaxHp)
            {
                return;
            }

            lastGateHp = hpInt;
            lastGateMaxHp = maxInt;
            gateText.text = "城门 " + hpInt + " / " + maxInt;
        }

        // 更新连杀文字、倍率、颜色，并按需播放缩放动画
        private void UpdateCombo(int combo, float multiplier, int feedbackLevel, bool animate)
        {
            if (combo <= 0)
            {
                lastCombo = 0;
                lastMultiplierPercent = -1;
                if (comboGroup.gameObject.activeSelf)
                {
                    comboGroup.gameObject.SetActive(false);
                }

                return;
            }

            if (!comboGroup.gameObject.activeSelf)
            {
                comboGroup.gameObject.SetActive(true);
            }

            if (combo != lastCombo)
            {
                lastCombo = combo;
                comboText.text = combo + " 连杀";
            }

            int multiplierPercent = Mathf.RoundToInt(multiplier * 100f);
            if (multiplierPercent != lastMultiplierPercent)
            {
                lastMultiplierPercent = multiplierPercent;
                multiplierText.text = "金币 ×" + (multiplierPercent / 100f).ToString("0.0");
            }

            if (feedbackLevel != lastComboLevel)
            {
                lastComboLevel = feedbackLevel;
                comboText.color = UiTheme.ComboColor(feedbackLevel);
            }

            if (animate)
            {
                PlayComboPop();
            }
        }

        // 刷新底部武器槽（按已装备武器显示名称）
        private void RefreshWeapons(GameRuntime runtime, RunState run)
        {
            for (int i = 0; i < weaponChips.Count; i++)
            {
                bool active = i < run.equippedWeapons.Count;
                if (weaponChips[i].gameObject.activeSelf != active)
                {
                    weaponChips[i].gameObject.SetActive(active);
                }

                if (!active)
                {
                    continue;
                }

                string weaponId = run.equippedWeapons[i];
                WeaponConfigData config = null;
                if (runtime.Config != null)
                {
                    runtime.Config.TryGetWeapon(weaponId, out config);
                }

                string displayName = config != null ? config.displayName : weaponId;
                if (weaponLabels[i].text != displayName)
                {
                    weaponLabels[i].text = displayName;
                }
            }
        }

        // 扫描当前敌人列表，找存活的 Boss
        private static Enemy FindBoss()
        {
            GameRuntime runtime = GameRuntime.Instance;
            if (runtime == null)
            {
                return null;
            }

            IReadOnlyList<Enemy> enemies = runtime.Enemies;
            if (enemies == null)
            {
                return null;
            }

            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy != null && enemy.IsBoss && enemy.Alive)
                {
                    return enemy;
                }
            }

            return null;
        }

        // 重新扫描并刷新 Boss 血条显示
        private void RefreshBoss()
        {
            Enemy boss = FindBoss();
            if (boss == null)
            {
                SetBossVisible(false);
                return;
            }

            trackedBoss = boss;
            SetBossVisible(true);
            ApplyBossName(boss);
            UpdateBossHp(boss.Hp, boss.MaxHp);
        }

        // 更新 Boss 名称文字
        private void ApplyBossName(Enemy boss)
        {
            string displayName = boss.Config != null ? boss.Config.displayName : "Boss";
            if (bossNameText.text != displayName)
            {
                bossNameText.text = displayName;
            }
        }

        // 更新 Boss 血条与血量文字
        private void UpdateBossHp(float hp, float maxHp)
        {
            float safeMax = Mathf.Max(1f, maxHp);
            bossBar.SetRatio(Mathf.Clamp01(hp / safeMax));

            int hpInt = Mathf.CeilToInt(Mathf.Max(0f, hp));
            int maxInt = Mathf.CeilToInt(safeMax);
            if (hpInt == lastBossHp && maxInt == lastBossMaxHp)
            {
                return;
            }

            lastBossHp = hpInt;
            lastBossMaxHp = maxInt;
            bossHpText.text = hpInt + " / " + maxInt;
        }

        // 显示 / 隐藏 Boss 血条
        private void SetBossVisible(bool visible)
        {
            if (bossGroup.gameObject.activeSelf != visible)
            {
                bossGroup.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                trackedBoss = null;
                lastBossHp = -1;
                lastBossMaxHp = -1;
            }
        }

        // 取本关总波数（优先读波次系统，读不到时回退配置）
        private static int ResolveWaveCount(GameRuntime runtime)
        {
            if (runtime.Wave != null && runtime.Wave.WaveCount > 0)
            {
                return runtime.Wave.WaveCount;
            }

            TuningConfigData tuning = runtime.Config != null ? runtime.Config.Tuning : null;
            if (tuning != null && tuning.waves != null)
            {
                return tuning.waves.wavesPerLevel;
            }

            return 0;
        }

        // 按连杀数从配置档位取金币倍率
        private static float GetComboMultiplier(GameRuntime runtime, int combo)
        {
            TuningConfigData tuning = runtime.Config != null ? runtime.Config.Tuning : null;
            if (tuning == null || tuning.economy == null || tuning.economy.comboTiers == null)
            {
                return 1f;
            }

            ComboTierData[] tiers = tuning.economy.comboTiers;
            float multiplier = 1f;
            for (int i = 0; i < tiers.Length; i++)
            {
                if (combo >= tiers[i].minCombo)
                {
                    multiplier = tiers[i].multiplier;
                }
            }

            return multiplier;
        }

        // 连杀数字弹跳动画（新事件到来时重新开始）
        private void PlayComboPop()
        {
            if (!IsVisible)
            {
                return;
            }

            if (comboPopRoutine != null)
            {
                StopCoroutine(comboPopRoutine);
            }

            comboPopRoutine = StartCoroutine(ComboPopRoutine());
        }

        // 连杀缩放动画：1.4 → 1，0.18 秒回弹
        private IEnumerator ComboPopRoutine()
        {
            const float duration = 0.18f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float scale = Mathf.Lerp(1.4f, 1f, progress * progress);
                comboGroup.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            comboGroup.localScale = Vector3.one;
            comboPopRoutine = null;
        }
    }
}
