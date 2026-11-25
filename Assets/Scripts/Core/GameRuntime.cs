using System;
using Gunplay.Combat;
using Gunplay.Run;
using Gunplay.View;
using Gunplay.Waves;
using UnityEngine;

// GameRuntime.cs —— 游戏总控：装配全部系统、驱动帧循环、管理状态机与入口调用
namespace Gunplay.Core
{
    /// <summary>游戏运行时总控（表现层 / UI 层唯一入口）</summary>
    public sealed class GameRuntime
    {
        // 当前实例（全局唯一）
        private static GameRuntime instance;

        // 配置数据库
        private ConfigDatabase config;
        // 本轮状态
        private RunState run;
        // 事件总线
        private EventBus bus;
        // 随机源
        private IRandomSource random;
        // 瞄准输入
        private IAimInput aimInput;
        // 存档
        private SaveSystem saveSystem;
        // 埋点
        private TrackSystem trackSystem;
        // 经济
        private EconomySystem economySystem;
        // 城墙
        private WallSystem wallSystem;
        // 词条
        private UpgradeSystem upgradeSystem;
        // 连杀
        private ComboSystem comboSystem;
        // 敌人
        private EnemySystem enemySystem;
        // 子弹
        private BulletSystem bulletSystem;
        // 武器
        private WeaponSystem weaponSystem;
        // 波次
        private WaveSystem waveSystem;
        // 商店
        private ShopSystem shopSystem;
        // 当前状态
        private GameState state = GameState.Menu;
        // 是否已初始化
        private bool initialized;
        // 已订阅关卡通过事件
        private Action<LevelClearedEvent> onLevelCleared;

        /// <summary>全局实例</summary>
        public static GameRuntime Instance => instance;

        /// <summary>配置数据库</summary>
        public ConfigDatabase Config => config;

        /// <summary>本轮状态（金币 / 关卡 / 武器 / 词条）</summary>
        public RunState Run => run;

        /// <summary>事件总线（表现层订阅用）</summary>
        public EventBus Events => bus;

        /// <summary>当前游戏状态</summary>
        public GameState State => state;

        /// <summary>存档统计</summary>
        public SavedStats Stats => saveSystem != null ? saveSystem.Stats : null;

        /// <summary>瞄准输入接口</summary>
        public IAimInput AimInput
        {
            get => aimInput;
            set => aimInput = value;
        }

        /// <summary>当前是否正在瞄准（仅战斗中有效）</summary>
        public bool IsAiming => state == GameState.Battle && aimInput != null && aimInput.IsAiming;

        /// <summary>瞄准点（世界坐标，未瞄准时为炮口上方）</summary>
        public Vector2 AimWorldPos => aimInput != null ? aimInput.AimWorldPos : (Vector2)BattleField.Muzzle + Vector2.up;

        /// <summary>全部活跃敌人（只读）</summary>
        public System.Collections.Generic.IReadOnlyList<Enemy> Enemies => enemySystem.All;

        /// <summary>全部活跃子弹（只读）</summary>
        public System.Collections.Generic.IReadOnlyList<Bullet> Bullets => bulletSystem.All;

        /// <summary>本帧激光束（只读）</summary>
        public System.Collections.Generic.IReadOnlyList<LaserShot> Lasers => weaponSystem.Lasers;

        /// <summary>波次系统（UI 读取进度）</summary>
        public WaveSystem Wave => waveSystem;

        /// <summary>商店系统（UI 读取奖励）</summary>
        public ShopSystem Shop => shopSystem;

        /// <summary>城墙系统（UI 读取城门耐久）</summary>
        public WallSystem Wall => wallSystem;

        /// <summary>武器系统（表现层读取换弹进度）</summary>
        public WeaponSystem Weapons => weaponSystem;

        /// <summary>初始化：装配全部系统（仅在启动时调用一次）</summary>
        public void Initialize(ConfigDatabase configDatabase, IAimInput aim, bool verboseTracking = false, int randomSeed = 0)
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            instance = this;
            config = configDatabase ?? throw new ArgumentNullException(nameof(configDatabase));
            aimInput = aim;
            random = randomSeed == 0 ? (IRandomSource)new SeededRandomSource(Environment.TickCount) : new SeededRandomSource(randomSeed);
            bus = new EventBus();
            run = new RunState();
            trackSystem = new TrackSystem(verboseTracking);
            saveSystem = new SaveSystem(config.Tuning.ads);
            upgradeSystem = new UpgradeSystem(config, run);
            wallSystem = new WallSystem(config.Tuning.gate, config.Tuning.combat, run, bus);
            comboSystem = new ComboSystem(config.Tuning.economy, run, bus, upgradeSystem);
            economySystem = new EconomySystem(config.Tuning.economy, run, bus, comboSystem);
            enemySystem = new EnemySystem(config, run, bus, comboSystem, economySystem, wallSystem);
            bulletSystem = new BulletSystem(config, run, bus, enemySystem, random);
            weaponSystem = new WeaponSystem(config, run, bus, bulletSystem, enemySystem, upgradeSystem, random);
            WaveSystem createdWave = new WaveSystem(config, run, bus, enemySystem, new DifficultySystem(config.Tuning.difficulty), economySystem, random);
            waveSystem = createdWave;
            shopSystem = new ShopSystem(config, run, bus, economySystem, wallSystem, upgradeSystem, random);

            onLevelCleared = HandleLevelCleared;
            bus.Subscribe(onLevelCleared);
            ApplyQualitySettings();
            SetState(GameState.Menu);
        }

        /// <summary>释放：清事件与实例（场景重载时调用）</summary>
        public void Dispose()
        {
            if (!initialized)
            {
                return;
            }

            if (onLevelCleared != null)
            {
                bus.Unsubscribe(onLevelCleared);
                onLevelCleared = null;
            }

            bus.Clear();
            enemySystem.ClearAll();
            bulletSystem.ClearAll();
            if (instance == this)
            {
                instance = null;
            }

            initialized = false;
        }

        /// <summary>每帧驱动（由 GameBootstrap 调用）</summary>
        public void Tick(float deltaTime)
        {
            if (!initialized)
            {
                return;
            }

            float dt = Mathf.Max(0f, deltaTime);
            if (state != GameState.Battle)
            {
                return;
            }

            run.currentLevelStats.duration += dt;
            Vector2 muzzle = BattleField.Muzzle;
            Vector2 aimPos = AimWorldPos;
            Vector2 direction = aimPos - muzzle;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.up;
            }

            direction.Normalize();
            bool aiming = IsAiming;
            weaponSystem.Update(dt, aiming, muzzle, direction, aimPos);
            bulletSystem.Update(dt);
            enemySystem.Update(dt);
            comboSystem.Update(dt);
            waveSystem.Update(dt);

            if (wallSystem.IsBroken)
            {
                HandleGameOver();
            }
        }

        /// <summary>开始新一轮（主菜单「开始」）</summary>
        public void StartNewRun()
        {
            if (!initialized)
            {
                return;
            }

            GateConfigData gate = config.Tuning.gate;
            WeaponConfigData starter = config.Weapons.Count > 0 ? config.Weapons[0] : null;
            if (starter == null)
            {
                Debug.LogError("[Game] 没有可用武器配置，无法开局");
                return;
            }

            run.ResetForNewRun(gate.initialMaxHp, starter.id, Mathf.Max(1, starter.magazine));
            wallSystem.InitializeForRun();
            weaponSystem.ResetForRun();
            enemiesClear();
            bulletSystem.ClearAll();
            waveSystem.StartLevel();
            trackSystem.Track("run_start");
            SetState(GameState.Battle);
        }

        /// <summary>结算页「重新开始」：直接开始新一轮</summary>
        public void RestartRun()
        {
            StartNewRun();
        }

        /// <summary>结算页「继续」：进入关间商店</summary>
        public void ContinueFromResult()
        {
            if (state != GameState.Result)
            {
                return;
            }

            shopSystem.Roll();
            SetState(GameState.Shop);
        }

        /// <summary>商店页「出征」：进入下一关</summary>
        public void ContinueFromShop()
        {
            if (state != GameState.Shop)
            {
                return;
            }

            run.StartNextLevel();
            wallSystem.SyncSlotsWithLevel();
            weaponSystem.RefillAll();
            enemiesClear();
            bulletSystem.ClearAll();
            waveSystem.StartLevel();
            trackSystem.Track("level_start", $"{{\"level\":{run.level}}}");
            SetState(GameState.Battle);
        }

        /// <summary>购买商店第 index 个奖励</summary>
        public bool BuyOffer(int index)
        {
            if (state != GameState.Shop)
            {
                return false;
            }

            bool bought = shopSystem.Purchase(index);
            if (bought)
            {
                trackSystem.Track("shop_buy", $"{{\"level\":{run.level},\"index\":{index}}}");
            }

            return bought;
        }

        /// <summary>付费重刷商店</summary>
        public bool RerollShop()
        {
            if (state != GameState.Shop || !shopSystem.CanReroll())
            {
                return false;
            }

            int cost = shopSystem.GetRerollCost();
            if (!economySystem.TrySpend(cost))
            {
                return false;
            }

            shopSystem.Reroll();
            trackSystem.Track("shop_reroll_paid", $"{{\"level\":{run.level},\"cost\":{cost}}}");
            return true;
        }

        /// <summary>看广告翻倍本关金币（每日次数限制）</summary>
        public bool WatchAdDoubleGold()
        {
            if (state != GameState.Result)
            {
                return false;
            }

            if (!saveSystem.TryConsumeGoldDouble())
            {
                return false;
            }

            int baseGold = run.currentLevelStats.goldEarned;
            economySystem.AddAdReward(baseGold, config.Tuning.ads.doubleGoldMultiplier);
            trackSystem.Track("ad_double_gold", $"{{\"level\":{run.level},\"base\":{baseGold}}}");
            return true;
        }

        /// <summary>看广告免费重刷（每日次数限制）</summary>
        public bool WatchAdFreeReroll()
        {
            if (state != GameState.Shop || !shopSystem.CanReroll())
            {
                return false;
            }

            if (!saveSystem.TryConsumeFreeReroll())
            {
                return false;
            }

            shopSystem.Reroll();
            trackSystem.Track("ad_free_reroll", $"{{\"level\":{run.level}}}");
            return true;
        }

        /// <summary>返回主菜单（清理战斗现场）</summary>
        public void ReturnToMenu()
        {
            if (!initialized)
            {
                return;
            }

            enemiesClear();
            bulletSystem.ClearAll();
            SetState(GameState.Menu);
        }

        /// <summary>切换音效开关并保存</summary>
        public void SetSoundOn(bool on)
        {
            saveSystem?.SetSoundOn(on);
        }

        /// <summary>埋点入口（UI 层可上报自定义事件）</summary>
        public void Track(string eventName, string payload = null)
        {
            trackSystem?.Track(eventName, payload);
        }

        // 关卡通过：记录进度并进入结算
        private void HandleLevelCleared(LevelClearedEvent evt)
        {
            saveSystem.RecordLevel(evt.Level);
            trackSystem.Track("level_clear", $"{{\"level\":{evt.Level},\"kills\":{run.currentLevelStats.kills}}}");
            enemiesClear();
            bulletSystem.ClearAll();
            SetState(GameState.Result);
        }

        // 城门被攻破：记录本轮并进入整轮结束
        private void HandleGameOver()
        {
            saveSystem.RecordRunEnd(run.level, run.bestCombo, run.totalKills);
            trackSystem.Track("run_end", $"{{\"level\":{run.level},\"kills\":{run.totalKills}}}");
            bulletSystem.ClearAll();
            SetState(GameState.GameOver);
        }

        // 清空敌人（封装以避免空引用）
        private void enemiesClear()
        {
            enemySystem?.ClearAll();
        }

        // 状态切换并广播
        private void SetState(GameState next)
        {
            if (state == next)
            {
                return;
            }

            GameState previous = state;
            state = next;
            bus.Publish(new GameStateChangedEvent { Previous = previous, Current = next });
        }

        // 基础运行时设置：竖屏锁定与帧率
        private static void ApplyQualitySettings()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
