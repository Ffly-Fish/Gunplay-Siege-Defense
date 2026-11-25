using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Gunplay.Combat;
using Gunplay.Core;
using Gunplay.Run;
using Gunplay.View;
using UnityEditor;
using UnityEngine;

// CiSelfTest.cs —— 无头逻辑自检：直接驱动 GameRuntime 跑通「配置 → 战斗 → 结算 → 商店 → 下一关」
namespace Gunplay.EditorTools
{
    /// <summary>逻辑自检器（不用 NUnit，直接驱动 GameRuntime 并断言）</summary>
    public static class CiSelfTest
    {
        /// <summary>存档 PlayerPrefs 键（与 SaveSystem 保持一致）</summary>
        private const string SAVE_KEY = "gunplay_save_v1";

        /// <summary>自检日志路径（CI 约定）</summary>
        private const string LOG_FILE_PATH = "E:/UnityProject/_ci/selftest.log";

        /// <summary>固定逻辑步长（1/60 秒）</summary>
        private const float FIXED_DELTA_TIME = 1f / 60f;

        /// <summary>首段战斗模拟时长（秒）</summary>
        private const float BATTLE_SIMULATE_SECONDS = 60f;

        /// <summary>等待关卡通过的最长模拟时长（秒）</summary>
        private const float LEVEL_CLEAR_TIMEOUT_SECONDS = 300f;

        /// <summary>自检入口：失败写失败项并退出 1，全部通过写 SELF_TEST_OK 并退出 0</summary>
        public static void Run()
        {
            TestContext context = new TestContext();
            bool hadSave = PlayerPrefs.HasKey(SAVE_KEY);
            string originalSave = hadSave ? PlayerPrefs.GetString(SAVE_KEY) : null;
            GameRuntime runtime = null;
            DateTime startedAt = DateTime.Now;

            try
            {
                // 用空存档开启测试：SaveSystem 会把每日广告次数重置为配置值，保证断言确定
                PlayerPrefs.SetString(SAVE_KEY, "{}");
                PlayerPrefs.Save();

                ConfigDatabase config = ConfigDatabase.LoadFromResources();
                context.Check(config != null, "ConfigDatabase.LoadFromResources() 成功");
                if (config == null)
                {
                    throw new InvalidOperationException("配置数据库加载失败，无法继续自检");
                }

                context.Check(config.Weapons.Count > 0, "武器配置数量 > 0");
                context.Check(config.Enemies.Count > 0, "敌人配置数量 > 0");
                context.Check(config.Upgrades.Count > 0, "词条配置数量 > 0");
                context.Check(config.Tuning.waves != null, "Tuning.waves 非空");
                context.Check(config.Tuning.difficulty != null, "Tuning.difficulty 非空");
                context.Check(config.Tuning.economy != null, "Tuning.economy 非空");
                context.Check(config.Tuning.shop != null, "Tuning.shop 非空");
                context.Check(config.Tuning.gate != null, "Tuning.gate 非空");
                context.Check(config.Tuning.combat != null, "Tuning.combat 非空");
                context.Check(config.Tuning.ads != null, "Tuning.ads 非空");
                context.Check(config.Tuning.economy.comboTiers != null && config.Tuning.economy.comboTiers.Length > 0, "连杀档位配置非空");
                context.Check(config.Tuning.combat.slotUnlockLevels != null && config.Tuning.combat.slotUnlockLevels.Length > 0, "武器槽解锁配置非空");

                FakeAim aim = new FakeAim();
                runtime = new GameRuntime();
                runtime.Initialize(config, aim, false, 12345);
                context.Check(runtime.State == GameState.Menu, "Initialize 后状态为 Menu");

                runtime.StartNewRun();
                context.Check(runtime.State == GameState.Battle, "StartNewRun 后状态为 Battle");

                System.Random random = new System.Random(20251125);
                SimulateFor(runtime, aim, random, BATTLE_SIMULATE_SECONDS);
                context.Check(runtime.Run.totalKills > 0, "60 秒内击杀数 > 0");
                context.Check(runtime.Run.gold > 0, "60 秒内金币 > 0");
                context.Check(runtime.Run.currentLevelStats.shotsFired > 0, "60 秒内开火次数 > 0");

                // 继续推进到关卡结算（最多 300 秒模拟）
                float simulated = 0f;
                while (runtime.State != GameState.Result && simulated < LEVEL_CLEAR_TIMEOUT_SECONDS)
                {
                    if (runtime.State != GameState.Battle)
                    {
                        break;
                    }

                    TickOnce(runtime, aim, random);
                    simulated += FIXED_DELTA_TIME;
                }

                context.Check(runtime.State == GameState.Result, "推进到关卡结算（Result）");

                // 结算页：广告翻倍金币的每日次数限制
                if (runtime.State == GameState.Result)
                {
                    int dailyDouble = Mathf.Max(0, config.Tuning.ads.dailyDoubleGoldCount);
                    int doubleSuccess = 0;
                    for (int i = 0; i <= dailyDouble; i++)
                    {
                        bool ok = runtime.WatchAdDoubleGold();
                        if (i < dailyDouble)
                        {
                            if (ok)
                            {
                                doubleSuccess++;
                            }
                        }
                        else
                        {
                            context.Check(!ok, "广告翻倍次数耗尽后返回 false");
                        }
                    }

                    context.Check(doubleSuccess == dailyDouble, "广告翻倍每日可用次数 = " + dailyDouble);
                }

                if (runtime.State == GameState.Result)
                {
                    runtime.ContinueFromResult();
                }

                context.Check(runtime.State == GameState.Shop, "ContinueFromResult 后进入商店（Shop）");
                context.Check(runtime.Shop != null && runtime.Shop.Offers.Count > 0, "商店报价数量 > 0");

                if (runtime.State == GameState.Shop)
                {
                    int offerCount = runtime.Shop.Offers.Count;
                    context.Check(!runtime.BuyOffer(-1), "非法下标 -1 购买返回 false");
                    context.Check(!runtime.BuyOffer(offerCount + 5), "越界下标购买返回 false");

                    for (int i = 0; i < offerCount; i++)
                    {
                        ShopOffer offer = runtime.Shop.Offers[i];
                        bool wasValid = offer.IsValid;
                        int price = offer.Price;
                        int goldBefore = runtime.Run.gold;
                        bool bought = runtime.BuyOffer(i);
                        if (!wasValid)
                        {
                            continue;
                        }

                        if (price <= goldBefore)
                        {
                            context.Check(bought, "金币足够时购买成功: " + offer.Title);
                        }
                        else
                        {
                            context.Check(!bought, "金币不足时购买返回 false: " + offer.Title);
                        }

                        if (!bought)
                        {
                            context.Check(runtime.Run.gold == goldBefore, "购买失败不扣金币: " + offer.Title);
                        }
                    }

                    int maxRerolls = Mathf.Max(0, config.Tuning.shop.maxRerolls);
                    int dailyFree = Mathf.Max(0, config.Tuning.ads.dailyFreeRerollCount);
                    int expectedFree = Mathf.Min(dailyFree, maxRerolls);
                    int freeSuccess = 0;
                    for (int i = 0; i <= expectedFree; i++)
                    {
                        bool ok = runtime.WatchAdFreeReroll();
                        if (i < expectedFree)
                        {
                            if (ok)
                            {
                                freeSuccess++;
                            }
                        }
                        else
                        {
                            context.Check(!ok, "免费重刷次数耗尽后返回 false");
                        }
                    }

                    context.Check(freeSuccess == expectedFree, "免费重刷每日可用次数 = " + expectedFree);
                }

                if (runtime.State == GameState.Shop)
                {
                    runtime.ContinueFromShop();
                }

                context.Check(runtime.State == GameState.Battle, "ContinueFromShop 后进入战斗（Battle）");
                context.Check(runtime.Run.level == 2, "继续后关卡 = 2");
            }
            catch (Exception exception)
            {
                context.Check(false, "自检过程抛出异常: " + exception.Message);
                Debug.LogError("[CiSelfTest] 异常: " + exception);
            }
            finally
            {
                if (runtime != null)
                {
                    try
                    {
                        runtime.Dispose();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning("[CiSelfTest] Dispose 异常: " + exception.Message);
                    }
                }

                if (hadSave)
                {
                    PlayerPrefs.SetString(SAVE_KEY, originalSave);
                }
                else
                {
                    PlayerPrefs.DeleteKey(SAVE_KEY);
                }

                PlayerPrefs.Save();
            }

            float elapsed = (float)(DateTime.Now - startedAt).TotalSeconds;
            string summary = BuildLog(context, elapsed);
            WriteLog(summary);
            Debug.Log("[CiSelfTest] " + summary);
            Exit(context.failures.Count == 0 ? 0 : 1);
        }

        // 连续模拟指定秒数：每帧瞄准当前敌人或场上随机点
        private static void SimulateFor(GameRuntime runtime, FakeAim aim, System.Random random, float seconds)
        {
            int frames = Mathf.CeilToInt(seconds / FIXED_DELTA_TIME);
            for (int i = 0; i < frames; i++)
            {
                if (runtime.State != GameState.Battle)
                {
                    break;
                }

                TickOnce(runtime, aim, random);
            }
        }

        // 单帧：设置瞄准点并推进逻辑
        private static void TickOnce(GameRuntime runtime, FakeAim aim, System.Random random)
        {
            aim.IsAiming = true;
            aim.AimWorldPos = PickAimPoint(runtime, random);
            runtime.Tick(FIXED_DELTA_TIME);
        }

        // 选一个瞄准点：优先随机敌人，其次场上随机位置
        private static Vector2 PickAimPoint(GameRuntime runtime, System.Random random)
        {
            if (runtime.Enemies.Count > 0)
            {
                int index = random.Next(runtime.Enemies.Count);
                Enemy enemy = runtime.Enemies[index];
                if (enemy != null && enemy.Alive)
                {
                    return enemy.Position;
                }
            }

            float x = (float)(random.NextDouble() * 2d - 1d) * BattleField.HalfWidth * 0.8f;
            float y = (float)(random.NextDouble() * 10d);
            return new Vector2(x, y);
        }

        // 组织自检日志内容
        private static string BuildLog(TestContext context, float elapsedSeconds)
        {
            StringBuilder builder = new StringBuilder();
            if (context.failures.Count == 0)
            {
                builder.AppendLine("SELF_TEST_OK");
            }
            else
            {
                builder.AppendLine("SELF_TEST_FAILED");
                for (int i = 0; i < context.failures.Count; i++)
                {
                    builder.AppendLine("FAIL: " + context.failures[i]);
                }
            }

            builder.AppendLine("passed=" + context.passed);
            builder.AppendLine("failed=" + context.failures.Count);
            builder.AppendLine("elapsedSeconds=" + elapsedSeconds.ToString("0.###"));
            builder.AppendLine("unityVersion=" + Application.unityVersion);
            builder.AppendLine("time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            return builder.ToString();
        }

        // 写自检日志文件（自动创建目录）
        private static void WriteLog(string content)
        {
            try
            {
                string directory = Path.GetDirectoryName(LOG_FILE_PATH);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(LOG_FILE_PATH, content, new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[CiSelfTest] 写自检日志失败（CI 仍需读取退出码）: " + exception.Message);
            }
        }

        // 退出：批处理模式直接结束进程；交互模式失败时抛异常
        private static void Exit(int exitCode)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
                return;
            }

            if (exitCode != 0)
            {
                throw new InvalidOperationException("自检失败（退出码 " + exitCode + "），详见 " + LOG_FILE_PATH);
            }

            Debug.Log("[CiSelfTest] 自检通过（交互模式不调用 EditorApplication.Exit）");
        }

        /// <summary>断言上下文：累计通过数与失败项</summary>
        private sealed class TestContext
        {
            /// <summary>通过项数量</summary>
            public int passed;

            /// <summary>失败项描述列表</summary>
            public readonly List<string> failures = new List<string>();

            /// <summary>断言：条件不成立时记录失败项</summary>
            public void Check(bool condition, string message)
            {
                if (condition)
                {
                    passed++;
                    Debug.Log("[CiSelfTest] OK: " + message);
                }
                else
                {
                    failures.Add(message);
                    Debug.LogError("[CiSelfTest] FAIL: " + message);
                }
            }
        }
    }

    /// <summary>自检用瞄准输入：由测试主动设置瞄准状态与瞄准点</summary>
    public sealed class FakeAim : IAimInput
    {
        /// <summary>是否处于瞄准状态</summary>
        public bool IsAiming { get; set; }

        /// <summary>瞄准点世界坐标</summary>
        public Vector2 AimWorldPos { get; set; }
    }
}
