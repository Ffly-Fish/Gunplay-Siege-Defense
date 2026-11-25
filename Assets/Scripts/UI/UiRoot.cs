using Gunplay.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// UiRoot.cs —— 用代码创建 Canvas / EventSystem / 五个界面，并跟随 GameState 切换显示
namespace Gunplay.UI
{
    /// <summary>UI 根节点：构建整个 uGUI 界面层级（不手改场景），跟随游戏状态切换面板</summary>
    [DisallowMultipleComponent]
    public sealed class UiRoot : MonoBehaviour
    {
        /// <summary>全局唯一实例（Bootstrap / 其他模块可访问）</summary>
        public static UiRoot Instance { get; private set; }

        [Header("参考分辨率宽度")]
        [Tooltip("参考分辨率宽度（竖屏设计基准，单位像素）")]
        [SerializeField] private float referenceWidth = UiTheme.RefWidth;

        [Header("参考分辨率高度")]
        [Tooltip("参考分辨率高度（竖屏设计基准，单位像素）")]
        [SerializeField] private float referenceHeight = UiTheme.RefHeight;

        [Header("宽高匹配权重")]
        [Tooltip("屏幕宽高匹配权重（1 = 以高度为基准缩放，竖屏游戏保持上下满屏）")]
        [SerializeField, Range(0f, 1f)] private float matchWidthOrHeight = 1f;

        [Header("自动适配屏幕比例")]
        [Tooltip("勾选后按屏幕宽高比自动切换缩放基准，保证非 9:16 屏幕上界面不被横向裁切")]
        [SerializeField] private bool autoFitAspect = true;

        [Header("画布排序层级")]
        [Tooltip("画布排序层级（数值越大越靠前，避免被战场精灵遮挡）")]
        [SerializeField] private int canvasSortingOrder = 100;

        // 画布组件
        private Canvas canvas;
        // 画布缩放组件
        private CanvasScaler canvasScaler;
        // 安全区容器（全部面板的父节点）
        private RectTransform safeArea;
        // 主菜单面板
        private MenuPanel menuPanel;
        // 战斗 HUD
        private BattleHud battleHud;
        // 过关结算面板
        private ResultPanel resultPanel;
        // 关间商店面板
        private ShopPanel shopPanel;
        // 本轮结束面板
        private GameOverPanel gameOverPanel;
        // 是否已订阅游戏状态事件
        private bool bound;
        // 当前显示的面板状态
        private GameState currentState = GameState.Menu;
        // 上次适配时的屏幕宽度（变化时才重算缩放基准）
        private int lastFitWidth;
        // 上次适配时的屏幕高度
        private int lastFitHeight;

        /// <summary>当前画布（供需要自定义 UI 的模块使用）</summary>
        public Canvas Canvas
        {
            get { return canvas; }
        }

        /// <summary>当前界面状态</summary>
        public GameState CurrentState
        {
            get { return currentState; }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[UI] 场景中存在多个 UiRoot，已销毁重复实例");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            BuildCanvas();
            EnsureEventSystem();
            BuildPanels();
            ApplyState(GameState.Menu);
        }

        private void Start()
        {
            TryBind();
        }

        private void Update()
        {
            if (!bound)
            {
                TryBind();
            }

            FitCanvas();
        }

        private void OnDestroy()
        {
            Unbind();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>确保场景中存在 UiRoot（场景未摆放时由 Bootstrap 调用自动创建）</summary>
        public static UiRoot EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            UiRoot existing = FindObjectOfType<UiRoot>();
            if (existing != null)
            {
                return existing;
            }

            GameObject rootObject = new GameObject("UiRoot");
            return rootObject.AddComponent<UiRoot>();
        }

        // 场景没有摆放 UiRoot 时自动创建（Bootstrap 也可显式调用 EnsureExists，二者等价且不会重复创建）
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateUiRoot()
        {
            EnsureExists();
        }

        /// <summary>强制刷新当前显示的界面（外部数据变化但未发事件时调用）</summary>
        public void RefreshCurrent()
        {
            switch (currentState)
            {
                case GameState.Menu:
                    if (menuPanel != null)
                    {
                        menuPanel.Refresh();
                    }

                    break;
                case GameState.Battle:
                    if (battleHud != null)
                    {
                        battleHud.Refresh();
                    }

                    break;
                case GameState.Result:
                    if (resultPanel != null)
                    {
                        resultPanel.Refresh();
                    }

                    break;
                case GameState.Shop:
                    if (shopPanel != null)
                    {
                        shopPanel.Refresh();
                    }

                    break;
                case GameState.GameOver:
                    if (gameOverPanel != null)
                    {
                        gameOverPanel.Refresh();
                    }

                    break;
            }
        }

        // 创建画布、缩放器、射线检测器与安全区容器
        private void BuildCanvas()
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            RectTransform canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.localScale = Vector3.one;

            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortingOrder;

            canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(referenceWidth, referenceHeight);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = matchWidthOrHeight;
            canvasScaler.referencePixelsPerUnit = 100f;
            FitCanvas();

            safeArea = UiBuilder.NewStretch("SafeArea", canvasObject.transform);
            safeArea.gameObject.AddComponent<UiSafeArea>();
        }

        // 按屏幕宽高比自动切换画布缩放基准：宽屏按高度缩放、超窄屏按宽度缩放，避免界面被裁切
        private void FitCanvas()
        {
            if (canvasScaler == null || !autoFitAspect)
            {
                return;
            }

            if (Screen.width == lastFitWidth && Screen.height == lastFitHeight)
            {
                return;
            }

            lastFitWidth = Screen.width;
            lastFitHeight = Screen.height;

            float designAspect = referenceWidth / Mathf.Max(1f, referenceHeight);
            float screenAspect = (float)lastFitWidth / Mathf.Max(1, lastFitHeight);
            canvasScaler.matchWidthOrHeight = screenAspect >= designAspect ? 1f : 0f;
        }

        // 场景中没有 EventSystem 时自动补一个（旧输入管理器模式）
        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            EventSystem existing = FindObjectOfType<EventSystem>();
            if (existing != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystemObject.transform.SetParent(transform, false);
        }

        // 创建五个面板（全部常驻，用 CanvasGroup 控制显隐，保证事件订阅始终有效）
        private void BuildPanels()
        {
            menuPanel = CreatePanel<MenuPanel>("MenuPanel");
            battleHud = CreatePanel<BattleHud>("BattleHud");
            resultPanel = CreatePanel<ResultPanel>("ResultPanel");
            shopPanel = CreatePanel<ShopPanel>("ShopPanel");
            gameOverPanel = CreatePanel<GameOverPanel>("GameOverPanel");
        }

        // 创建单个面板并立即构建其界面节点
        private T CreatePanel<T>(string name) where T : UiPanelBase
        {
            RectTransform rect = UiBuilder.NewStretch(name, safeArea);
            T panel = rect.gameObject.AddComponent<T>();
            panel.Build();
            return panel;
        }

        // 等待 GameRuntime 就绪后订阅状态事件并同步当前状态
        private void TryBind()
        {
            if (bound)
            {
                return;
            }

            GameRuntime runtime = GameRuntime.Instance;
            if (runtime == null)
            {
                return;
            }

            runtime.Events.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
            bound = true;
            ApplyState(runtime.State);
        }

        // 反注册状态事件
        private void Unbind()
        {
            if (!bound)
            {
                return;
            }

            bound = false;
            GameRuntime runtime = GameRuntime.Instance;
            if (runtime != null)
            {
                runtime.Events.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            }
        }

        // 状态切换：只显示对应面板并刷新
        private void ApplyState(GameState state)
        {
            currentState = state;
            SetPanelVisible(menuPanel, state == GameState.Menu);
            SetPanelVisible(battleHud, state == GameState.Battle);
            SetPanelVisible(resultPanel, state == GameState.Result);
            SetPanelVisible(shopPanel, state == GameState.Shop);
            SetPanelVisible(gameOverPanel, state == GameState.GameOver);
            RefreshCurrent();
        }

        // 设置单个面板显隐
        private static void SetPanelVisible(UiPanelBase panel, bool visible)
        {
            if (panel != null)
            {
                panel.SetVisible(visible);
            }
        }

        // 游戏状态变化事件回调
        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            ApplyState(evt.Current);
        }
    }
}
