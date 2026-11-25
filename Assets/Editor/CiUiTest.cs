using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Gunplay.Core;
using Gunplay.UI;

// CiUiTest.cs —— 批处理里进入 Play 模式，点一下「开始新一轮」，验证 UI 事件链（按钮 → GameRuntime → 状态切换）
namespace Gunplay.EditorTools
{
    /// <summary>Play 模式 UI 交互自检：菜单按钮能否真的把游戏切到战斗状态</summary>
    public static class CiUiTest
    {
        /// <summary>结果日志路径</summary>
        private const string LOG_FILE_PATH = "E:/UnityProject/_ci/uitest.log";
        /// <summary>等待 GameRuntime 初始化的最长秒数</summary>
        private const float INIT_TIMEOUT_SECONDS = 20f;
        /// <summary>点击后等待状态切换的秒数</summary>
        private const float CLICK_WAIT_SECONDS = 1.5f;

        // 当前阶段
        private static int phase;
        // 阶段计时
        private static float timer;
        // 失败项
        private static readonly List<string> failures = new List<string>();
        // 是否已开始运行
        private static bool running;

        /// <summary>批处理入口：打开主场景并进入 Play 模式跑交互自检</summary>
        public static void Run()
        {
            failures.Clear();
            phase = 0;
            timer = 0f;
            running = true;
            WriteLog("UI_TEST_START");

            if (!File.Exists(SceneBuilder.MAIN_SCENE_PATH))
            {
                WriteLog("UI_TEST_FAILED\nmissing scene");
                Exit(1);
                return;
            }

            EditorSceneManager.OpenScene(SceneBuilder.MAIN_SCENE_PATH, OpenSceneMode.Single);
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        // 每帧推进阶段机
        private static void Tick()
        {
            if (!running)
            {
                return;
            }

            timer += Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.02f;

            switch (phase)
            {
                case 0:
                    if (GameRuntime.Instance == null)
                    {
                        if (timer > INIT_TIMEOUT_SECONDS)
                        {
                            Finish("GameRuntime 在 " + INIT_TIMEOUT_SECONDS + " 秒内未初始化");
                        }

                        return;
                    }

                    if (!Application.isPlaying)
                    {
                        return;
                    }

                    if (GameRuntime.Instance.State != GameState.Menu)
                    {
                        failures.Add("初始状态应为 Menu，实际为 " + GameRuntime.Instance.State);
                    }

                    GameObject startButton = GameObject.Find("StartButton");
                    if (startButton == null)
                    {
                        Finish("场景里找不到 StartButton");
                        return;
                    }

                    Button button = startButton.GetComponent<Button>();
                    if (button == null)
                    {
                        Finish("StartButton 上没有 Button 组件");
                        return;
                    }

                    if (button.onClick.GetPersistentEventCount() == 0 && !button.interactable)
                    {
                        failures.Add("StartButton 不可交互");
                    }

                    PointerEventData pointer = new PointerEventData(EventSystem.current);
                    ExecuteEvents.Execute(startButton, pointer, ExecuteEvents.pointerClickHandler);
                    phase = 1;
                    timer = 0f;
                    return;

                case 1:
                    if (timer < CLICK_WAIT_SECONDS)
                    {
                        return;
                    }

                    GameRuntime runtime = GameRuntime.Instance;
                    if (runtime == null)
                    {
                        Finish("点击后 GameRuntime 丢失");
                        return;
                    }

                    if (runtime.State != GameState.Battle)
                    {
                        failures.Add("点击开始后状态应为 Battle，实际为 " + runtime.State);
                    }

                    if (runtime.Enemies != null && runtime.Run != null && runtime.Run.level <= 0)
                    {
                        failures.Add("开局后关卡号应 >= 1，实际为 " + runtime.Run.level);
                    }

                    Finish(null);
                    return;
            }
        }

        // 结束并写结论
        private static void Finish(string fatal)
        {
            if (!running)
            {
                return;
            }

            running = false;
            EditorApplication.update -= Tick;

            if (fatal != null)
            {
                failures.Add(fatal);
            }

            if (failures.Count == 0)
            {
                WriteLog("UI_TEST_OK");
            }
            else
            {
                WriteLog("UI_TEST_FAILED");
                for (int i = 0; i < failures.Count; i++)
                {
                    WriteLog("FAIL " + failures[i]);
                }
            }

            EditorApplication.isPlaying = false;
            EditorApplication.update += ExitNextFrame;
        }

        // 退出 Play 模式后再退出编辑器
        private static void ExitNextFrame()
        {
            if (Application.isPlaying)
            {
                return;
            }

            EditorApplication.update -= ExitNextFrame;
            Exit(failures.Count == 0 ? 0 : 1);
        }

        // 写日志（追加行）
        private static void WriteLog(string line)
        {
            Debug.Log("[CiUiTest] " + line);
            Directory.CreateDirectory(Path.GetDirectoryName(LOG_FILE_PATH));
            File.AppendAllText(LOG_FILE_PATH, line + "\n");
        }

        // 批处理退出
        private static void Exit(int code)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }
    }
}
