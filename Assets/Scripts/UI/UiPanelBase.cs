using Gunplay.Core;
using UnityEngine;

// UiPanelBase.cs —— 所有界面面板的公共基类（延迟绑定 GameRuntime、事件订阅、显隐与刷新）
namespace Gunplay.UI
{
    /// <summary>界面面板基类：自动等待 GameRuntime 就绪、订阅 / 反注册事件、控制显隐并在显示时刷新</summary>
    public abstract class UiPanelBase : MonoBehaviour
    {
        // 面板自身的 CanvasGroup（控制整体透明度与射线）
        private CanvasGroup canvasGroup;
        // 自身 RectTransform 缓存
        private RectTransform panelRect;

        /// <summary>是否已绑定 GameRuntime 并完成事件订阅</summary>
        protected bool Bound { get; private set; }

        /// <summary>界面节点是否已由 Build 构建完成</summary>
        protected bool Built { get; private set; }

        /// <summary>当前是否处于显示状态（由 UiRoot 控制）</summary>
        public bool IsVisible { get; private set; }

        /// <summary>当前运行时入口（GameRuntime 尚未初始化时可能为 null）</summary>
        protected GameRuntime Runtime
        {
            get { return GameRuntime.Instance; }
        }

        /// <summary>自身面板矩形（供子类构建界面时使用）</summary>
        protected RectTransform PanelRect
        {
            get { return panelRect; }
        }

        private void Awake()
        {
            panelRect = (RectTransform)transform;
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // 初始隐藏，等待 UiRoot 切换到对应状态时再显示
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            IsVisible = false;
        }

        private void Start()
        {
            TryBind();
        }

        private void Update()
        {
            if (!Bound)
            {
                TryBind();
                return;
            }

            if (IsVisible)
            {
                OnTick(Time.unscaledDeltaTime);
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }

        /// <summary>构建界面节点（由 UiRoot 在创建面板后立即调用，只执行一次）</summary>
        public abstract void Build();

        /// <summary>刷新全部显示内容（从 GameRuntime 读取状态）</summary>
        public abstract void Refresh();

        /// <summary>显示或隐藏面板（由 UiRoot 在状态切换时调用）</summary>
        public void SetVisible(bool visible)
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // 显隐状态未变化时不重复触发生命周期回调，避免重复启动协程或重复刷新
            if (IsVisible == visible)
            {
                return;
            }

            IsVisible = visible;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
            if (visible)
            {
                OnShown();
            }
            else
            {
                OnHidden();
            }
        }

        /// <summary>标记界面构建完成（子类 Build 末尾调用）</summary>
        protected void MarkBuilt()
        {
            Built = true;
            if (Bound)
            {
                Refresh();
            }
        }

        /// <summary>子类实现：订阅自己关心的事件（必须与 Unsubscribe 成对）</summary>
        protected abstract void Subscribe(GameRuntime runtime);

        /// <summary>子类实现：反注册事件（必须与 Subscribe 成对）</summary>
        protected abstract void Unsubscribe(GameRuntime runtime);

        /// <summary>每帧回调（仅在已绑定且显示时执行，子类按需重写）</summary>
        protected virtual void OnTick(float deltaTime)
        {
        }

        /// <summary>面板显示回调（子类可重写以播放入场动画或重置临时状态）</summary>
        protected virtual void OnShown()
        {
        }

        /// <summary>面板隐藏回调（子类可重写以停止动画）</summary>
        protected virtual void OnHidden()
        {
        }

        // 等待 GameRuntime 就绪后绑定事件并首次刷新
        private void TryBind()
        {
            if (Bound)
            {
                return;
            }

            GameRuntime runtime = Runtime;
            if (runtime == null)
            {
                return;
            }

            Subscribe(runtime);
            Bound = true;
            if (Built)
            {
                Refresh();
            }
        }

        // 反注册事件（GameRuntime 已销毁时跳过）
        private void Unbind()
        {
            if (!Bound)
            {
                return;
            }

            Bound = false;
            GameRuntime runtime = Runtime;
            if (runtime != null)
            {
                Unsubscribe(runtime);
            }
        }
    }
}
