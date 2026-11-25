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

        // ==================== 像素风控件（九宫格 Sliced 精灵，见 UiPixelArt） ====================

        /// <summary>创建像素面板底框（深色面板 + 描边 / 亮暗边 / 铆钉 / 切角，九宫格拉伸不变形）</summary>
        public static Image NewPixelPanel(string name, Transform parent)
        {
            return NewPixelPanel(name, parent, UiPixelArt.Panel, Color.white);
        }

        /// <summary>创建像素九宫格图片（指定精灵与染色，Image.type = Sliced；不接收射线）</summary>
        public static Image NewPixelPanel(string name, Transform parent, Sprite sprite, Color tint)
        {
            Image image = NewImage(name, parent, tint, sprite, false);
            image.type = Image.Type.Sliced;
            return image;
        }

        /// <summary>创建像素卡片底框（比面板亮一档的石色卡片，带铆钉与切角）</summary>
        public static Image NewPixelCard(string name, Transform parent)
        {
            return NewPixelPanel(name, parent, UiPixelArt.Card, Color.white);
        }

        /// <summary>创建像素主按钮（金黄立体按钮：亮顶边 + 暗底边 + 描边；点击自动播放 UI 音效）</summary>
        public static Button NewPixelButton(string name, Transform parent, string label, Color labelColor, int fontSize, UnityAction onClick)
        {
            return CreatePixelButton(name, parent, label, labelColor, fontSize, onClick, UiPixelArt.ButtonPrimary);
        }

        /// <summary>创建像素次级按钮（石色立体按钮；点击自动播放 UI 音效）</summary>
        public static Button NewPixelSecondaryButton(string name, Transform parent, string label, Color labelColor, int fontSize, UnityAction onClick)
        {
            return CreatePixelButton(name, parent, label, labelColor, fontSize, onClick, UiPixelArt.ButtonSecondary);
        }

        /// <summary>创建像素进度条（凹槽底槽 + 可染色条填充；尺寸与摆放仍由调用方 Anchor 决定）</summary>
        public static UiBar NewPixelBar(Transform parent, string name, Color fillColor, float width, float height)
        {
            return new UiBar(parent, name, fillColor, width, height, UiPixelArt.Slot, UiPixelArt.BarFill);
        }

        // 创建像素按钮的公共实现：Sliced 底图 + 居中文字 + 点击音效
        private static Button CreatePixelButton(string name, Transform parent, string label, Color labelColor, int fontSize, UnityAction onClick, Sprite skin)
        {
            Image backgroundImage = NewImage(name, parent, Color.white, skin, true);
            backgroundImage.type = Image.Type.Sliced;
            Button button = backgroundImage.gameObject.AddComponent<Button>();
            button.targetGraphic = backgroundImage;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.65f);
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

        /// <summary>构建纯色进度条（初始尺寸由 width / height 决定，之后可用 UiBuilder.Anchor 摆放）</summary>
        public UiBar(Transform parent, string name, Color fillColor, float width, float height)
            : this(parent, name, fillColor, width, height, null, null)
        {
        }

        /// <summary>构建进度条（可指定九宫格底槽与填充精灵；传 null 则退回纯色块）</summary>
        /// <param name="backgroundSprite">底槽九宫格精灵（null = 纯色底）</param>
        /// <param name="fillSprite">填充九宫格精灵（null = 纯色填充）</param>
        public UiBar(Transform parent, string name, Color fillColor, float width, float height, Sprite backgroundSprite, Sprite fillSprite)
        {
            Background = UiBuilder.NewImage(name, parent, backgroundSprite != null ? Color.white : UiTheme.PanelDeep, backgroundSprite, false);
            if (backgroundSprite != null)
            {
                Background.type = Image.Type.Sliced;
            }

            Background.rectTransform.sizeDelta = new Vector2(width, height);
            Fill = UiBuilder.NewImage("Fill", Background.transform, fillColor, fillSprite, false);
            if (fillSprite != null)
            {
                Fill.type = Image.Type.Sliced;
            }
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
