using Gunplay.Core;
using UnityEngine;

// DifficultySystem.cs —— 无限关卡的难度系数（手册第 6 章）
namespace Gunplay.Waves
{
    /// <summary>按关卡计算敌人血量 / 速度 / 额外难度系数</summary>
    public sealed class DifficultySystem
    {
        // 数值配置
        private readonly DifficultyConfigData config;

        /// <summary>构造：注入难度配置</summary>
        public DifficultySystem(DifficultyConfigData difficultyConfig)
        {
            config = difficultyConfig;
        }

        /// <summary>基础血量系数：1 + baseHpPerLevel × (L-1)</summary>
        public float GetBaseHpMultiplier(int level)
        {
            return 1f + config.baseHpPerLevel * Mathf.Max(0, level - 1);
        }

        /// <summary>额外难度系数：extraHpPow ^ (L-1)</summary>
        public float GetExtraHpMultiplier(int level)
        {
            return Mathf.Pow(config.extraHpPow, Mathf.Max(0, level - 1));
        }

        /// <summary>血量总系数（受 hpCap 钳制）</summary>
        public float GetHpMultiplier(int level)
        {
            float value = GetBaseHpMultiplier(level) * GetExtraHpMultiplier(level);
            return Mathf.Min(value, config.hpCap);
        }

        /// <summary>速度系数：1 + speedPerLevel × (L-1)</summary>
        public float GetSpeedMultiplier(int level)
        {
            return 1f + config.speedPerLevel * Mathf.Max(0, level - 1);
        }
    }
}
