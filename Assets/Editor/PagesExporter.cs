using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gunplay.EditorTools
{
    /// <summary>
    /// GitHub Pages 站点导出器：把 WebGL 构建产物整理成可直接由 GitHub Pages 托管的目录（docs/）。
    /// 只需把 docs/ 提交到仓库并把 Pages 源设为「main 分支 /docs」即可。
    /// </summary>
    public static class PagesExporter
    {
        /// <summary>Unity WebGL 构建产物目录（相对工程根）</summary>
        private const string WEBGL_BUILD_DIR = "Build/WebGL";

        /// <summary>GitHub Pages 站点目录（相对工程根，必须位于仓库内）</summary>
        private const string PAGES_DIR = "docs";

        /// <summary>TTSDK 插件目录（存在时不能直接在本工程出网页版包，见导出说明）</summary>
        private const string TTSDK_PLUGIN_DIR = "Assets/Plugins/ByteGame";

        /// <summary>菜单入口：把现有 Build/WebGL 导出到 docs/。</summary>
        [MenuItem("Gunplay/6. 导出 GitHub Pages 站点", false, 60)]
        public static void BuildAndExport()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            bool hasTtsdk = Directory.Exists(Path.Combine(projectRoot, TTSDK_PLUGIN_DIR));
            if (hasTtsdk)
            {
                // 重要：本工程装了 TTSDK（抖音适配层），它会把 WebGL 切成混合渲染引擎 + WebGL 1.0，
                // 打出来的包只能在抖音容器里跑，普通浏览器打开会世界层全黑（只剩界面）。
                // 所以网页版包必须由 E:/UnityProject/_ci/ci_web_build.sh 在剥掉 TTSDK 的克隆里生成。
                Debug.LogWarning("[Pages] 检测到 TTSDK 插件：本工程直接构建的 WebGL 包不适合浏览器运行，" +
                    "网页版请用 _ci/ci_web_build.sh 出包后再导出。");
            }

            string error = Export();
            if (string.IsNullOrEmpty(error))
            {
                EditorUtility.DisplayDialog("导出完成", "站点已导出到 " + PAGES_DIR + "/\n\n把该目录提交后，在仓库 Settings → Pages 里选择「main 分支 /docs」即可访问。", "好");
            }
            else
            {
                EditorUtility.DisplayDialog("导出失败", error, "好");
            }
        }

        /// <summary>
        /// 把 Build/WebGL 的内容复制到 docs/（保留 docs 下已有的文档与截图）。
        /// </summary>
        /// <returns>错误信息；成功返回空字符串。</returns>
        public static string Export()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string source = Path.Combine(projectRoot, WEBGL_BUILD_DIR);
            string target = Path.Combine(projectRoot, PAGES_DIR);
            if (!Directory.Exists(source))
            {
                return "找不到 WebGL 产物目录：" + source + "\n请先构建 WebGL。";
            }

            Directory.CreateDirectory(target);
            CopyDirectory(source, target);

            // GitHub Pages 默认走 Jekyll，会把以下划线开头的文件忽略掉；放 .nojekyll 关闭该行为
            string noJekyll = Path.Combine(target, ".nojekyll");
            if (!File.Exists(noJekyll))
            {
                File.WriteAllText(noJekyll, string.Empty);
            }

            Debug.Log("[Pages] 已导出站点到 " + target);
            return string.Empty;
        }

        /// <summary>递归复制目录（覆盖同名文件）。</summary>
        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            }

            foreach (string directory in Directory.GetDirectories(source))
            {
                CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
            }
        }
    }
}
