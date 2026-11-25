using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// BuildWebGL.cs —— WebGL 竖屏构建入口（CI 通过 -executeMethod 调用）
namespace Gunplay.EditorTools
{
    /// <summary>WebGL 构建器：切换平台、写入构建设置、输出到 Build/WebGL</summary>
    public static class WebGLBuilder
    {
        /// <summary>构建输出目录（工程内相对路径）</summary>
        public const string OUTPUT_DIRECTORY = "Build/WebGL";

        /// <summary>WebGL 构建入口：失败时抛出异常，让批处理返回非 0 退出码</summary>
        public static void BuildWebGL()
        {
            if (!File.Exists(EditorBuildUtil.ProjectPath(SceneBuilder.MAIN_SCENE_PATH)))
            {
                Debug.LogWarning("[WebGLBuilder] 主场景不存在，先自动生成: " + SceneBuilder.MAIN_SCENE_PATH);
                SceneBuilder.BuildAll();
            }

            SceneBuilder.ApplyBuildSettings();
            ApplyWebGLSettings();
            EnsureWebGLPlatform();

            string outputPath = EditorBuildUtil.ProjectPath(OUTPUT_DIRECTORY);
            EditorBuildUtil.DeleteDirectory(outputPath);
            EditorBuildUtil.EnsureDirectory(outputPath);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new string[] { SceneBuilder.MAIN_SCENE_PATH },
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            };

            DateTime startedAt = DateTime.Now;
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report == null)
            {
                throw new BuildFailedException("WebGL 构建没有返回构建报告");
            }

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException("WebGL 构建失败: " + report.summary.result +
                    "（错误 " + report.summary.totalErrors + " 个，耗时 " +
                    (DateTime.Now - startedAt).TotalSeconds.ToString("0") + " 秒）");
            }

            string indexFile = Path.Combine(outputPath, "index.html");
            if (!File.Exists(indexFile))
            {
                Debug.LogWarning("[WebGLBuilder] 构建成功但未找到 index.html: " + indexFile);
            }

            Debug.Log("[WebGLBuilder] WebGL 构建成功: " + outputPath);
        }

        // 写入 WebGL 平台参数：gzip 压缩、数据缓存、256MB 内存、无异常支持、关闭 Development
        private static void ApplyWebGLSettings()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            // 抖音小游戏托管静态资源时不保证返回 Content-Encoding 头，必须内置解压兜底，否则资源 404/解析失败
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.memorySize = 256;
            // 只保留显式抛出的异常堆栈：既能定位线上崩溃，又不会像 Full 那样显著增加包体
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;
        }

        // 确保当前构建平台是 WebGL
        private static void EnsureWebGLPlatform()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
            {
                return;
            }

            bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            if (!switched)
            {
                throw new BuildFailedException("无法切换到 WebGL 构建平台，请检查编辑器是否正在编译或 WebGL 模块是否安装");
            }
        }
    }

    /// <summary>兼容入口：与文件名同名的类，便于 -executeMethod 直接调用</summary>
    public static class BuildWebGL
    {
        /// <summary>构建 WebGL（等价于 WebGLBuilder.BuildWebGL）</summary>
        public static void Build()
        {
            WebGLBuilder.BuildWebGL();
        }
    }
}
