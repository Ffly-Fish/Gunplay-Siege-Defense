using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// CiSceneCheck.cs —— 批处理场景校验：打开主场景断言可玩性组件齐全，作为 WebGL 构建前置门
namespace Gunplay.EditorTools
{
    /// <summary>主场景结构校验器（不进入 PlayMode，直接在批处理里检查场景资源与构建设置）</summary>
    public static class CiSceneCheck
    {
        /// <summary>校验日志路径（CI 约定，与 selftest.log 同目录）</summary>
        private const string LOG_FILE_PATH = "E:/UnityProject/_ci/scenecheck.log";

        /// <summary>校验入口：全部通过写 SCENE_CHECK_OK 退出 0，有失败项写 SCENE_CHECK_FAILED 退出 1</summary>
        public static void Run()
        {
            // 通过项描述（写日志便于定位）
            List<string> passes = new List<string>();
            // 失败项描述（任一非空即判定失败）
            List<string> failures = new List<string>();

            try
            {
                VerifySceneAsset(failures, passes);
                VerifyBuildSettings(failures, passes);
                VerifyPlayerSettings(failures, passes);
            }
            catch (Exception exception)
            {
                failures.Add("校验过程异常: " + exception);
            }

            WriteLog(passes, failures);
            Exit(failures.Count == 0 ? 0 : 1);
        }

        // 校验主场景资源存在，并检查根物体与关键组件
        private static void VerifySceneAsset(List<string> failures, List<string> passes)
        {
            if (!File.Exists(EditorBuildUtil.ProjectPath(SceneBuilder.MAIN_SCENE_PATH)))
            {
                failures.Add("主场景文件不存在: " + SceneBuilder.MAIN_SCENE_PATH);
                return;
            }

            // 交互模式下先让用户保存当前场景，避免校验顺手丢弃手工修改
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                failures.Add("用户取消保存当前场景，已中止主场景校验");
                return;
            }

            EditorSceneManager.OpenScene(SceneBuilder.MAIN_SCENE_PATH, OpenSceneMode.Single);
            passes.Add("主场景已打开: " + SceneBuilder.MAIN_SCENE_PATH);

            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            Check(roots.Length == SceneBuilder.EXPECTED_ROOT_COUNT, "根物体数量为 " + SceneBuilder.EXPECTED_ROOT_COUNT + "（实际 " + roots.Length + "）", failures, passes);

            RequireComponent(roots, "GameBootstrap", "Gunplay.Bootstrap.GameBootstrap", failures, passes);
            RequireComponent(roots, "AimInput", "Gunplay.View.AimInput", failures, passes);
            RequireComponent(roots, "BattleView", "Gunplay.View.BattleView", failures, passes);
            RequireComponent(roots, "UiRoot", "Gunplay.UI.UiRoot", failures, passes);
            RequireCamera(roots, failures, passes);
            RequireAimInputReference(roots, failures, passes);
        }

        // 校验 Build Settings 只勾选主场景，且场景已启用
        private static void VerifyBuildSettings(List<string> failures, List<string> passes)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            bool onlyMain = scenes.Length == 1 && scenes[0].path == SceneBuilder.MAIN_SCENE_PATH && scenes[0].enabled;
            if (!onlyMain)
            {
                // 失败时把实际场景列表写进日志，便于定位是哪份设置覆盖了构建设置
                for (int i = 0; i < scenes.Length; i++)
                {
                    failures.Add("Build Settings 第 " + i + " 项: " + scenes[i].path + "（enabled=" + scenes[i].enabled + "）");
                }
            }

            Check(onlyMain, "Build Settings 只包含并启用主场景", failures, passes);
        }

        // 校验竖屏与 WebGL 默认分辨率设置
        private static void VerifyPlayerSettings(List<string> failures, List<string> passes)
        {
            Check(PlayerSettings.defaultInterfaceOrientation == UIOrientation.Portrait, "PlayerSettings 方向为竖屏 Portrait", failures, passes);
            Check(PlayerSettings.defaultWebScreenWidth == 1080 && PlayerSettings.defaultWebScreenHeight == 1920, "WebGL 默认分辨率 1080x1920", failures, passes);
        }

        // 断言指定名字的根物体上挂着目标组件类型
        private static void RequireComponent(GameObject[] roots, string objectName, string typeName, List<string> failures, List<string> passes)
        {
            Type type = EditorBuildUtil.FindType(typeName, objectName);
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name != objectName)
                {
                    continue;
                }

                if (type != null && roots[i].GetComponent(type) != null)
                {
                    passes.Add(objectName + " 已挂载 " + typeName);
                    return;
                }

                failures.Add(objectName + " 缺少组件 " + typeName);
                return;
            }

            failures.Add("场景缺少根物体 " + objectName);
        }

        // 断言场景唯一相机的正交参数可用于竖屏
        private static void RequireCamera(GameObject[] roots, List<string> failures, List<string> passes)
        {
            for (int i = 0; i < roots.Length; i++)
            {
                Camera camera = roots[i].GetComponent<Camera>();
                if (camera == null)
                {
                    continue;
                }

                Check(camera.orthographic, "主相机为正交投影", failures, passes);
                Check(Mathf.Approximately(camera.orthographicSize, SceneBuilder.CAMERA_ORTHOGRAPHIC_SIZE), "主相机 orthographicSize = " + SceneBuilder.CAMERA_ORTHOGRAPHIC_SIZE, failures, passes);
                Check(camera.transform.position.z < 0f, "主相机位于场景前方（z < 0）", failures, passes);
                return;
            }

            failures.Add("场景缺少相机");
        }

        // 断言 GameBootstrap 的 aimInputSource 序列化引用已指向场景里的瞄准组件
        private static void RequireAimInputReference(GameObject[] roots, List<string> failures, List<string> passes)
        {
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name != "GameBootstrap")
                {
                    continue;
                }

                SerializedObject serialized = new SerializedObject(roots[i].GetComponent("GameBootstrap"));
                SerializedProperty property = serialized.FindProperty("aimInputSource");
                if (property == null)
                {
                    failures.Add("GameBootstrap 找不到序列化字段 aimInputSource");
                    return;
                }

                Check(property.objectReferenceValue != null, "GameBootstrap.aimInputSource 已指向 AimInput", failures, passes);
                return;
            }

            failures.Add("场景缺少 GameBootstrap 根物体，无法校验瞄准引用");
        }

        // 记录一条断言结果
        private static void Check(bool condition, string description, List<string> failures, List<string> passes)
        {
            if (condition)
            {
                passes.Add(description);
                return;
            }

            failures.Add(description);
        }

        // 写校验日志（自动创建目录）
        private static void WriteLog(List<string> passes, List<string> failures)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(failures.Count == 0 ? "SCENE_CHECK_OK" : "SCENE_CHECK_FAILED");
            for (int i = 0; i < failures.Count; i++)
            {
                builder.AppendLine("FAIL: " + failures[i]);
            }

            builder.AppendLine("passed=" + passes.Count);
            builder.AppendLine("failed=" + failures.Count);
            builder.AppendLine("unityVersion=" + Application.unityVersion);
            builder.AppendLine("time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            try
            {
                string directory = Path.GetDirectoryName(LOG_FILE_PATH);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                File.WriteAllText(LOG_FILE_PATH, builder.ToString(), new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[CiSceneCheck] 写校验日志失败（CI 仍按退出码判定）: " + exception.Message);
            }

            Debug.Log("[CiSceneCheck] " + builder.ToString());
        }

        // 退出：批处理直接结束进程；交互模式失败时抛异常，避免静默通过
        private static void Exit(int exitCode)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
                return;
            }

            if (exitCode != 0)
            {
                throw new InvalidOperationException("场景校验失败（退出码 " + exitCode + "），详见 " + LOG_FILE_PATH);
            }
        }
    }
}
