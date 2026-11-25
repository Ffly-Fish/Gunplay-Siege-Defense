using UnityEditor;
using UnityEngine;

// GunplayMenu.cs —— 编辑器菜单入口：让美术/策划不用命令行也能重建场景、跑自检、出包
namespace Gunplay.EditorTools
{
    /// <summary>菜单入口集合（菜单项统一在 Gunplay 下）</summary>
    public static class GunplayMenu
    {
        /// <summary>一键重建主场景与竖屏 PlayerSettings</summary>
        [MenuItem("Gunplay/1. 重建主场景 (Main.unity)", false, 10)]
        public static void RebuildScene()
        {
            SceneBuilder.BuildAll();
            EditorUtility.DisplayDialog("Gunplay", "主场景与竖屏设置已生成：Assets/Scenes/Main.unity", "好");
        }

        /// <summary>校验主场景结构（组件、相机、构建设置）</summary>
        [MenuItem("Gunplay/2. 校验主场景结构", false, 11)]
        public static void ValidateScene()
        {
            CiSceneCheck.Run();
            EditorUtility.DisplayDialog("Gunplay", "主场景校验通过，详见 E:/UnityProject/_ci/scenecheck.log", "好");
        }

        /// <summary>无头逻辑自检（配置 → 战斗 → 结算 → 商店 → 下一关）</summary>
        [MenuItem("Gunplay/3. 运行逻辑自检", false, 12)]
        public static void RunSelfTest()
        {
            CiSelfTest.Run();
            EditorUtility.DisplayDialog("Gunplay", "逻辑自检通过，详见 E:/UnityProject/_ci/selftest.log", "好");
        }

        /// <summary>构建 WebGL 包并生成抖音小游戏目录</summary>
        [MenuItem("Gunplay/4. 构建 WebGL（含抖音目录）", false, 13)]
        public static void BuildWebGLPackage()
        {
            WebGLBuilder.BuildWebGL();
            EditorUtility.DisplayDialog("Gunplay", "WebGL 构建完成：Build/WebGL 与 Build/Douyin/gunplay-siege-defense", "好");
        }
    }
}
