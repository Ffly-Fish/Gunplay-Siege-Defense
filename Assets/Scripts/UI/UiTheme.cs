using System.Globalization;
using UnityEngine;

// UiTheme.cs —— UI 全局视觉主题：配色、字号、间距、字体（所有界面统一从这里取值）
namespace Gunplay.UI
{
    /// <summary>UI 主题常量：深色底 + 金色强调；界面禁止各自硬编码颜色与字号</summary>
    public static class UiTheme
    {
        // ==================== 配色 ====================

        /// <summary>主色：金（标题、主按钮、金币）</summary>
        public static readonly Color Gold = FromHex("#FFD24A");

        /// <summary>主色暗部（金色描边 / 次级强调）</summary>
        public static readonly Color GoldDark = FromHex("#B88E22");

        /// <summary>危险色：红（Boss 血条、城门告急、失败提示）</summary>
        public static readonly Color Danger = FromHex("#E5484D");

        /// <summary>成功色：绿（城门耐久充足、可购买、正向反馈）</summary>
        public static readonly Color Success = FromHex("#3DDC97");

        /// <summary>主题底色：深蓝黑（全屏背景）</summary>
        public static readonly Color Background = FromHex("#14161F");

        /// <summary>面板底色</summary>
        public static readonly Color Panel = FromHex("#232838");

        /// <summary>浅一档面板底色（普通按钮 / 卡片）</summary>
        public static readonly Color PanelLight = FromHex("#2E3448");

        /// <summary>深一档面板底色（血条底槽 / 凹槽）</summary>
        public static readonly Color PanelDeep = FromHex("#151825");

        /// <summary>描边色（分隔线、卡片边）</summary>
        public static readonly Color Border = FromHex("#3A4257");

        /// <summary>主要文字（近白）</summary>
        public static readonly Color TextPrimary = FromHex("#F4F6FB");

        /// <summary>次要文字（灰蓝）</summary>
        public static readonly Color TextSecondary = FromHex("#9AA3B8");

        /// <summary>弱化文字（提示 / 禁用）</summary>
        public static readonly Color TextMuted = FromHex("#6B7488");

        /// <summary>不可购买 / 禁用状态底色</summary>
        public static readonly Color Disabled = FromHex("#394052");

        /// <summary>全屏遮罩底色（半透明深色，压暗战场）</summary>
        public static readonly Color Dim = new Color(0.06f, 0.075f, 0.11f, 0.94f);

        /// <summary>连杀反馈：橙色（10 连杀以上）</summary>
        public static readonly Color Orange = FromHex("#FF8A3D");

        /// <summary>稀有度 0：普通（灰）</summary>
        public static readonly Color TierCommon = FromHex("#8A93A8");

        /// <summary>稀有度 1：稀有（蓝）</summary>
        public static readonly Color TierRare = FromHex("#4EA8FF");

        /// <summary>稀有度 2：史诗（紫）</summary>
        public static readonly Color TierEpic = FromHex("#B575FF");

        // ==================== 像素风配色（UiPixelArt 生成九宫格精灵时使用） ====================

        /// <summary>像素描边色：九宫格最外层的深色描边（近黑，保证低分辨率下轮廓清晰）</summary>
        public static readonly Color PixelOutline = FromHex("#0A0C12");

        /// <summary>像素亮边色：面板 / 卡片左上受光面</summary>
        public static readonly Color PixelLightEdge = FromHex("#55648C");

        /// <summary>像素暗边色：面板 / 卡片右下阴影面</summary>
        public static readonly Color PixelDarkEdge = FromHex("#151A26");

        /// <summary>像素亮金：主按钮顶部受光边</summary>
        public static readonly Color PixelGoldLight = FromHex("#FFF0A6");

        /// <summary>像素暗金：主按钮外描边</summary>
        public static readonly Color PixelGoldShadow = FromHex("#8A5E0E");

        /// <summary>凹槽内阴影色：血条底槽 / 武器槽顶部压暗</summary>
        public static readonly Color PixelSlotShadow = FromHex("#0D0F18");

        /// <summary>凹槽口高光色：血条底槽 / 武器槽底部受光</summary>
        public static readonly Color PixelSlotHighlight = FromHex("#2A3247");

        /// <summary>条填充暗边色：血条填充顶部高光之外的两侧 / 底部压暗（与 Image.color 相乘）</summary>
        public static readonly Color PixelBarShade = FromHex("#9C9C9C");

        /// <summary>木色暗部（木质边框深色，预留给后续木框控件）</summary>
        public static readonly Color WoodDark = FromHex("#4A2F1B");

        /// <summary>木色主体（木质边框）</summary>
        public static readonly Color Wood = FromHex("#6B4423");

        /// <summary>木色亮部（木质边框受光面）</summary>
        public static readonly Color WoodLight = FromHex("#8C5A2E");

        /// <summary>石色暗部（石质按钮 / 卡片阴影面）</summary>
        public static readonly Color StoneDark = FromHex("#232838");

        /// <summary>石色主体（次级按钮底色）</summary>
        public static readonly Color Stone = FromHex("#3A4257");

        /// <summary>石色亮部（石质按钮受光面）</summary>
        public static readonly Color StoneLight = FromHex("#55648C");

        /// <summary>禁用按钮底色（灰石，次数用尽 / 不可点击）</summary>
        public static readonly Color PixelDisabledFill = FromHex("#394052");

        /// <summary>禁用按钮受光面</summary>
        public static readonly Color PixelDisabledLight = FromHex("#4A5268");

        /// <summary>禁用按钮阴影面</summary>
        public static readonly Color PixelDisabledDark = FromHex("#252B38");

        // ==================== 画布与字号 ====================

        /// <summary>参考分辨率宽度（竖屏设计基准）</summary>
        public const float RefWidth = 1080f;

        /// <summary>参考分辨率高度（竖屏设计基准）</summary>
        public const float RefHeight = 1920f;

        /// <summary>大标题字号</summary>
        public const int FontTitle = 104;

        /// <summary>一级标题字号</summary>
        public const int FontH1 = 72;

        /// <summary>二级标题字号</summary>
        public const int FontH2 = 52;

        /// <summary>正文字号</summary>
        public const int FontBody = 36;

        /// <summary>小字号（说明文字）</summary>
        public const int FontSmall = 30;

        /// <summary>最小字号（标签、HUD 辅助信息）</summary>
        public const int FontTiny = 24;

        // ==================== 间距与圆角 ====================

        /// <summary>极小间距</summary>
        public const float SpaceXS = 8f;

        /// <summary>小间距</summary>
        public const float SpaceS = 16f;

        /// <summary>中间距</summary>
        public const float SpaceM = 24f;

        /// <summary>大间距</summary>
        public const float SpaceL = 40f;

        /// <summary>面板圆角半径（像素）</summary>
        public const int Radius = 24;

        /// <summary>按钮圆角半径（像素）</summary>
        public const int ButtonRadius = 20;

        /// <summary>像素九宫格贴图边长（像素）：32×32 足够容纳描边 + 亮暗边 + 铆钉</summary>
        public const int PixelPatchSize = 32;

        /// <summary>像素九宫格边距（像素）：Image.type = Sliced 时四角固定、中间拉伸</summary>
        public const int PixelSlice = 8;

        /// <summary>像素描边粗细（像素）：最外层深色描边的厚度</summary>
        public const int PixelOutlineWidth = 2;

        /// <summary>像素亮 / 暗边厚度（像素）：模拟受光面与阴影面的倒角宽度（3px 在 1080 宽画布上有明显立体感）</summary>
        public const int PixelBevelWidth = 3;

        // 中文字体资源路径（相对 Assets/Resources，不带扩展名；Lead 已放入 HarmonyOS Sans SC Medium）
        private const string FontResourcePath = "Fonts/MainFont";

        // 缓存的 UI 字体
        private static Font cachedFont;

        /// <summary>UI 统一字体：优先 Resources/Fonts 下的中文 TTF，缺失时回退内置字体</summary>
        public static Font DefaultFont
        {
            get
            {
                if (cachedFont != null)
                {
                    return cachedFont;
                }

                cachedFont = Resources.Load<Font>(FontResourcePath);
                if (cachedFont == null)
                {
                    // 兜底：Unity 2022.3 内置字体为 Arial.ttf（LegacyRuntime.ttf 仅 2023+ 才有）；内置字体不含中文字形
                    cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    Debug.LogWarning("[UI] 未找到中文字体 Resources/" + FontResourcePath + "，已回退内置 Arial，中文可能显示为方块");
                }

                return cachedFont;
            }
        }

        /// <summary>把 "#RRGGBB" 解析为颜色（解析失败返回白色）</summary>
        public static Color FromHex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.white;
        }

        /// <summary>整数千分位格式化（金币 / 击杀数展示）</summary>
        public static string FormatInt(int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>按连杀反馈等级取连杀文字颜色</summary>
        public static Color ComboColor(int feedbackLevel)
        {
            switch (feedbackLevel)
            {
                case 1: return Gold;
                case 2: return Orange;
                case 3: return Danger;
                case 4: return TierEpic;
                default: return TextSecondary;
            }
        }

        /// <summary>按剩余比例取血条颜色（绿 → 金 → 红）</summary>
        public static Color HealthColor(float ratio)
        {
            if (ratio > 0.5f)
            {
                return Success;
            }

            if (ratio > 0.25f)
            {
                return Gold;
            }

            return Danger;
        }

        /// <summary>按稀有度取色标颜色</summary>
        public static Color TierColor(int tier)
        {
            switch (tier)
            {
                case 1: return TierRare;
                case 2: return TierEpic;
                default: return TierCommon;
            }
        }
    }
}
