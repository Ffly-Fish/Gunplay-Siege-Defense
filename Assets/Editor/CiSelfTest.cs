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

        /// <summary>暂停冻结验证时长（秒）：暂停期间持续 Tick 该时长，断言世界完全不推进</summary>
        private const float PAUSE_SIMULATE_SECONDS = 2f;

        /// <summary>「过关回满」验证时对城门造成的伤害值（用于把耐久打到上限以下）</summary>
        private const float GATE_DAMAGE_TEST_AMOUNT = 40f;

        /// <summary>「多武器同时开火」验证时的战斗模拟时长（秒）</summary>
        private const float WEAPON_FIRE_TEST_SECONDS = 4f;

        /// <summary>「多武器同时开火」验证时额外解锁的武器把数</summary>
        private const int WEAPON_FIRE_TEST_EXTRA_COUNT = 2;

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

                // ================= task-8：暂停 / 一键通关 / 过关回满 三组新功能断言 =================

                // ---- A. 过关回满：城门受损后，经「结算 → 商店 → 下一关」必须恢复到上限 ----
                runtime.StartNewRun();
                context.Check(runtime.State == GameState.Battle && runtime.Run.level == 1, "A 重新开局后进入第 1 关战斗");
                runtime.Wall.ApplyGateDamage(GATE_DAMAGE_TEST_AMOUNT);
                context.Check(runtime.Run.gateHp < runtime.Run.gateMaxHp, "A 城门受到伤害后耐久低于上限");
                context.Check(runtime.SkipCurrentLevel(), "A 一键通关返回 true");
                context.Check(runtime.State == GameState.Result, "A 一键通关后进入结算（Result）");
                runtime.ContinueFromResult();
                context.Check(runtime.State == GameState.Shop, "A 结算继续后进入商店（Shop）");
                runtime.ContinueFromShop();
                context.Check(runtime.State == GameState.Battle, "A 商店出征后进入下一关（Battle）");
                context.Check(runtime.Run.level == 2, "A 商店出征后关卡 = 2");
                context.Check(runtime.Run.gateMaxHp > 0f && Mathf.Approximately(runtime.Run.gateHp, runtime.Run.gateMaxHp), "A 过关后城门耐久回满");

                // ---- B. 暂停冻结：暂停期间 Tick 不得推进计时、伤害、金币与敌人 ----
                SimulateFor(runtime, aim, random, 1f);
                float gateHpBeforePause = runtime.Run.gateHp;
                int goldBeforePause = runtime.Run.gold;
                int enemyCountBeforePause = runtime.Enemies.Count;
                float durationBeforePause = runtime.Run.currentLevelStats.duration;
                context.Check(runtime.PauseGame(), "B PauseGame() 返回 true");
                context.Check(runtime.State == GameState.Paused && runtime.IsPaused, "B 暂停后状态为 Paused 且 IsPaused 为 true");
                context.Check(!runtime.IsAiming, "B 暂停后 IsAiming 为 false");
                int pauseFrames = Mathf.CeilToInt(PAUSE_SIMULATE_SECONDS / FIXED_DELTA_TIME);
                for (int i = 0; i < pauseFrames; i++)
                {
                    // 暂停期间仍主动推进 Tick，用来验证逻辑被冻结（而不是靠不调用 Tick 蒙对）
                    aim.IsAiming = true;
                    aim.AimWorldPos = PickAimPoint(runtime, random);
                    runtime.Tick(FIXED_DELTA_TIME);
                }

                context.Check(runtime.State == GameState.Paused, "B 暂停 2 秒后仍保持 Paused");
                context.Check(Mathf.Approximately(runtime.Run.gateHp, gateHpBeforePause), "B 暂停期间城门耐久不变");
                context.Check(runtime.Run.gold == goldBeforePause, "B 暂停期间金币不变");
                context.Check(runtime.Enemies.Count == enemyCountBeforePause, "B 暂停期间敌人数不变");
                context.Check(Mathf.Approximately(runtime.Run.currentLevelStats.duration, durationBeforePause), "B 暂停期间关卡计时不变");
                context.Check(runtime.ResumeGame(), "B ResumeGame() 返回 true");
                context.Check(runtime.State == GameState.Battle && !runtime.IsPaused, "B 恢复后回到战斗（Battle）");

                // ---- C. 暂停边界：非暂停状态恢复、非战斗状态暂停都必须返回 false ----
                context.Check(!runtime.ResumeGame(), "C 战斗中调用 ResumeGame() 返回 false");
                runtime.ReturnToMenu();
                context.Check(runtime.State == GameState.Menu, "C 返回主菜单后状态为 Menu");
                context.Check(!runtime.PauseGame(), "C Menu 状态调用 PauseGame() 返回 false");

                // ---- D. 一键通关：返回 true、进入结算并发放本关通关奖励 ----
                runtime.StartNewRun();
                SimulateFor(runtime, aim, random, 1f);
                int goldBeforeSkip = runtime.Run.gold;
                int expectedLevelReward = config.Tuning.economy.levelClearGoldBase +
                                          config.Tuning.economy.levelClearGoldPerLevel * Mathf.Max(1, runtime.Run.level);
                context.Check(runtime.SkipCurrentLevel(), "D SkipCurrentLevel() 返回 true");
                context.Check(runtime.State == GameState.Result, "D 一键通关后进入结算（Result）");
                context.Check(expectedLevelReward > 0, "D 本关通关奖励 > 0");
                context.Check(runtime.Run.gold == goldBeforeSkip + expectedLevelReward, "D 结算后金币增加了本关通关奖励");
                context.Check(!runtime.SkipCurrentLevel(), "D 结算状态再次 SkipCurrentLevel() 返回 false");

                // ---- E. 装备槽上限取消：拥有即全部同时开火（m03631） ----
                runtime.StartNewRun();
                context.Check(runtime.Run.equippedWeapons.Count == 1, "E 开局只装备初始武器");

                // 直接解锁前两把未拥有的武器（绕开商店随机，模拟玩家已购买多把枪）
                int extraUnlocked = 0;
                for (int i = 0; i < config.Weapons.Count && extraUnlocked < WEAPON_FIRE_TEST_EXTRA_COUNT; i++)
                {
                    WeaponConfigData weapon = config.Weapons[i];
                    if (weapon == null || runtime.Run.OwnsWeapon(weapon.id))
                    {
                        continue;
                    }

                    runtime.Run.UnlockWeapon(weapon.id, Mathf.Max(1, weapon.magazine));
                    extraUnlocked++;
                }

                runtime.Wall.EquipAllOwned();
                int ownedCount = runtime.Run.ownedWeapons.Count;
                context.Check(ownedCount >= 1 + WEAPON_FIRE_TEST_EXTRA_COUNT, "E 已拥有至少 3 把武器");
                context.Check(runtime.Run.equippedWeapons.Count == ownedCount, "E 所有已拥有武器都处于装备状态（无槽位上限）");

                // 战斗内验证：一次对准目标，多把武器都会各自开火
                HashSet<string> firedWeaponIds = new HashSet<string>();
                Action<WeaponFiredEvent> onWeaponFired = evt =>
                {
                    if (evt != null && !string.IsNullOrEmpty(evt.WeaponId))
                    {
                        firedWeaponIds.Add(evt.WeaponId);
                    }
                };
                runtime.Events.Subscribe(onWeaponFired);
                try
                {
                    SimulateFor(runtime, aim, random, WEAPON_FIRE_TEST_SECONDS);
                }
                finally
                {
                    runtime.Events.Unsubscribe(onWeaponFired);
                }

                context.Check(firedWeaponIds.Count >= 2, "E 同时开火的武器种类数 >= 2（实际 " + firedWeaponIds.Count + "）");
                context.Check(firedWeaponIds.Count == runtime.Run.equippedWeapons.Count,
                    "E 所有已装备武器都在开火（开火 " + firedWeaponIds.Count + " / 装备 " + runtime.Run.equippedWeapons.Count + "）");
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
