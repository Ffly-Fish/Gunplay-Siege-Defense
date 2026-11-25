using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// DouyinToolkitCheck.cs —— 抖音开发工具包（BGDT）与抖音小游戏包产物校验
namespace Gunplay.EditorTools
{
    /// <summary>抖音开发工具包与 Build/Douyin 产物校验器（CI 入口 + 菜单 Gunplay/7）</summary>
    public static class DouyinToolkitCheck
    {
        /// <summary>ByteGame Develop Tools 包目录（工程内相对路径）</summary>
        private const string BGDT_DIRECTORY = "Assets/Plugins/ByteGame/com.bytedance.bgdt";

        /// <summary>抖音小游戏构建产物目录（工程内相对路径）</summary>
        private const string DOUYIN_OUTPUT_DIRECTORY = "Build/Douyin/" + DouyinBuildPostprocess.PACKAGE_NAME;

        /// <summary>校验日志输出路径（CI 收集用；写盘失败不影响退出码）</summary>
        private const string LOG_PATH = "E:/UnityProject/_ci/douyincheck.log";

        /// <summary>project.config.json 中 appid 的占位符（需替换为真实小游戏 appid）</summary>
        private const string APPID_PLACEHOLDER = "// TODO: 填写抖音小游戏 appid";

        /// <summary>当前工程记录的 BGDT 版本（用于提示版本变化，不强制失败）</summary>
        private const string EXPECTED_BGDT_VERSION = "3.0.271";

        /// <summary>index.html 中全屏样式标记（与 DouyinBuildPostprocess 一致）</summary>
        private const string FULLSCREEN_STYLE_MARKER = "gunplay-douyin-fullscreen";

        /// <summary>抖音包内必须存在的文件</summary>
        private static readonly string[] REQUIRED_OUTPUT_FILES = { "game.json", "project.config.json", "index.html" };

        /// <summary>BGDT package.json 的最小字段（只解析关心的三项）</summary>
        [Serializable]
        private sealed class BgdtPackageInfo
        {
            /// <summary>包名（com.bytedance.bgdt）</summary>
            public string name;
            /// <summary>包版本（如 3.0.271）</summary>
            public string version;
            /// <summary>显示名</summary>
            public string displayName;
        }

        /// <summary>抖音小游戏 project.config.json 的最小字段</summary>
        [Serializable]
        private sealed class DouyinProjectConfig
        {
            /// <summary>小游戏 appid（需用户填写真实值）</summary>
            public string appid;
            /// <summary>项目名</summary>
            public string projectname;
            /// <summary>编译类型（应为 game）</summary>
            public string compileType;
        }

        /// <summary>抖音小游戏 game.json 的最小字段</summary>
        [Serializable]
        private sealed class DouyinGameConfig
        {
            /// <summary>屏幕方向（应为 portrait）</summary>
            public string deviceOrientation;
            /// <summary>是否显示状态栏（应为 false）</summary>
            public bool showStatusBar;
        }

        /// <summary>校验入口：检查 BGDT 包、抖音包三个必需文件、appid 占位符，写日志并按结果退出</summary>
        [MenuItem("Gunplay/7. 检查抖音开发工具包", false, 70)]
        public static void Run()
        {
            StringBuilder details = new StringBuilder();
            int errors = 0;
            int warnings = 0;

            details.AppendLine("=== 抖音开发工具包 / 抖音小游戏包校验 ===");
            details.AppendLine("时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            details.AppendLine("工程根目录: " + EditorBuildUtil.ProjectRoot);
            details.AppendLine();

            try
            {
                CheckToolkit(details, ref errors, ref warnings);
                details.AppendLine();
                CheckDouyinPackage(details, ref errors, ref warnings);
            }
            catch (Exception exception)
            {
                errors++;
                details.AppendLine("- [FAIL] 校验过程异常: " + exception);
            }

            bool passed = errors == 0;
            StringBuilder output = new StringBuilder();
            output.AppendLine(passed ? "DOUYIN_CHECK_OK" : "DOUYIN_CHECK_FAILED");
            output.AppendLine("结果: error=" + errors + ", warning=" + warnings);
            output.Append(details);
            if (!passed)
            {
                output.AppendLine();
                output.AppendLine("修复提示: 先执行菜单 Gunplay/4 构建 WebGL（含抖音目录），再补齐 appid / 广告位。");
            }

            string content = output.ToString();
            try
            {
                EditorBuildUtil.WriteAllText(LOG_PATH, content);
                Debug.Log("[DouyinToolkitCheck] 校验完成，日志: " + LOG_PATH + "（" + (passed ? "OK" : "FAILED") + "）");
            }
            catch (Exception exception)
            {
                Debug.LogError("[DouyinToolkitCheck] 写日志失败: " + exception.Message);
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(passed ? 0 : 1);
            }
        }

        /// <summary>检查 BGDT 包目录、package.json 版本与 Editor 插件 dll</summary>
        private static void CheckToolkit(StringBuilder report, ref int errors, ref int warnings)
        {
            report.AppendLine("[BGDT 开发工具包]");
            string bgdtPath = EditorBuildUtil.ProjectPath(BGDT_DIRECTORY);
            if (!Directory.Exists(bgdtPath))
            {
                AddError(report, ref errors, "BGDT 包目录不存在: " + BGDT_DIRECTORY + "（请用 Unity 包管理器安装 ByteGame Develop Tools）");
                return;
            }

            AddPass(report, "BGDT 包目录存在: " + BGDT_DIRECTORY);

            string packageJsonPath = Path.Combine(bgdtPath, "package.json");
            if (!File.Exists(packageJsonPath))
            {
                AddError(report, ref errors, "BGDT 缺少 package.json: " + packageJsonPath);
            }
            else
            {
                BgdtPackageInfo info = null;
                try
                {
                    string json = File.ReadAllText(packageJsonPath, Encoding.UTF8);
                    info = JsonUtility.FromJson<BgdtPackageInfo>(json);
                }
                catch (Exception exception)
                {
                    AddError(report, ref errors, "BGDT package.json 解析失败: " + exception.Message);
                }

                if (info != null)
                {
                    string version = info.version ?? string.Empty;
                    if (string.IsNullOrEmpty(version))
                    {
                        AddError(report, ref errors, "BGDT package.json 缺少 version 字段");
                    }
                    else if (string.Equals(version, EXPECTED_BGDT_VERSION, StringComparison.Ordinal))
                    {
                        AddPass(report, "BGDT 版本: " + version);
                    }
                    else
                    {
                        AddWarning(report, ref warnings, "BGDT 版本为 " + version + "（工程记录为 " + EXPECTED_BGDT_VERSION + "），请确认桥接用法是否需要同步更新");
                    }

                    if (!string.IsNullOrEmpty(info.name))
                    {
                        AddPass(report, "BGDT 包名: " + info.name);
                    }
                }
            }

            string editorDllPath = Path.Combine(bgdtPath, "Editor", "bgdt.dll");
            if (File.Exists(editorDllPath))
            {
                AddPass(report, "BGDT 编辑器插件存在: Editor/bgdt.dll");
            }
            else
            {
                AddError(report, ref errors, "BGDT 缺少 Editor/bgdt.dll: " + editorDllPath);
            }
        }

        /// <summary>检查 Build/Douyin 产物的三个必需文件、game.json 竖屏配置与 appid 占位符</summary>
        private static void CheckDouyinPackage(StringBuilder report, ref int errors, ref int warnings)
        {
            report.AppendLine("[抖音小游戏包产物]");
            string outputPath = EditorBuildUtil.ProjectPath(DOUYIN_OUTPUT_DIRECTORY);
            if (!Directory.Exists(outputPath))
            {
                AddError(report, ref errors, "抖音包目录不存在: " + DOUYIN_OUTPUT_DIRECTORY + "（先执行菜单 Gunplay/4 构建 WebGL）");
                return;
            }

            AddPass(report, "抖音包目录存在: " + DOUYIN_OUTPUT_DIRECTORY);

            for (int i = 0; i < REQUIRED_OUTPUT_FILES.Length; i++)
            {
                string fileName = REQUIRED_OUTPUT_FILES[i];
                string filePath = Path.Combine(outputPath, fileName);
                if (!File.Exists(filePath))
                {
                    AddError(report, ref errors, "缺少必需文件: " + fileName);
                    continue;
                }

                long length = new FileInfo(filePath).Length;
                if (length <= 0)
                {
                    AddError(report, ref errors, "必需文件内容为空: " + fileName);
                    continue;
                }

                AddPass(report, "必需文件存在: " + fileName + "（" + length + " 字节）");
            }

            CheckGameJson(outputPath, report, ref errors, ref warnings);
            CheckProjectConfig(outputPath, report, ref errors, ref warnings);
            CheckFullscreenStyle(outputPath, report, ref warnings);
        }

        /// <summary>校验 game.json 为竖屏且隐藏状态栏</summary>
        private static void CheckGameJson(string outputPath, StringBuilder report, ref int errors, ref int warnings)
        {
            string filePath = Path.Combine(outputPath, "game.json");
            if (!File.Exists(filePath))
            {
                return;
            }

            try
            {
                DouyinGameConfig config = JsonUtility.FromJson<DouyinGameConfig>(File.ReadAllText(filePath, Encoding.UTF8));
                if (config == null || !string.Equals(config.deviceOrientation, "portrait", StringComparison.OrdinalIgnoreCase))
                {
                    AddWarning(report, ref warnings, "game.json 的 deviceOrientation 不是 portrait");
                }
                else
                {
                    AddPass(report, "game.json 竖屏配置正确（portrait）");
                }

                if (config != null && config.showStatusBar)
                {
                    AddWarning(report, ref warnings, "game.json 的 showStatusBar 为 true，建议关闭");
                }
            }
            catch (Exception exception)
            {
                AddWarning(report, ref warnings, "game.json 解析失败: " + exception.Message);
            }
        }

        /// <summary>校验 project.config.json 的项目名与 appid（占位符只提示不失败）</summary>
        private static void CheckProjectConfig(string outputPath, StringBuilder report, ref int errors, ref int warnings)
        {
            string filePath = Path.Combine(outputPath, "project.config.json");
            if (!File.Exists(filePath))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                DouyinProjectConfig config = JsonUtility.FromJson<DouyinProjectConfig>(json);
                if (config != null && !string.IsNullOrEmpty(config.projectname))
                {
                    AddPass(report, "project.config.json 项目名: " + config.projectname);
                }

                string appid = config != null ? config.appid : null;
                if (string.IsNullOrEmpty(appid) || appid.Contains("TODO") || string.Equals(appid, APPID_PLACEHOLDER, StringComparison.Ordinal))
                {
                    AddWarning(report, ref warnings, "project.config.json 的 appid 仍为占位符，需替换为真实小游戏 appid 后才能上传: " + APPID_PLACEHOLDER);
                }
                else
                {
                    AddPass(report, "project.config.json appid: " + appid);
                }
            }
            catch (Exception exception)
            {
                AddError(report, ref errors, "project.config.json 解析失败: " + exception.Message);
            }
        }

        /// <summary>校验 index.html 是否已注入抖音全屏样式</summary>
        private static void CheckFullscreenStyle(string outputPath, StringBuilder report, ref int warnings)
        {
            string filePath = Path.Combine(outputPath, "index.html");
            if (!File.Exists(filePath))
            {
                return;
            }

            try
            {
                string html = File.ReadAllText(filePath, Encoding.UTF8);
                if (html.Contains(FULLSCREEN_STYLE_MARKER))
                {
                    AddPass(report, "index.html 已注入全屏样式");
                }
                else
                {
                    AddWarning(report, ref warnings, "index.html 未注入抖音全屏样式（重新构建可自动注入）");
                }
            }
            catch (Exception exception)
            {
                AddWarning(report, ref warnings, "index.html 读取失败: " + exception.Message);
            }
        }

        /// <summary>记录通过项</summary>
        private static void AddPass(StringBuilder report, string message)
        {
            report.AppendLine("- [OK] " + message);
        }

        /// <summary>记录警告项（不改变最终结果）</summary>
        private static void AddWarning(StringBuilder report, ref int warnings, string message)
        {
            warnings++;
            report.AppendLine("- [WARN] " + message);
        }

        /// <summary>记录失败项（最终结果由 error 数决定）</summary>
        private static void AddError(StringBuilder report, ref int errors, string message)
        {
            errors++;
            report.AppendLine("- [FAIL] " + message);
        }
    }
}
