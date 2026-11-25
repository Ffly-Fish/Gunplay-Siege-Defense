using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// WebGraphicsMode.cs —— 在「抖音出包模式」与「浏览器可玩模式」之间切换 WebGL 图形设置
namespace Gunplay.EditorTools
{
    /// <summary>
    /// WebGL 渲染模式切换器。
    /// 背景：抖音 TTSDK 安装时会把 WebGL 固定成「混合渲染引擎 + OpenGL ES 2.0(WebGL 1.0)」，
    /// 这种包只能在抖音容器里跑；用普通浏览器打开时世界层（相机渲染的精灵）会整屏黑，
    /// 只剩 uGUI 界面 —— 表现为「看不到怪物但能开枪打中」。
    /// 想用 Unity 自己构建一个能跑浏览器的包，就点「Gunplay/8」；之后要回抖音出包，点「Gunplay/9」。
    /// </summary>
    public static class WebGraphicsMode
    {
        /// <summary>TTSDK 混合渲染引擎的宏（TTSDK 安装时自动写入 WebGL 平台宏）</summary>
        private const string TTSDK_MIX_ENGINE_DEFINE = "TTSDK_MIX_ENGINE";

        /// <summary>菜单：切到浏览器可玩模式（关闭 TTSDK 混引擎，图形 API 交回 Unity 自动选择）</summary>
        [MenuItem("Gunplay/8. 切到浏览器可玩模式（关闭 TTSDK 混引擎）", false, 80)]
        public static void UseBrowserMode()
        {
            bool changed = RemoveDefine(TTSDK_MIX_ENGINE_DEFINE);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, true);
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            AssetDatabase.SaveAssets();
            Debug.Log("[WebMode] 已切到浏览器可玩模式：移除宏 " + TTSDK_MIX_ENGINE_DEFINE +
                "（" + (changed ? "有改动" : "原本就没有") + "），WebGL 图形 API 交回自动选择。请重新构建 WebGL。");
            ShowDialog("已切到浏览器可玩模式",
                "已关闭 TTSDK 混引擎并恢复自动图形 API。\n\n现在重新 Build 出来的包可以在普通浏览器里正常显示。\n\n注意：要回抖音出包前，请点「Gunplay/9. 切回抖音出包模式」。");
        }

        /// <summary>菜单：切回抖音出包模式（恢复 TTSDK 混引擎宏与 OpenGL ES 2.0）</summary>
        [MenuItem("Gunplay/9. 切回抖音出包模式（开启 TTSDK 混引擎）", false, 81)]
        public static void UseDouyinMode()
        {
            bool changed = AddDefine(TTSDK_MIX_ENGINE_DEFINE);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES2 });
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            AssetDatabase.SaveAssets();
            Debug.Log("[WebMode] 已切回抖音出包模式：写入宏 " + TTSDK_MIX_ENGINE_DEFINE +
                "（" + (changed ? "有改动" : "原本就有") + "），WebGL 图形 API = OpenGL ES 2.0（WebGL 1.0）。");
            ShowDialog("已切回抖音出包模式",
                "已恢复 TTSDK 混引擎与 OpenGL ES 2.0。\n\n现在请用 ByteGame → TTSDK Tools → Build Tool 出抖音包；不要再把这种包放到浏览器里打开（会世界层全黑）。");
        }

        /// <summary>弹提示框（批处理模式下不弹，避免阻塞 CI）</summary>
        private static void ShowDialog(string title, string message)
        {
            if (Application.isBatchMode)
            {
                Debug.Log("[WebMode] " + title + " - " + message);
                return;
            }

            EditorUtility.DisplayDialog(title, message, "好");
        }

        /// <summary>确保 WebGL 平台的宏列表里包含指定宏</summary>
        /// <returns>是否真的改动了宏列表</returns>
        private static bool AddDefine(string define)
        {
            List<string> defines = ReadDefines();
            if (defines.Contains(define))
            {
                return false;
            }

            defines.Add(define);
            WriteDefines(defines);
            return true;
        }

        /// <summary>从 WebGL 平台的宏列表里移除指定宏</summary>
        /// <returns>是否真的改动了宏列表</returns>
        private static bool RemoveDefine(string define)
        {
            List<string> defines = ReadDefines();
            if (!defines.Remove(define))
            {
                return false;
            }

            WriteDefines(defines);
            return true;
        }

        /// <summary>读取 WebGL 平台当前的宏列表</summary>
        private static List<string> ReadDefines()
        {
            string raw = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.WebGL);
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(raw))
            {
                return result;
            }

            string[] parts = raw.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (part.Length > 0)
                {
                    result.Add(part);
                }
            }

            return result;
        }

        /// <summary>写回 WebGL 平台的宏列表</summary>
        private static void WriteDefines(List<string> defines)
        {
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.WebGL, string.Join(";", defines));
        }
    }
}