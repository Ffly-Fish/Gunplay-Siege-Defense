using Gunplay.View;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// UiBuilder.cs —— uGUI 代码构建工具：统一创建节点、文本、按钮、进度条（不依赖任何外部美术资源）
namespace Gunplay.UI
{
    /// <summary>uGUI 构建工具：所有界面通过它创建节点，保证层级、锚点、字体、射线设置一致</summary>
    public static class UiBuilder
    {
        /// <summary>创建一个空 RectTransform 节点（默认居中锚点）</summary>
        public static RectTransform NewRect(string name, Transform parent)
        {
            GameObject node = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)node.transform;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(100f, 100f);
            return rect;
        }

        /// <summary>创建铺满父节点的 RectTransform 节点</summary>
        public static RectTransform NewStretch(string name, Transform parent)
        {
            RectTransform rect = NewRect(name, parent);
            Stretch(rect);
            return rect;
        }

        /// <summary>让节点铺满父节点（可指定四边内缩像素）</summary>
        public static void Stretch(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>按锚点摆放节点（anchor 为归一化锚点，pivot 为自身轴心，offset 为相对锚点的偏移，size 为尺寸）</summary>
        public static void Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        /// <summary>创建纯色 / 精灵图片节点（默认不接收射线）</summary>
        public static Image NewImage(string name, Transform parent, Color color, Sprite sprite = null, bool raycast = false)
        {
            RectTransform rect = NewRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = raycast;
            return image;
        }

        /// <summary>创建纯色面板（UI 只用纯色块，程序化美术由表现层负责）</summary>
        public static Image NewPanel(string name, Transform parent, Color color)
        {
            return NewImage(name, parent, color, null, false);
        }

        /// <summary>创建文本（默认不接收射线，支持自动换行）</summary>
        public static Text NewText(string name, Transform parent, string content, int fontSize, Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            RectTransform rect = NewRect(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = UiTheme.DefaultFont;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.text = content;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>创建按钮（背景图 + 居中文字；点击自动播放 UI 音效）</summary>
        public static Button NewButton(string name, Transform parent, string label, Color background, Color labelColor, int fontSize, UnityAction onClick)
        {
            Image backgroundImage = NewImage(name, parent, background, null, true);
            Button button = backgroundImage.gameObject.AddComponent<Button>();
            button.targetGraphic = backgroundImage;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            Text labelText = NewText("Label", backgroundImage.transform, label, fontSize, labelColor);
            labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            labelText.verticalOverflow = VerticalWrapMode.Overflow;
            Stretch(labelText.rectTransform, 12f, 6f, 12f, 6f);

            if (onClick != null)
            {
                button.onClick.AddListener(() =>
                {
                    PlayClickSound();
                    onClick.Invoke();
                });
            }
            else
            {
                button.onClick.AddListener(PlayClickSound);
            }

            return button;
        }

        /// <summary>播放按钮点击音效（AudioBus 缺失时静默）</summary>
        public static void PlayClickSound()
        {
            AudioBus audio = AudioBus.Instance;
            if (audio != null)
            {
                audio.Play("ui_click", 0.7f);
            }
        }
    }

    /// <summary>简易进度条：底槽 + 用锚点宽度表示比例的填充（不依赖填充型精灵）</summary>
    public sealed class UiBar
    {
        /// <summary>底槽图片（负责尺寸与位置）</summary>
        public Image Background { get; private set; }

        /// <summary>填充图片（颜色随比例变化）</summary>
        public Image Fill { get; private set; }

        /// <summary>父级空间中的进度条矩形</summary>
        public RectTransform Rect
        {
            get { return Background.rectTransform; }
        }

        /// <summary>构建进度条（初始尺寸由 width / height 决定，之后可用 UiBuilder.Anchor 摆放）</summary>
        public UiBar(Transform parent, string name, Color fillColor, float width, float height)
        {
            Background = UiBuilder.NewImage(name, parent, UiTheme.PanelDeep, null, false);
            Background.rectTransform.sizeDelta = new Vector2(width, height);
            Fill = UiBuilder.NewImage("Fill", Background.transform, fillColor, null, false);
            RectTransform fillRect = Fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);
        }

        /// <summary>设置填充比例（0–1，0 时隐藏填充）</summary>
        public void SetRatio(float ratio)
        {
            float clamped = Mathf.Clamp01(ratio);
            bool fillVisible = clamped > 0.001f;
            if (Fill.enabled != fillVisible)
            {
                Fill.enabled = fillVisible;
            }

            if (!fillVisible)
            {
                return;
            }

            RectTransform fillRect = Fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(clamped, 1f);
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);
        }

        /// <summary>按当前值 / 上限设置填充比例</summary>
        public void SetValue(float current, float max)
        {
            SetRatio(max <= 0f ? 0f : current / max);
        }
    }
}
