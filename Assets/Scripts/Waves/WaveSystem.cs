using Gunplay.Combat;
using Gunplay.Core;
using Gunplay.Run;
using UnityEngine;

// WaveSystem.cs —— 关卡内的波次投放（数量公式、敌人权重、Boss 波与清波奖励）
namespace Gunplay.Waves
{
    /// <summary>波次系统：按关卡生成波次计划并逐个投放敌人</summary>
    public sealed class WaveSystem
    {
        // 配置库
        private readonly ConfigDatabase config;
        // 本轮状态
        private readonly RunState run;
        // 事件总线
        private readonly EventBus bus;
        // 敌人系统
        private readonly EnemySystem enemies;
        // 难度系统（血量 / 速度系数）
        private readonly DifficultySystem difficulty;
        // 经济系统（清波 / 通关奖励）
        private readonly EconomySystem economy;
        // 随机源
        private readonly IRandomSource random;
        // 本关波数
        private int waveCount = 1;
        // 当前波序号（0 = 尚未开始）
        private int currentWave;
        // 当前波计划投放数量
        private int waveTotal;
        // 当前波已投放数量
        private int waveSpawned;
        // 刷怪计时
        private float spawnTimer;
        // 波间空档计时
        private float gapTimer;
        // 当前波是否进行中
        private bool waveActive;
        // 本关是否已结束
        private bool levelFinished;

        /// <summary>当前波序号（1 起）</summary>
        public int CurrentWave => currentWave;

        /// <summary>本关总波数</summary>
        public int WaveCount => waveCount;

        /// <summary>当前波计划敌人总数</summary>
        public int TotalEnemies => waveTotal;

        /// <summary>当前波已投放数量</summary>
        public int SpawnedCount => waveSpawned;

        /// <summary>场上存活敌人数量</summary>
        public int AliveCount => enemies.AliveCount;

        /// <summary>当前波是否进行中</summary>
        public bool WaveActive => waveActive;

        /// <summary>当前波是否为 Boss 波</summary>
        public bool IsBossWave { get; private set; }

        /// <summary>本关是否已打完</summary>
        public bool LevelFinished => levelFinished;

        /// <summary>构造：注入依赖</summary>
        public WaveSystem(ConfigDatabase configDatabase, RunState runState, EventBus eventBus, EnemySystem enemySystem,
            DifficultySystem difficultySystem, EconomySystem economySystem, IRandomSource randomSource)
        {
            config = configDatabase;
            run = runState;
            bus = eventBus;
            enemies = enemySystem;
            difficulty = difficultySystem;
            economy = economySystem;
            random = randomSource;
        }

        /// <summary>开始新一关：重置波次并准备第 1 波</summary>
        public void StartLevel()
        {
            levelFinished = false;
            currentWave = 0;
            waveActive = false;
            waveSpawned = 0;
            waveTotal = 0;
            gapTimer = 0.6f;
            int bossEvery = config.Tuning.waves.bossEveryLevels;
            waveCount = Mathf.Max(1, config.Tuning.waves.wavesPerLevel);
            if (bossEvery > 0 && run.level % bossEvery == 0)
            {
                waveCount = Mathf.Max(1, config.Tuning.waves.bossWaveIndex > 0 ? config.Tuning.waves.bossWaveIndex : waveCount);
            }
        }

        /// <summary>每帧推进：波间空档 → 逐个投放 → 清场后结算</summary>
        public void Update(float deltaTime)
        {
            if (levelFinished)
            {
                return;
            }

            if (!waveActive)
            {
                gapTimer -= deltaTime;
                if (gapTimer <= 0f)
                {
                    StartNextWave();
                }

                return;
            }

            if (waveSpawned < waveTotal)
            {
                spawnTimer -= deltaTime;
                if (spawnTimer <= 0f)
                {
                    SpawnOne();
                    WaveConfigData waves = config.Tuning.waves;
                    spawnTimer = random.Range(waves.spawnIntervalMin, Mathf.Max(waves.spawnIntervalMin, waves.spawnIntervalMax));
                }

                return;
            }

            if (enemies.AliveCount == 0)
            {
                FinishWave();
            }
        }

        /// <summary>清空波次状态（重开 / 回主菜单）</summary>
        public void Reset()
        {
            levelFinished = true;
            waveActive = false;
            currentWave = 0;
            waveSpawned = 0;
            waveTotal = 0;
        }

        // 开始下一波
        private void StartNextWave()
        {
            currentWave++;
            waveSpawned = 0;
            waveActive = true;
            IsBossWave = IsBossLevel() && currentWave >= waveCount;
            waveTotal = CalculateWaveCount(run.level, currentWave, IsBossWave);
            spawnTimer = 0f;

            bus.Publish(new WaveStartedEvent
            {
                Level = run.level,
                Wave = currentWave,
                WaveCount = waveCount,
                BossWave = IsBossWave
            });
        }

        // 清波结算：奖励 → 判断本关是否结束
        private void FinishWave()
        {
            waveActive = false;
            int reward = Mathf.Max(1, currentWave * config.Tuning.economy.waveClearGoldPerWave);
            economy.AddWaveClearGold(currentWave);
            bus.Publish(new WaveClearedEvent { Level = run.level, Wave = currentWave, Reward = reward });

            if (currentWave >= waveCount)
            {
                levelFinished = true;
                int levelReward = config.Tuning.economy.levelClearGoldBase +
                                  config.Tuning.economy.levelClearGoldPerLevel * Mathf.Max(1, run.level);
                economy.AddLevelClearGold(run.level);
                bus.Publish(new LevelClearedEvent
                {
                    Level = run.level,
                    Reward = levelReward,
                    Stats = run.currentLevelStats
                });
                return;
            }

            gapTimer = Mathf.Max(0.2f, config.Tuning.waves.waveGap);
        }

        // 本关是否为 Boss 关（每 bossEveryLevels 关）
        private bool IsBossLevel()
        {
            int bossEvery = config.Tuning.waves.bossEveryLevels;
            return bossEvery > 0 && run.level % bossEvery == 0;
        }

        // 波内敌人数量：baseCount + perWaveCount × 波序号 + floor(关卡 / levelCountDivisor)
        private int CalculateWaveCount(int level, int wave, bool bossWave)
        {
            WaveConfigData waves = config.Tuning.waves;
            int divisor = Mathf.Max(1, waves.levelCountDivisor);
            int count = waves.baseCount + waves.perWaveCount * wave + level / divisor;
            if (bossWave)
            {
                // Boss 波：1 只 Boss + 约一半数量的小兵
                return 1 + Mathf.Max(2, count / 2);
            }

            return Mathf.Max(1, count);
        }

        // 投放一个敌人（Boss 波第一个一定是 Boss）
        private void SpawnOne()
        {
            waveSpawned++;
            EnemyConfigData chosen = IsBossWave && waveSpawned == 1 ? FindBossConfig() : PickEnemyByWeight();
            if (chosen == null)
            {
                return;
            }

            float xRange = Mathf.Max(0.5f, config.Tuning.waves.spawnXRange);
            float x = chosen.bossPhases > 0 ? 0f : random.Range(-xRange, xRange);
            Vector2 position = new Vector2(x, BattleField.SpawnLineY);
            enemies.Spawn(chosen, difficulty.GetHpMultiplier(run.level), difficulty.GetSpeedMultiplier(run.level), position);
        }

        // 取 Boss 配置
        private EnemyConfigData FindBossConfig()
        {
            for (int i = 0; i < config.Enemies.Count; i++)
            {
                if (config.Enemies[i].bossPhases > 0)
                {
                    return config.Enemies[i];
                }
            }

            return null;
        }

        // 按权重抽取当前波可出现的敌人（权重 = weightBase + weightGrowth × (波序号-1)）
        private EnemyConfigData PickEnemyByWeight()
        {
            float totalWeight = 0f;
            for (int i = 0; i < config.Enemies.Count; i++)
            {
                EnemyConfigData enemy = config.Enemies[i];
                if (enemy.bossPhases > 0 || enemy.minWave > currentWave)
                {
                    continue;
                }

                totalWeight += Mathf.Max(0f, enemy.weightBase + enemy.weightGrowth * Mathf.Max(0, currentWave - 1));
            }

            if (totalWeight <= 0f)
            {
                return config.Enemies.Count > 0 ? config.Enemies[0] : null;
            }

            float roll = random.Range(0f, totalWeight);
            for (int i = 0; i < config.Enemies.Count; i++)
            {
                EnemyConfigData enemy = config.Enemies[i];
                if (enemy.bossPhases > 0 || enemy.minWave > currentWave)
                {
                    continue;
                }

                float weight = Mathf.Max(0f, enemy.weightBase + enemy.weightGrowth * Mathf.Max(0, currentWave - 1));
                if (roll <= weight)
                {
                    return enemy;
                }

                roll -= weight;
            }

            return config.Enemies[0];
        }
    }
}
