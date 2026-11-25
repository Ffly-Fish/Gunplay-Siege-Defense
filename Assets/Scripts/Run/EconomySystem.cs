using Gunplay.Core;
using UnityEngine;

// EconomySystem.cs —— 金币获取与消费（击杀金币受连杀倍率影响）
namespace Gunplay.Run
{
    /// <summary>经济系统：金币的唯一出入口</summary>
    public sealed class EconomySystem
    {
        // 经济配置
        private readonly EconomyConfigData config;
        // 本轮状态
        private readonly RunState run;
        // 事件总线
        private readonly EventBus bus;
        // 连杀系统（取金币倍率）
        private readonly ComboSystem combo;

        /// <summary>构造：注入配置、状态、事件总线与连杀系统</summary>
        public EconomySystem(EconomyConfigData economyConfig, RunState runState, EventBus eventBus, ComboSystem comboSystem)
        {
            config = economyConfig;
            run = runState;
            bus = eventBus;
            combo = comboSystem;
        }

        /// <summary>增加金币并广播变化，返回实际获得值</summary>
        public int AddGold(int amount)
        {
            if (amount == 0)
            {
                return 0;
            }

            run.gold += amount;
            if (amount > 0)
            {
                run.currentLevelStats.goldEarned += amount;
            }

            bus.Publish(new GoldChangedEvent { Gold = run.gold, Delta = amount });
            return amount;
        }

        /// <summary>按连杀倍率结算一枚击杀金币</summary>
        public void AddKillGold(int baseGold)
        {
            int gained = Mathf.RoundToInt(baseGold * combo.GetMultiplier());
            AddGold(Mathf.Max(1, gained));
        }

        /// <summary>清波奖励：波序号 × 每波奖励</summary>
        public void AddWaveClearGold(int wave)
        {
            AddGold(Mathf.Max(1, wave * config.waveClearGoldPerWave));
        }

        /// <summary>通关奖励：基础值 + 每关增量 × 关卡</summary>
        public void AddLevelClearGold(int level)
        {
            AddGold(config.levelClearGoldBase + config.levelClearGoldPerLevel * Mathf.Max(1, level));
        }

        /// <summary>尝试消费金币（不足返回 false）</summary>
        public bool TrySpend(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (run.gold < amount)
            {
                return false;
            }

            run.gold -= amount;
            bus.Publish(new GoldChangedEvent { Gold = run.gold, Delta = -amount });
            return true;
        }

        /// <summary>模拟广告：把指定金额按倍率补发一份（结算页金币 ×2 使用）</summary>
        public int AddAdReward(int baseAmount, float multiplier)
        {
            int extra = Mathf.RoundToInt(Mathf.Max(0, baseAmount) * Mathf.Max(0f, multiplier - 1f));
            if (extra <= 0)
            {
                return 0;
            }

            AddGold(extra);
            return extra;
        }
    }
}
