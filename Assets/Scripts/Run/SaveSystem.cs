using System;
using Gunplay.Core;
using UnityEngine;

// SaveSystem.cs —— 跨轮存档（只存统计与设置，符合手册「不存档局内成长」要求）
namespace Gunplay.Run
{
    /// <summary>跨轮持久统计与设置</summary>
    [Serializable]
    public class SavedStats
    {
        /// <summary>历史最高关卡</summary>
        public int bestLevel = 1;
        /// <summary>历史最高连杀</summary>
        public int bestCombo;
        /// <summary>累计击杀</summary>
        public int totalKills;
        /// <summary>累计游玩轮数</summary>
        public int totalRuns;
        /// <summary>今日剩余金币翻倍次数</summary>
        public int goldDoubleLeft;
        /// <summary>今日剩余免费重刷次数</summary>
        public int freeRerollLeft;
        /// <summary>上次重置每日次数日期（yyyyMMdd）</summary>
        public string lastDailyDate = string.Empty;
        /// <summary>是否开启音效</summary>
        public bool soundOn = true;
    }

    /// <summary>存档系统：PlayerPrefs + JSON（抖音小游戏 WebGL 同样可用）</summary>
    public sealed class SaveSystem
    {
        // 存档键
        private const string SAVE_KEY = "gunplay_save_v1";
        // 广告配置（每日次数）
        private readonly AdsConfigData adsConfig;
        // 当前统计
        private SavedStats stats;

        /// <summary>当前统计（只读）</summary>
        public SavedStats Stats => stats;

        /// <summary>构造：加载存档并重置每日次数</summary>
        public SaveSystem(AdsConfigData ads)
        {
            adsConfig = ads;
            Load();
        }

        /// <summary>加载存档（不存在或损坏时使用默认值）</summary>
        public void Load()
        {
            string json = PlayerPrefs.GetString(SAVE_KEY, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                stats = new SavedStats();
            }
            else
            {
                try
                {
                    stats = JsonUtility.FromJson<SavedStats>(json) ?? new SavedStats();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[Save] 存档解析失败，使用默认值: {exception.Message}");
                    stats = new SavedStats();
                }
            }

            ResetDailyCountersIfNeeded();
        }

        /// <summary>写盘</summary>
        public void Save()
        {
            PlayerPrefs.SetString(SAVE_KEY, JsonUtility.ToJson(stats));
            PlayerPrefs.Save();
        }

        /// <summary>更新最高关卡</summary>
        public void RecordLevel(int level)
        {
            if (level > stats.bestLevel)
            {
                stats.bestLevel = level;
            }

            Save();
        }

        /// <summary>一轮结束：累计轮数、击杀与最高连杀</summary>
        public void RecordRunEnd(int level, int combo, int kills)
        {
            stats.totalRuns++;
            stats.totalKills += Mathf.Max(0, kills);
            if (combo > stats.bestCombo)
            {
                stats.bestCombo = combo;
            }

            if (level > stats.bestLevel)
            {
                stats.bestLevel = level;
            }

            Save();
        }

        /// <summary>消费一次金币翻倍次数（无剩余返回 false）</summary>
        public bool TryConsumeGoldDouble()
        {
            if (stats.goldDoubleLeft <= 0)
            {
                return false;
            }

            stats.goldDoubleLeft--;
            Save();
            return true;
        }

        /// <summary>消费一次免费重刷次数（无剩余返回 false）</summary>
        public bool TryConsumeFreeReroll()
        {
            if (stats.freeRerollLeft <= 0)
            {
                return false;
            }

            stats.freeRerollLeft--;
            Save();
            return true;
        }

        /// <summary>切换音效开关并保存</summary>
        public void SetSoundOn(bool on)
        {
            stats.soundOn = on;
            Save();
        }

        // 跨天时重置每日广告次数
        private void ResetDailyCountersIfNeeded()
        {
            string today = DateTime.Now.ToString("yyyyMMdd");
            if (stats.lastDailyDate == today)
            {
                return;
            }

            stats.lastDailyDate = today;
            stats.goldDoubleLeft = Mathf.Max(0, adsConfig.dailyDoubleGoldCount);
            stats.freeRerollLeft = Mathf.Max(0, adsConfig.dailyFreeRerollCount);
            Save();
        }
    }
}
