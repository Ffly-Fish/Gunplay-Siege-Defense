using Gunplay.Core;
using UnityEngine;

// ComboSystem.cs —— 连杀计数、窗口衰减与金币倍率（手册第 4 章）
namespace Gunplay.Run
{
    /// <summary>连杀系统：击杀累积连杀，超时清零，按档位给金币倍率</summary>
    public sealed class ComboSystem
    {
        // 经济/连杀配置
        private readonly EconomyConfigData config;
        // 本轮状态
        private readonly RunState run;
        // 事件总线
        private readonly EventBus bus;
        // 词条系统（连杀窗口词条）
        private readonly UpgradeSystem upgrades;
        // 模拟时间累加（秒）
        private float time;

        /// <summary>当前连杀数</summary>
        public int Combo => run.combo;

        /// <summary>构造：注入配置、状态、事件总线与词条系统</summary>
        public ComboSystem(EconomyConfigData economyConfig, RunState runState, EventBus eventBus, UpgradeSystem upgradeSystem)
        {
            config = economyConfig;
            run = runState;
            bus = eventBus;
            upgrades = upgradeSystem;
        }

        /// <summary>连杀窗口时长（秒）：基础窗口 + 每级增量 × 关卡 + 词条加成</summary>
        public float GetWindow()
        {
            return config.comboWindow + config.comboWindowPerLevel * Mathf.Max(0, run.level - 1) + upgrades.GetComboWindowBonus();
        }

        /// <summary>当前连杀对应的金币倍率</summary>
        public float GetMultiplier()
        {
            float multiplier = 1f;
            if (config.comboTiers == null)
            {
                return multiplier;
            }

            for (int i = 0; i < config.comboTiers.Length; i++)
            {
                ComboTierData tier = config.comboTiers[i];
                if (run.combo >= tier.minCombo && tier.multiplier > multiplier)
                {
                    multiplier = tier.multiplier;
                }
            }

            return multiplier;
        }

        /// <summary>当前连杀对应的反馈等级（0–4，表现层用）</summary>
        public int GetFeedbackLevel()
        {
            int level = 0;
            if (config.comboTiers == null)
            {
                return level;
            }

            for (int i = 0; i < config.comboTiers.Length; i++)
            {
                ComboTierData tier = config.comboTiers[i];
                if (run.combo >= tier.minCombo && tier.feedbackLevel > level)
                {
                    level = tier.feedbackLevel;
                }
            }

            return level;
        }

        /// <summary>每帧推进：超过连杀窗口则清零</summary>
        public void Update(float deltaTime)
        {
            time += deltaTime;
            if (run.combo > 0 && time - run.lastKillTime > GetWindow())
            {
                run.combo = 0;
                PublishChanged();
            }
        }

        /// <summary>记录一次击杀：连杀 +1、刷新窗口、更新最高连杀</summary>
        public void OnKill()
        {
            run.combo++;
            run.lastKillTime = time;
            if (run.combo > run.bestCombo)
            {
                run.bestCombo = run.combo;
            }

            if (run.combo > run.currentLevelStats.bestCombo)
            {
                run.currentLevelStats.bestCombo = run.combo;
            }

            PublishChanged();
        }

        /// <summary>清空连杀（关间 / 重开时）</summary>
        public void Reset()
        {
            run.combo = 0;
            run.lastKillTime = -999f;
            PublishChanged();
        }

        /// <summary>当前模拟时间（秒）</summary>
        public float Now => time;

        // 广播连杀变化
        private void PublishChanged()
        {
            bus.Publish(new ComboChangedEvent
            {
                Combo = run.combo,
                Multiplier = GetMultiplier(),
                FeedbackLevel = GetFeedbackLevel()
            });
        }
    }
}
