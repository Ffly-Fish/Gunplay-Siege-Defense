using Gunplay.Core;
using Gunplay.View;
using UnityEngine;

// GameBootstrap.cs —— 场景唯一启动器：加载配置、创建 GameRuntime、每帧驱动
namespace Gunplay.Bootstrap
{
    /// <summary>游戏启动器（挂在场景根节点上，全场唯一）</summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        /// <summary>瞄准输入来源（实现 IAimInput 的组件，留空则自动查找）</summary>
        [Header("瞄准输入")]
        [Tooltip("实现 IAimInput 的组件；留空则自动在本场景内查找")]
        [SerializeField] private MonoBehaviour aimInputSource;

        /// <summary>是否输出埋点调试日志</summary>
        [Header("调试")]
        [Tooltip("开启后埋点事件会输出到 Console")]
        [SerializeField] private bool verboseTracking = true;

        /// <summary>是否启动后立即开局（跳过主菜单，便于测试）</summary>
        [Tooltip("勾选后进入游戏立即开始战斗，跳过主菜单")]
        [SerializeField] private bool autoStartBattle;

        /// <summary>随机种子（0 表示按时间随机）</summary>
        [Tooltip("随机种子；0 表示每局随机")]
        [SerializeField] private int randomSeed;

        /// <summary>总控实例</summary>
        private GameRuntime runtime;

        /// <summary>Unity 启动：装配并初始化</summary>
        private void Awake()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            ConfigDatabase config;
            try
            {
                config = ConfigDatabase.LoadFromResources();
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[Game] 配置加载失败: {exception.Message}");
                enabled = false;
                return;
            }

            runtime = new GameRuntime();
            runtime.Initialize(config, ResolveAimInput(), verboseTracking, randomSeed);
            if (autoStartBattle)
            {
                // 调试用：跳过主菜单直接开局
                runtime.StartNewRun();
            }
        }

        /// <summary>Unity 每帧：驱动总控</summary>
        private void Update()
        {
            if (runtime != null)
            {
                runtime.Tick(Time.deltaTime);
            }
        }

        /// <summary>Unity 销毁：释放总控</summary>
        private void OnDestroy()
        {
            runtime?.Dispose();
            runtime = null;
        }

        // 查找瞄准输入组件（优先使用面板指定）
        private IAimInput ResolveAimInput()
        {
            if (aimInputSource is IAimInput direct)
            {
                return direct;
            }

            if (aimInputSource != null)
            {
                Debug.LogWarning($"[Game] 指定的瞄准组件未实现 IAimInput: {aimInputSource.GetType().Name}");
            }

            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IAimInput found)
                {
                    return found;
                }
            }

            // TODO: 由 art-view 提供 AimInput 组件后即可自动找到；缺失时先用不瞄准的占位实现
            Debug.LogWarning("[Game] 场景内没有 IAimInput 实现，使用空瞄准输入");
            return new NullAimInput();
        }

        /// <summary>空瞄准输入：未接入触摸时使用，保证系统可运行</summary>
        private sealed class NullAimInput : IAimInput
        {
            /// <summary>永远不瞄准</summary>
            public bool IsAiming => false;

            /// <summary>瞄准点固定在炮口上方</summary>
            public Vector2 AimWorldPos => (Vector2)BattleField.Muzzle + Vector2.up * 2f;
        }
    }
}
