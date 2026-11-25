using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// DouyinBuildPostprocess.cs —— WebGL 构建后处理：生成抖音小游戏目录与配置
namespace Gunplay.EditorTools
{
    /// <summary>抖音小游戏构建后处理：复制 WebGL 产物并补齐 game.json / project.config.json</summary>
    public sealed class DouyinBuildPostprocess : IPostprocessBuildWithReport
    {
        /// <summary>回调顺序（数字越大越晚执行，确保拿到最终产物）</summary>
        public int callbackOrder
        {
            get { return 1000; }
        }

        /// <summary>抖音小游戏包目录名（Build/Douyin/<name>）</summary>
        public const string PACKAGE_NAME = "gunplay-siege-defense";

        /// <summary>抖音小游戏 appid（用户于 2026-11-25 提供，构建时写入 project.config.json）</summary>
        public const string MINI_GAME_APPID = "tt8d3dfb80134f55ff07";

        /// <summary>全屏样式标记 Id（用于幂等注入）</summary>
        private const string FULLSCREEN_STYLE_ID = "gunplay-douyin-fullscreen-v2";

        /// <summary>全屏样式：让 Unity WebGL 画布铺满抖音小游戏容器，并隐藏 Unity 默认页脚与提示条</summary>
        private static readonly string FullscreenStyle =
            "<style id=\"" + FULLSCREEN_STYLE_ID + "\">\n" +
            "html, body { margin: 0; padding: 0; width: 100%; height: 100%; overflow: hidden; background: #14161F; }\n" +
            "#unity-container { position: absolute !important; left: 0 !important; top: 0 !important; width: 100% !important; height: 100% !important; transform: none !important; }\n" +
            "#unity-canvas, canvas { width: 100% !important; height: 100% !important; display: block; }\n" +
            "#unity-footer, #unity-build-title, #unity-webgl-logo, #unity-fullscreen-button { display: none !important; }\n" +
            "#unity-warning { position: absolute !important; top: 0 !important; width: 100% !important; }\n" +
            "</style>";

        /// <summary>构建完成回调：仅对 WebGL 生效</summary>
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL)
            {
                return;
            }

            string sourceDirectory = report.summary.outputPath;
            if (string.IsNullOrEmpty(sourceDirectory) || !Directory.Exists(sourceDirectory))
            {
                sourceDirectory = EditorBuildUtil.ProjectPath(WebGLBuilder.OUTPUT_DIRECTORY);
            }

            if (!Directory.Exists(sourceDirectory))
            {
                throw new BuildFailedException("抖音小游戏后处理失败：找不到 WebGL 构建产物目录 " + sourceDirectory);
            }

            try
            {
                CreateDouyinPackage(sourceDirectory);

                // 网页产物同样注入全屏样式，便于在浏览器里预览与自测（与抖音包一致的显示效果）
                PatchIndexHtml(Path.Combine(sourceDirectory, "index.html"));
            }
            catch (Exception exception)
            {
                throw new BuildFailedException("抖音小游戏后处理失败: " + exception.Message);
            }
        }

        // 复制产物并写入抖音配置文件
        private static void CreateDouyinPackage(string sourceDirectory)
        {
            string targetDirectory = EditorBuildUtil.ProjectPath("Build/Douyin/" + PACKAGE_NAME);
            EditorBuildUtil.DeleteDirectory(targetDirectory);
            EditorBuildUtil.CopyDirectory(sourceDirectory, targetDirectory);

            EditorBuildUtil.WriteAllText(Path.Combine(targetDirectory, "game.json"), BuildGameJson());
            EditorBuildUtil.WriteAllText(Path.Combine(targetDirectory, "project.config.json"), BuildProjectConfigJson());
            PatchIndexHtml(Path.Combine(targetDirectory, "index.html"));

            Debug.Log("[DouyinBuildPostprocess] 抖音小游戏包已生成: " + targetDirectory);
        }

        // game.json：竖屏、隐藏状态栏、网络超时
        private static string BuildGameJson()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"deviceOrientation\": \"portrait\",");
            builder.AppendLine("  \"showStatusBar\": false,");
            builder.AppendLine("  \"networkTimeout\": {");
            builder.AppendLine("    \"request\": 10000,");
            builder.AppendLine("    \"connectSocket\": 10000,");
            builder.AppendLine("    \"uploadFile\": 10000,");
            builder.AppendLine("    \"downloadFile\": 10000");
            builder.AppendLine("  }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        // project.config.json 模板：写入真实小游戏 appid 与基础编译设置
        private static string BuildProjectConfigJson()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"appid\": \"" + MINI_GAME_APPID + "\",");
            builder.AppendLine("  \"projectname\": \"" + PACKAGE_NAME + "\",");
            builder.AppendLine("  \"compileType\": \"game\",");
            builder.AppendLine("  \"miniprogramRoot\": \"./\",");
            builder.AppendLine("  \"libVersion\": \"latest\",");
            builder.AppendLine("  \"setting\": {");
            builder.AppendLine("    \"es6\": false,");
            builder.AppendLine("    \"minified\": true,");
            builder.AppendLine("    \"urlCheck\": false");
            builder.AppendLine("  }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        // 向 index.html 注入全屏样式（已注入则跳过，保证可重复构建）
        private static void PatchIndexHtml(string indexFile)
        {
            if (!File.Exists(indexFile))
            {
                Debug.LogWarning("[DouyinBuildPostprocess] 未找到 index.html，跳过全屏注入: " + indexFile);
                return;
            }

            string html = File.ReadAllText(indexFile, Encoding.UTF8);
            if (html.Contains(FULLSCREEN_STYLE_ID))
            {
                Debug.Log("[DouyinBuildPostprocess] index.html 已包含全屏样式，跳过注入");
                return;
            }

            int headEndIndex = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
            if (headEndIndex >= 0)
            {
                html = html.Insert(headEndIndex, FullscreenStyle + "\n");
            }
            else
            {
                html = FullscreenStyle + "\n" + html;
            }

            EditorBuildUtil.WriteAllText(indexFile, html);
        }
    }
}
