using System.Collections.Generic;
using UnityEngine;

// UiPixelArt.cs —— 运行时程序化生成「像素九宫格」精灵：面板框 / 按钮 / 凹槽 / 条填充（零外部美术资源）
namespace Gunplay.UI
{
    /// <summary>
    /// 像素九宫格精灵工厂：用代码绘制 32×32 的像素贴图（Point 过滤、无 mipmap、FullRect + border），
    /// 再交给 Image（type = Sliced）任意缩放，得到亮顶边 / 暗底边 / 外描边 / 切角 / 铆钉的像素立体感。
    /// 所有精灵按「样式 + 颜色 + 尺寸」做 key 缓存，只在首次使用时生成一次，运行期不再创建纹理。
    /// </summary>
    public static class UiPixelArt
    {
        // 精灵缓存：key 由样式与颜色参数拼成，避免重复生成贴图
        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

        // ==================== 预置样式（界面直接用这些属性，颜色来自 UiTheme） ====================

        /// <summary>标准面板底框：深蓝黑底 + 外描边 + 左上亮边 / 右下暗边 + 四角铆钉 + 切角</summary>
        public static Sprite Panel
        {
            get
            {
                return CreatePanelSprite(UiTheme.Panel, UiTheme.PixelOutline, UiTheme.PixelLightEdge, UiTheme.PixelDarkEdge, true, true);
            }
        }

        /// <summary>卡片底框：比面板亮一档的石色，带铆钉与切角（商店奖励卡 / 结算卡 / 战果卡）</summary>
        public static Sprite Card
        {
            get
            {
                return CreatePanelSprite(UiTheme.PanelLight, UiTheme.PixelOutline, UiTheme.PixelLightEdge, UiTheme.PixelDarkEdge, true, true);
            }
        }

        /// <summary>主按钮：金黄底 + 亮金顶边 + 暗金底边 + 深色外描边（重要操作）</summary>
        public static Sprite ButtonPrimary
        {
            get
            {
                return CreateButtonSprite(UiTheme.Gold, UiTheme.PixelGoldShadow, UiTheme.PixelGoldLight, UiTheme.GoldDark, true);
            }
        }

        /// <summary>次级按钮：石色底 + 亮石顶边 + 暗石底边（次要操作 / 购买按钮）</summary>
        public static Sprite ButtonSecondary
        {
            get
            {
                return CreateButtonSprite(UiTheme.Stone, UiTheme.PixelOutline, UiTheme.StoneLight, UiTheme.StoneDark, true);
            }
        }

        /// <summary>禁用按钮：灰石底（次数用尽 / 不可点击时使用）</summary>
        public static Sprite ButtonDisabled
        {
            get
            {
                return CreateButtonSprite(UiTheme.PixelDisabledFill, UiTheme.PixelOutline, UiTheme.PixelDisabledLight, UiTheme.PixelDisabledDark, true);
            }
        }

        /// <summary>凹槽底：血条底槽 / 武器槽（内阴影在顶边、底部一道高光，像凹进去的石槽）</summary>
        public static Sprite Slot
        {
            get
            {
                return CreateSlotSprite(UiTheme.PanelDeep, UiTheme.PixelOutline, UiTheme.PixelSlotShadow, UiTheme.PixelSlotHighlight, true);
            }
        }

        /// <summary>条填充：白色底 + 顶部高光 + 底部暗边，配合 Image.color 染色（城门耐久 / Boss 血条）</summary>
        public static Sprite BarFill
        {
            get
            {
                return CreateBarFillSprite(Color.white, UiTheme.PixelBarShade);
            }
        }

        // ==================== 参数化生成器（带缓存） ====================

        /// <summary>生成 / 取缓存：面板底框精灵（外描边 + 左上亮边 + 右下暗边 + 可选铆钉与切角）</summary>
        /// <param name="fill">底框填充色</param>
        /// <param name="outline">最外侧描边色</param>
        /// <param name="lightEdge">左上亮边色</param>
        /// <param name="darkEdge">右下暗边色</param>
        /// <param name="rivets">是否在四角画像素铆钉</param>
        /// <param name="cutCorners">是否切掉四角像素（八边形轮廓）</param>
        /// <param name="size">贴图边长（像素），默认 32</param>
        /// <param name="border">九宫格边距（像素），默认 8</param>
        public static Sprite CreatePanelSprite(Color fill, Color outline, Color lightEdge, Color darkEdge, bool rivets = true, bool cutCorners = true, int size = UiTheme.PixelPatchSize, int border = UiTheme.PixelSlice)
        {
            string key = "panel|" + ToKey(fill) + "|" + ToKey(outline) + "|" + ToKey(lightEdge) + "|" + ToKey(darkEdge) + "|" + (rivets ? 1 : 0) + (cutCorners ? 1 : 0) + "|" + size + "x" + border;
            if (SpriteCache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            int edge = UiTheme.PixelOutlineWidth;
            int bevel = UiTheme.PixelBevelWidth;
            Color32[] pixels = CreatePixels(size, fill);
            DrawOutline(pixels, size, outline, edge);
            // 左上亮边（描边内侧，bevel 像素宽的受光倒角）
            FillBlock(pixels, size, edge, edge, bevel, size - edge * 2, lightEdge);
            FillBlock(pixels, size, edge, size - edge - bevel, size - edge * 2, bevel, lightEdge);
            // 右下暗边（描边内侧，bevel 像素宽的阴影倒角）
            FillBlock(pixels, size, size - edge - bevel, edge, bevel, size - edge * 2, darkEdge);
            FillBlock(pixels, size, edge, edge, size - edge * 2, bevel, darkEdge);
            if (cutCorners)
            {
                CutCorners(pixels, size, edge);
            }

            if (rivets)
            {
                int inset = edge + 3;
                int dot = 2;
                FillBlock(pixels, size, inset, inset, dot, dot, lightEdge);
                FillBlock(pixels, size, size - inset - dot, inset, dot, dot, lightEdge);
                FillBlock(pixels, size, inset, size - inset - dot, dot, dot, lightEdge);
                FillBlock(pixels, size, size - inset - dot, size - inset - dot, dot, dot, lightEdge);
            }

            return CacheSprite(key, pixels, size, border);
        }

        /// <summary>生成 / 取缓存：按钮底框精灵（外描边 + 顶面亮边 + 底面暗边 + 切角）</summary>
        /// <param name="fill">按钮底色</param>
        /// <param name="outline">外描边色</param>
        /// <param name="lightEdge">顶部亮边色（模拟受光面）</param>
        /// <param name="darkEdge">底部暗边色（模拟阴影面）</param>
        /// <param name="cutCorners">是否切角</param>
        /// <param name="size">贴图边长（像素）</param>
        /// <param name="border">九宫格边距（像素）</param>
        public static Sprite CreateButtonSprite(Color fill, Color outline, Color lightEdge, Color darkEdge, bool cutCorners = true, int size = UiTheme.PixelPatchSize, int border = UiTheme.PixelSlice)
        {
            string key = "button|" + ToKey(fill) + "|" + ToKey(outline) + "|" + ToKey(lightEdge) + "|" + ToKey(darkEdge) + "|" + (cutCorners ? 1 : 0) + "|" + size + "x" + border;
            if (SpriteCache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            int edge = UiTheme.PixelOutlineWidth;
            int bevel = UiTheme.PixelBevelWidth;
            Color32[] pixels = CreatePixels(size, fill);
            DrawOutline(pixels, size, outline, edge);
            // 顶部亮边（bevel 像素受光面）+ 左侧亮边
            FillBlock(pixels, size, edge, size - edge - bevel, size - edge * 2, bevel, lightEdge);
            FillBlock(pixels, size, edge, edge, bevel, size - edge * 2, lightEdge);
            // 底部暗边（bevel 像素阴影面）+ 右侧暗边
            FillBlock(pixels, size, edge, edge, size - edge * 2, bevel, darkEdge);
            FillBlock(pixels, size, size - edge - bevel, edge, bevel, size - edge * 2, darkEdge);
            if (cutCorners)
            {
                CutCorners(pixels, size, edge);
            }

            return CacheSprite(key, pixels, size, border);
        }

        /// <summary>生成 / 取缓存：凹槽精灵（外描边 + 顶部内阴影 + 底部高光，用于血条底槽与武器槽）</summary>
        /// <param name="fill">槽底色</param>
        /// <param name="outline">外描边色</param>
        /// <param name="shadow">顶部内阴影色（比底色更深）</param>
        /// <param name="highlight">底部高光色（比底色更亮）</param>
        /// <param name="cutCorners">是否切角</param>
        /// <param name="size">贴图边长（像素）</param>
        /// <param name="border">九宫格边距（像素）</param>
        public static Sprite CreateSlotSprite(Color fill, Color outline, Color shadow, Color highlight, bool cutCorners = true, int size = UiTheme.PixelPatchSize, int border = UiTheme.PixelSlice)
        {
            string key = "slot|" + ToKey(fill) + "|" + ToKey(outline) + "|" + ToKey(shadow) + "|" + ToKey(highlight) + "|" + (cutCorners ? 1 : 0) + "|" + size + "x" + border;
            if (SpriteCache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            int edge = UiTheme.PixelOutlineWidth;
            int bevel = UiTheme.PixelBevelWidth;
            Color32[] pixels = CreatePixels(size, fill);
            DrawOutline(pixels, size, outline, edge);
            // 顶部内阴影（bevel 像素），像被压进去的凹槽
            FillBlock(pixels, size, edge, size - edge - bevel, size - edge * 2, bevel, shadow);
            // 底部高光（1px），凹槽口的受光边
            FillBlock(pixels, size, edge, edge, size - edge * 2, 1, highlight);
            if (cutCorners)
            {
                CutCorners(pixels, size, edge);
            }

            return CacheSprite(key, pixels, size, border);
        }

        /// <summary>生成 / 取缓存：条填充精灵（白底可染色，顶部高光、底部暗边、左右暗边，九宫格边距 2px）</summary>
        /// <param name="highlight">顶部高光色（一般用白色）</param>
        /// <param name="shade">底部 / 两侧暗边色</param>
        public static Sprite CreateBarFillSprite(Color highlight, Color shade)
        {
            const int size = 8;
            const int border = 2;
            string key = "fill|" + ToKey(highlight) + "|" + ToKey(shade);
            if (SpriteCache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            Color32[] pixels = CreatePixels(size, Color.white);
            // 顶部 2 行高光（拉伸后仍固定在顶边）
            FillBlock(pixels, size, 0, size - border, size, border, highlight);
            // 底部 2 行暗边
            FillBlock(pixels, size, 0, 0, size, border, shade);
            // 左右各 2 列暗边（让填充有厚度）
            FillBlock(pixels, size, 0, 0, border, size, shade);
            FillBlock(pixels, size, size - border, 0, border, size, shade);
            return CacheSprite(key, pixels, size, border);
        }

        /// <summary>清空精灵缓存（纹理随资源卸载释放；正常游戏流程不需要调用）</summary>
        public static void ClearCache()
        {
            SpriteCache.Clear();
        }

        // ==================== 内部绘制工具 ====================

        // 创建整块填色的像素缓冲（数组下标 = y * size + x，原点在左下）
        private static Color32[] CreatePixels(int size, Color fill)
        {
            Color32[] pixels = new Color32[size * size];
            Color32 fillColor = fill;
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = fillColor;
            }

            return pixels;
        }

        // 把矩形区域填成指定颜色（自动裁剪越界部分）
        private static void FillBlock(Color32[] pixels, int size, int x, int y, int width, int height, Color color)
        {
            Color32 color32 = color;
            int xMin = Mathf.Max(0, x);
            int yMin = Mathf.Max(0, y);
            int xMax = Mathf.Min(size, x + width);
            int yMax = Mathf.Min(size, y + height);
            for (int py = yMin; py < yMax; py++)
            {
                int rowStart = py * size;
                for (int px = xMin; px < xMax; px++)
                {
                    pixels[rowStart + px] = color32;
                }
            }
        }

        // 绘制一圈外描边（粗细 = edge 像素）
        private static void DrawOutline(Color32[] pixels, int size, Color outline, int edge)
        {
            FillBlock(pixels, size, 0, 0, size, edge, outline);
            FillBlock(pixels, size, 0, size - edge, size, edge, outline);
            FillBlock(pixels, size, 0, 0, edge, size, outline);
            FillBlock(pixels, size, size - edge, 0, edge, size, outline);
        }

        // 切掉四角（edge × edge 像素透明），形成八边形像素轮廓
        private static void CutCorners(Color32[] pixels, int size, int edge)
        {
            Color32 clear = new Color32(0, 0, 0, 0);
            int corner = Mathf.Max(1, edge);
            // 左下
            FillBlock(pixels, size, 0, 0, corner, corner, clear);
            // 右下
            FillBlock(pixels, size, size - corner, 0, corner, corner, clear);
            // 左上
            FillBlock(pixels, size, 0, size - corner, corner, corner, clear);
            // 右上
            FillBlock(pixels, size, size - corner, size - corner, corner, corner, clear);
        }

        // 把像素缓冲写成贴图 + 九宫格精灵并写入缓存
        private static Sprite CacheSprite(string key, Color32[] pixels, int size, int border)
        {
            // 边距必须小于贴图半边长，否则九宫格无效
            int safeBorder = Mathf.Clamp(border, 2, Mathf.Max(2, size / 2 - 1));
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "UiPixel_" + key;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            // 第二个参数 true：上传后不再保留 CPU 端副本，省内存
            texture.Apply(false, true);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(safeBorder, safeBorder, safeBorder, safeBorder));
            sprite.name = "UiPixelSprite_" + key;
            SpriteCache[key] = sprite;
            return sprite;
        }

        // 把颜色转成缓存 key 用的十六进制串（不含 GC 压力场景：只在构建界面时调用）
        private static string ToKey(Color color)
        {
            return ColorUtility.ToHtmlStringRGBA(color);
        }
    }
}
