using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// SceneBuilder.cs —— 自动生成主场景与竖屏 Player 设置（CI 与手工构建共用）
namespace Gunplay.EditorTools
{
    /// <summary>主场景构建器：生成 Assets/Scenes/Main.unity 并配置竖屏 PlayerSettings</summary>
    public static class SceneBuilder
    {
        /// <summary>主场景资源路径</summary>
        public const string MAIN_SCENE_PATH = "Assets/Scenes/Main.unity";

        /// <summary>相机正交尺寸（世界单位；屏幕高 19.2 的半高）</summary>
        public const float CAMERA_ORTHOGRAPHIC_SIZE = 9.6f;

        /// <summary>场景背景色（#14161F）</summary>
        public static readonly Color BACKGROUND_COLOR = new Color32(0x14, 0x16, 0x1F, 0xFF);

        /// <summary>场景根物体应有数量：MainCamera / AimInput / BattleView / GameBootstrap / UiRoot</summary>
        public const int EXPECTED_ROOT_COUNT = 5;

        /// <summary>一键构建：生成主场景，并写入 Build Settings 与竖屏 PlayerSettings</summary>
        public static void BuildAll()
        {
            PrepareCurrentScene();
            EnsureSceneFolder();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RemoveExistingSceneAsset();
            CreateMainCamera();
            MonoBehaviour aimInput = CreateAimInput();
            CreateBattleView();
            CreateGameBootstrap(aimInput);
            CreateUiRoot();
            VerifyRootObjects();
            SaveMainScene(scene);
            ApplyBuildSettings();
            ApplyPlayerSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] 主场景与竖屏 PlayerSettings 已就绪: " + MAIN_SCENE_PATH);
        }

        /// <summary>把 Build Settings 的场景列表设置为仅 Main.unity</summary>
        public static void ApplyBuildSettings()
        {
            EditorBuildSettings.scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(MAIN_SCENE_PATH, true)
            };
        }

        // 交互模式下先保存用户当前场景，避免丢掉手工修改
        private static void PrepareCurrentScene()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new InvalidOperationException("用户取消保存当前场景，已中止主场景构建");
            }
        }

        // 确保 Assets/Scenes 目录存在
        private static void EnsureSceneFolder()
        {
            EditorBuildUtil.EnsureDirectory(EditorBuildUtil.ProjectPath("Assets/Scenes"));
            AssetDatabase.Refresh();
        }

        // 删除旧的主场景资源，保证得到全新覆盖的场景文件
        private static void RemoveExistingSceneAsset()
        {
            if (File.Exists(EditorBuildUtil.ProjectPath(MAIN_SCENE_PATH)))
            {
                AssetDatabase.DeleteAsset(MAIN_SCENE_PATH);
            }
        }

        // 创建正交主相机（竖屏可视范围 10.8 × 19.2）
        private static void CreateMainCamera()
        {
            GameObject cameraObject = new GameObject("MainCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CAMERA_ORTHOGRAPHIC_SIZE;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BACKGROUND_COLOR;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;

            if (cameraObject.GetComponent<AudioListener>() == null)
            {
                cameraObject.AddComponent<AudioListener>();
            }

            AddUrpCameraData(cameraObject);
            AddCameraFitter(cameraObject);
        }

        // 挂载相机视野适配组件（运行时按屏幕宽高比放大正交尺寸，保证长屏手机不裁切战场左右边界）
        private static void AddCameraFitter(GameObject cameraObject)
        {
            Type fitterType = EditorBuildUtil.FindType("Gunplay.View.CameraFitter", "CameraFitter");
            if (fitterType == null || !typeof(Component).IsAssignableFrom(fitterType))
            {
                // TODO: 缺少相机适配组件（Gunplay.View.CameraFitter）时，9:19.5 长屏会裁切战场左右边界
                Debug.LogWarning("[SceneBuilder] 未找到 CameraFitter 组件类型，长屏适配将失效");
                return;
            }

            if (cameraObject.GetComponent(fitterType) == null)
            {
                cameraObject.AddComponent(fitterType);
            }
        }

        // URP 相机需要 UniversalAdditionalCameraData；用反射添加，避免编辑器脚本强依赖 URP 程序集
        private static void AddUrpCameraData(GameObject cameraObject)
        {
            Type cameraDataType = EditorBuildUtil.FindType("UniversalAdditionalCameraData");
            if (cameraDataType == null || !typeof(Component).IsAssignableFrom(cameraDataType))
            {
                return;
            }

            try
            {
                if (cameraObject.GetComponent(cameraDataType) == null)
                {
                    cameraObject.AddComponent(cameraDataType);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[SceneBuilder] 添加 URP 相机数据组件失败: " + exception.Message);
            }
        }

        // 创建瞄准输入根物体并挂载 AimInput 组件（触摸瞄准 / 编辑器鼠标模拟都依赖它）
        private static MonoBehaviour CreateAimInput()
        {
            GameObject aimObject = new GameObject("AimInput");
            Type aimType = EditorBuildUtil.FindType("Gunplay.View.AimInput", "AimInput");
            if (aimType == null || !typeof(Component).IsAssignableFrom(aimType))
            {
                // TODO: 瞄准输入组件（Gunplay.View.AimInput）缺失；缺失时游戏会退化为永不瞄准，无法游玩
                Debug.LogWarning("[SceneBuilder] 未找到 AimInput 组件类型，已保留空物体占位（TODO: 组件就绪后重新生成本场景）");
                return null;
            }

            return aimObject.AddComponent(aimType) as MonoBehaviour;
        }

        // 创建战斗表现层根物体并挂载 BattleView 组件（敌人 / 子弹 / 特效全部由它绘制）
        private static void CreateBattleView()
        {
            GameObject viewObject = new GameObject("BattleView");
            Type viewType = EditorBuildUtil.FindType("Gunplay.View.BattleView", "BattleView");
            if (viewType == null || !typeof(Component).IsAssignableFrom(viewType))
            {
                // TODO: 战斗表现层组件（Gunplay.View.BattleView）缺失；缺失时战斗画面完全空白
                Debug.LogWarning("[SceneBuilder] 未找到 BattleView 组件类型，已保留空物体占位（TODO: 组件就绪后重新生成本场景）");
                return;
            }

            viewObject.AddComponent(viewType);
        }

        // 创建 GameBootstrap 空物体并挂载运行时启动器，同时把瞄准组件写入其序列化引用
        private static void CreateGameBootstrap(MonoBehaviour aimInput)
        {
            GameObject bootstrapObject = new GameObject("GameBootstrap");
            Type bootstrapType = EditorBuildUtil.FindType("Gunplay.Core.GameBootstrap", "Gunplay.Bootstrap.GameBootstrap", "GameBootstrap");
            if (bootstrapType != null && typeof(Component).IsAssignableFrom(bootstrapType))
            {
                Component bootstrap = bootstrapObject.AddComponent(bootstrapType);
                AssignAimInputSource(bootstrap, aimInput);
                return;
            }

            // TODO: 运行时启动器类型尚未就绪（预期 Gunplay.Core.GameBootstrap 或 Gunplay.Bootstrap.GameBootstrap）。
            // TODO: 当前保留空物体占位；等组件就绪后重新执行 SceneBuilder.BuildAll 即可自动挂载。
            Debug.LogWarning("[SceneBuilder] 未找到 GameBootstrap 组件类型，已保留空物体占位（TODO: 类型就绪后重新生成本场景）");
        }

        // 把瞄准组件写入 GameBootstrap 的私有序列化字段 aimInputSource（避免运行时再靠 FindObjectsOfType 查找）
        private static void AssignAimInputSource(Component bootstrap, MonoBehaviour aimInput)
        {
            if (bootstrap == null || aimInput == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(bootstrap);
            SerializedProperty property = serialized.FindProperty("aimInputSource");
            if (property == null)
            {
                // TODO: GameBootstrap 字段改名后需同步这里的序列化字段名（aimInputSource）
                Debug.LogWarning("[SceneBuilder] GameBootstrap 上找不到序列化字段 aimInputSource，运行时将退化为自动查找");
                return;
            }

            property.objectReferenceValue = aimInput;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // 创建界面根物体并挂载 UiRoot 组件（Canvas 与五个面板都由它在运行时搭建）
        private static void CreateUiRoot()
        {
            GameObject uiObject = new GameObject("UiRoot");
            Type uiType = EditorBuildUtil.FindType("Gunplay.UI.UiRoot", "UiRoot");
            if (uiType == null || !typeof(Component).IsAssignableFrom(uiType))
            {
                // TODO: 界面根组件（Gunplay.UI.UiRoot）缺失；缺失时游戏没有任何界面
                Debug.LogWarning("[SceneBuilder] 未找到 UiRoot 组件类型，已保留空物体占位（TODO: 组件就绪后重新生成本场景）");
                return;
            }

            uiObject.AddComponent(uiType);
        }

        // 校验场景根物体数量与关键组件，缺少可玩性组件时直接失败，避免产出打不开的包
        private static void VerifyRootObjects()
        {
            GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            if (rootObjects.Length != EXPECTED_ROOT_COUNT)
            {
                throw new InvalidOperationException("[SceneBuilder] 场景根物体数量为 " + rootObjects.Length + "，预期 " + EXPECTED_ROOT_COUNT);
            }

            RequireComponentOn(rootObjects, "GameBootstrap", "Gunplay.Bootstrap.GameBootstrap");
            RequireComponentOn(rootObjects, "AimInput", "Gunplay.View.AimInput");
            RequireComponentOn(rootObjects, "BattleView", "Gunplay.View.BattleView");
            RequireComponentOn(rootObjects, "UiRoot", "Gunplay.UI.UiRoot");
            RequireMainCamera(rootObjects);
        }

        // 断言指定根物体上挂着目标组件类型（缺失即抛异常）
        private static void RequireComponentOn(GameObject[] rootObjects, string objectName, string typeName)
        {
            Type type = EditorBuildUtil.FindType(typeName, objectName);
            for (int i = 0; i < rootObjects.Length; i++)
            {
                if (rootObjects[i].name != objectName)
                {
                    continue;
                }

                if (type != null && rootObjects[i].GetComponent(type) != null)
                {
                    return;
                }

                throw new InvalidOperationException("[SceneBuilder] " + objectName + " 根物体缺少组件 " + typeName + "，场景不可玩");
            }

            throw new InvalidOperationException("[SceneBuilder] 场景缺少根物体 " + objectName);
        }

        // 断言场景相机为正交且尺寸符合竖屏（10.8 × 19.2 可视范围）
        private static void RequireMainCamera(GameObject[] rootObjects)
        {
            for (int i = 0; i < rootObjects.Length; i++)
            {
                Camera camera = rootObjects[i].GetComponent<Camera>();
                if (camera == null)
                {
                    continue;
                }

                if (!camera.orthographic || !Mathf.Approximately(camera.orthographicSize, CAMERA_ORTHOGRAPHIC_SIZE))
                {
                    throw new InvalidOperationException("[SceneBuilder] 主相机必须为正交且 orthographicSize = " + CAMERA_ORTHOGRAPHIC_SIZE);
                }

                return;
            }

            throw new InvalidOperationException("[SceneBuilder] 场景缺少相机");
        }

        // 保存主场景
        private static void SaveMainScene(Scene scene)
        {
            if (!EditorSceneManager.SaveScene(scene, MAIN_SCENE_PATH))
            {
                throw new InvalidOperationException("保存主场景失败: " + MAIN_SCENE_PATH);
            }
        }

        // 写入竖屏 PlayerSettings 与 WebGL 默认模板
        private static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Gunplay";
            PlayerSettings.productName = "枪法守城闯大关";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.defaultScreenWidth = 1080;
            PlayerSettings.defaultScreenHeight = 1920;
            PlayerSettings.defaultWebScreenWidth = 1080;
            PlayerSettings.defaultWebScreenHeight = 1920;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            try
            {
                PlayerSettings.WebGL.template = "APPLICATION:Default";
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[SceneBuilder] 设置 WebGL 模板失败，将沿用当前模板: " + exception.Message);
            }
        }
    }
}
