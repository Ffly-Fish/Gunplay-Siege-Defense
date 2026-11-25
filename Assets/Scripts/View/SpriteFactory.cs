using System.Collections.Generic;
using UnityEngine;

// SpriteFactory.cs —— 程序化美术工厂：运行时用 Texture2D 绘制全部 Sprite，不依赖任何外部美术资源
namespace Gunplay.View
{
    /// <summary>
    /// 程序化 Sprite 工厂。
    /// 全部图形由代码绘制成 Texture2D 再 Sprite.Create，同一 key 只生成一次并缓存；
    /// 像素与世界单位固定换算为 1 unit = 100 px（架构文档约定），因此 64 px 的图形在场景中是 0.64 unit。
    /// 图形统一为「亮色描边 + 扁平内填充」的 2D 卡通风格，运行时通过 SpriteRenderer.color 染色。
    /// </summary>
    public static class SpriteFactory
    {
        /// <summary>贴图像素与世界单位的换算比例（1 unit = 100 px）</summary>
        public const float PixelsPerUnit = 100f;

        /// <summary>Sprite 缓存：key → Sprite，保证同一 key 只生成一次</summary>
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>(System.StringComparer.Ordinal);

        /// <summary>数字 / 符号字形缓存：字符 → Sprite</summary>
        private static readonly Dictionary<char, Sprite> glyphCache = new Dictionary<char, Sprite>();

        /// <summary>字模点阵：每个字符 7 行 × 5 列，'1' 为实心</summary>
        private static readonly Dictionary<char, string[]> glyphPatterns = new Dictionary<char, string[]>
        {
            { '0', new string[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" } },
            { '1', new string[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" } },
            { '2', new string[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" } },
            { '3', new string[] { "11111", "00010", "00100", "00010", "00001", "10001", "01110" } },
            { '4', new string[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" } },
            { '5', new string[] { "11111", "10000", "11110", "00001", "00001", "10001", "01110" } },
            { '6', new string[] { "00110", "01000", "10000", "11110", "10001", "10001", "01110" } },
            { '7', new string[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" } },
            { '8', new string[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" } },
            { '9', new string[] { "01110", "10001", "10001", "01111", "00001", "00010", "01100" } },
            { '+', new string[] { "00000", "00100", "00100", "11111", "00100", "00100", "00000" } },
            { '-', new string[] { "00000", "00000", "00000", "11111", "00000", "00000", "00000" } },
            { '!', new string[] { "00100", "00100", "00100", "00100", "00100", "00000", "00100" } },
            { 'x', new string[] { "00000", "10001", "01010", "00100", "01010", "10001", "00000" } }
        };

        /// <summary>通用白色方块精灵（8×8 px，完全不透明）：拉伸后用作直线、血条、遮罩</summary>
        public static Sprite White
        {
            get { return Get("white"); }
        }

        /// <summary>取指定 key 的 Sprite；key 不存在时返回白色方块并打印一次警告</summary>
        /// <param name="key">图形 key（white / circle / ring / shield 等，见 BuildByKey）</param>
        public static Sprite Get(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return BuildWhite();
            }

            // 命中缓存（编辑器关闭域重载时贴图可能被销毁，这里顺带做失效检查）
            if (cache.TryGetValue(key, out Sprite cached))
            {
                if (cached != null && cached.texture != null)
                {
                    return cached;
                }

                cache.Remove(key);
            }

            Sprite sprite = BuildByKey(key);
            if (sprite == null)
            {
                Debug.LogWarning("[SpriteFactory] 未定义的图形 key: " + key + "，已回退为白色方块");
                sprite = BuildWhite();
            }

            cache[key] = sprite;
            return sprite;
        }

        /// <summary>取数字 / 符号字形精灵（5×7 点阵放大 4 倍，24×32 px）；不支持的字符回退为白色方块</summary>
        /// <param name="c">字符（0-9 以及 + - ! x）</param>
        public static Sprite Digit(char c)
        {
            if (glyphCache.TryGetValue(c, out Sprite cached) && cached != null && cached.texture != null)
            {
                return cached;
            }

            if (!glyphPatterns.TryGetValue(c, out string[] rows))
            {
                rows = glyphPatterns['0'];
            }

            Sprite sprite = BuildGlyph(c, rows);
            glyphCache[c] = sprite;
            return sprite;
        }

        // ============================ uGUI 兼容便捷 API ============================
        // 说明：文档 §5 给的旧签名（Rect / Circle / Coin）保留为薄封装，内部同样走缓存，
        // 因此 UI 侧无论是「Get(key)」还是「Rect(w,h,r)」都能拿到真实的程序化精灵。

        /// <summary>查询缓存并做失效检查；命中返回 true</summary>
        private static bool TryGetCached(string key, out Sprite sprite)
        {
            if (cache.TryGetValue(key, out sprite) && sprite != null && sprite.texture != null)
            {
                return true;
            }

            sprite = null;
            return false;
        }

        /// <summary>
        /// 生成任意尺寸的圆角矩形精灵（带 9 宫格边框，uGUI 里把 Image.type 设为 Sliced 即可任意缩放而不糊边）。
        /// </summary>
        /// <param name="width">宽度（像素，8~512）</param>
        /// <param name="height">高度（像素，8~512）</param>
        /// <param name="radius">圆角半径（像素，自动收敛到合法范围）</param>
        public static Sprite Rect(int width, int height, int radius)
        {
            int w = Mathf.Clamp(width, 8, 512);
            int h = Mathf.Clamp(height, 8, 512);
            int maxRadius = Mathf.Max(0, Mathf.Min(w, h) / 2 - 2);
            int r = Mathf.Clamp(radius, 0, maxRadius);
            string key = "rect_" + w + "_" + h + "_" + r;
            if (TryGetCached(key, out Sprite cached))
            {
                return cached;
            }

            float halfW = w * 0.5f - 0.5f;
            float halfH = h * 0.5f - 0.5f;
            Sprite sprite = MakeSliced(key, w, h, r, p =>
            {
                float sd = SdRoundBox(p, new Vector2(halfW, halfH), r);
                return BodyShade(sd, 2f, 0.94f);
            });
            cache[key] = sprite;
            return sprite;
        }

        /// <summary>生成任意像素直径的实心圆精灵（8~256 px）</summary>
        /// <param name="pixels">直径（像素）</param>
        public static Sprite Circle(int pixels)
        {
            int size = Mathf.Clamp(pixels, 8, 256);
            string key = "circle_" + size;
            if (TryGetCached(key, out Sprite cached))
            {
                return cached;
            }

            float radius = size * 0.5f - 1f;
            Sprite sprite = Make(key, size, size, p =>
            {
                float sd = SdCircle(p, Vector2.zero, radius);
                Color c = BodyShade(sd, radius * 0.16f, 0.74f);
                return AddHighlight(c, p, new Vector2(-radius * 0.34f, radius * 0.36f), radius * 0.26f, 0.55f);
            });
            cache[key] = sprite;
            return sprite;
        }

        /// <summary>生成任意像素直径的金币图标（12~256 px；≥32 px 才绘制内部「¥」符号）</summary>
        /// <param name="pixels">直径（像素）</param>
        public static Sprite Coin(int pixels)
        {
            int size = Mathf.Clamp(pixels, 12, 256);
            string key = "coin_" + size;
            if (TryGetCached(key, out Sprite cached))
            {
                return cached;
            }

            float radius = size * 0.5f - 1f;
            float scale = size / 48f;
            Sprite sprite = Make(key, size, size, p =>
            {
                float sd = SdCircle(p, Vector2.zero, radius);
                float alpha = Mathf.Clamp01(0.5f - sd);
                if (alpha <= 0.001f)
                {
                    return new Color(1f, 1f, 1f, 0f);
                }

                float rim = Mathf.Clamp01(-sd / Mathf.Max(1f, 2.5f * scale));
                float bright = Mathf.Lerp(1f, 0.84f, rim);
                float grooveRadius = radius * 0.71f;
                if (Mathf.Abs(p.magnitude - grooveRadius) < Mathf.Max(0.8f, 1.1f * scale))
                {
                    bright *= 0.76f;
                }

                if (size >= 32)
                {
                    bool symbol = (Mathf.Abs(p.x) < 1.6f * scale && p.y > -10f * scale && p.y < 4f * scale) ||
                                  (Mathf.Abs(p.y - 1f * scale) < 1.3f * scale && Mathf.Abs(p.x) < 7f * scale) ||
                                  (Mathf.Abs(p.y + 3f * scale) < 1.3f * scale && Mathf.Abs(p.x) < 5f * scale);
                    if (symbol)
                    {
                        bright = 1f;
                    }
                }

                return new Color(bright, bright, bright, alpha);
            });
            cache[key] = sprite;
            return sprite;
        }

        /// <summary>按 key 分派到具体绘制函数</summary>
        private static Sprite BuildByKey(string key)
        {
            switch (key)
            {
                case "white":
                    return BuildWhite();
                case "dot":
                    return BuildDot();
                case "circle":
                    return BuildCircle();
                case "ring":
                    return BuildRing();
                case "glow":
                    return BuildGlow();
                case "rounded":
                    return BuildRounded();
                case "capsule":
                    return BuildCapsule();
                case "triangle":
                    return BuildTriangle();
                case "diamond":
                    return BuildDiamond();
                case "star":
                    return BuildStar();
                case "shield":
                    return BuildShield();
                case "bullet":
                    return BuildBullet();
                case "grenade":
                    return BuildGrenade();
                case "crosshair":
                    return BuildCrosshair();
                case "skull":
                    return BuildSkull();
                case "coin":
                    return BuildCoin();
                case "heart":
                    return BuildHeart();
                case "gear":
                    return BuildGear();
                case "arrow_up":
                    return BuildArrowUp();
                case "lock":
                    return BuildLock();
                case "cannon":
                    return BuildCannon();
                case "wall":
                    return BuildWall();
                case "bg":
                    return BuildBackground();
                default:
                    return null;
            }
        }

        // ============================ 基础贴图 / 精灵创建 ============================

        /// <summary>把逐像素函数绘制成 Sprite（p 为以图形中心为原点的像素坐标，y 轴向上）</summary>
        private static Sprite Make(string key, int width, int height, System.Func<Vector2, Color> pixelFn)
        {
            Color32[] pixels = new Color32[width * height];
            float halfW = width * 0.5f;
            float halfH = height * 0.5f;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f - halfW, y + 0.5f - halfH);
                    pixels[y * width + x] = pixelFn(p);
                }
            }

            return MakeFromPixels(key, width, height, pixels);
        }

        /// <summary>由像素数组创建 Texture2D + Sprite（双线性过滤、关闭 mipmap、居中轴心）</summary>
        private static Sprite MakeFromPixels(string key, int width, int height, Color32[] pixels)
        {
            return MakeFromPixels(key, width, height, pixels, Vector4.zero);
        }

        /// <summary>由像素数组创建 Texture2D + Sprite，并写入 9 宫格边框（border 单位：像素，供 uGUI Sliced 使用）</summary>
        private static Sprite MakeFromPixels(string key, int width, int height, Color32[] pixels, Vector4 border)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "sf_tex_" + key;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.DontSave;
            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                border);
            sprite.name = "sf_" + key;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        /// <summary>把逐像素函数绘制成「带 9 宫格边框」的 Sprite（UI 面板 / 按钮底专用）</summary>
        private static Sprite MakeSliced(string key, int width, int height, float border, System.Func<Vector2, Color> pixelFn)
        {
            Color32[] pixels = new Color32[width * height];
            float halfW = width * 0.5f;
            float halfH = height * 0.5f;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f - halfW, y + 0.5f - halfH);
                    pixels[y * width + x] = pixelFn(p);
                }
            }

            return MakeFromPixels(key, width, height, pixels, new Vector4(border, border, border, border));
        }

        /// <summary>卡通着色：sd 为有符号距离（&lt;0 在形状内）；边缘 1 px 抗锯齿，边缘亮、内部按 fillBrightness 压暗</summary>
        private static Color BodyShade(float sd, float rimWidth, float fillBrightness)
        {
            float alpha = Mathf.Clamp01(0.5f - sd);
            if (alpha <= 0.001f)
            {
                return new Color(1f, 1f, 1f, 0f);
            }

            float t = Mathf.Clamp01(-sd / Mathf.Max(rimWidth, 0.01f));
            float b = Mathf.Lerp(1f, fillBrightness, t);
            return new Color(b, b, b, alpha);
        }

        /// <summary>在不透明像素上叠加一块白色高光（用于圆润的卡通质感）</summary>
        private static Color AddHighlight(Color baseColor, Vector2 p, Vector2 center, float radius, float strength)
        {
            if (baseColor.a <= 0.001f)
            {
                return baseColor;
            }

            float d = (p - center).magnitude - radius;
            float hl = Mathf.Clamp01(1f - d / 2.5f) * strength;
            return new Color(
                Mathf.Lerp(baseColor.r, 1f, hl),
                Mathf.Lerp(baseColor.g, 1f, hl),
                Mathf.Lerp(baseColor.b, 1f, hl),
                baseColor.a);
        }

        // ============================ 有符号距离函数 ============================

        /// <summary>圆的距离场</summary>
        private static float SdCircle(Vector2 p, Vector2 center, float radius)
        {
            return (p - center).magnitude - radius;
        }

        /// <summary>圆角矩形的距离场</summary>
        private static float SdRoundBox(Vector2 p, Vector2 halfSize, float radius)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x) - halfSize.x + radius, Mathf.Abs(p.y) - halfSize.y + radius);
            Vector2 qOutside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
            return qOutside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        /// <summary>线段的距离场</summary>
        private static float SdSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq <= 0.000001f)
            {
                return (p - a).magnitude;
            }

            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq);
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>任意凸/凹多边形的距离场（内部为负）</summary>
        private static float SdPoly(Vector2 p, Vector2[] poly)
        {
            float distance = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                distance = Mathf.Min(distance, SdSegment(p, poly[j], poly[i]));
                if ((poly[i].y > p.y) != (poly[j].y > p.y))
                {
                    float crossX = (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x;
                    if (p.x < crossX)
                    {
                        inside = !inside;
                    }
                }
            }

            return inside ? -distance : distance;
        }

        // ============================ 具体图形 ============================

        /// <summary>8×8 纯白方块（直线 / 血条 / 遮罩用）</summary>
        private static Sprite BuildWhite()
        {
            return Make("white", 8, 8, p => Color.white);
        }

        /// <summary>16×16 柔边小圆点（子弹拖尾 / 火花）</summary>
        private static Sprite BuildDot()
        {
            return Make("dot", 16, 16, p =>
            {
                float d = Mathf.Clamp01(p.magnitude / 7.5f);
                float a = Mathf.Pow(1f - d, 1.6f);
                return new Color(1f, 1f, 1f, a);
            });
        }

        /// <summary>64×64 实心圆（敌人身体 / 圆点子弹）</summary>
        private static Sprite BuildCircle()
        {
            const float radius = 26f;
            return Make("circle", 64, 64, p =>
            {
                float sd = SdCircle(p, Vector2.zero, radius);
                Color c = BodyShade(sd, radius * 0.16f, 0.74f);
                return AddHighlight(c, p, new Vector2(-radius * 0.34f, radius * 0.36f), radius * 0.26f, 0.55f);
            });
        }

        /// <summary>64×64 空心圆环（光环 / 冲击波）</summary>
        private static Sprite BuildRing()
        {
            return Make("ring", 64, 64, p =>
            {
                float sd = Mathf.Abs(p.magnitude - 26f) - 4.5f;
                return BodyShade(sd, 2.5f, 0.78f);
            });
        }

        /// <summary>128×128 径向光晕（枪口 / 爆炸 / 高光）</summary>
        private static Sprite BuildGlow()
        {
            const float radius = 62f;
            return Make("glow", 128, 128, p =>
            {
                float d = Mathf.Clamp01(p.magnitude / radius);
                float a = Mathf.Pow(1f - d, 2.4f);
                return new Color(1f, 1f, 1f, a);
            });
        }

        /// <summary>64×64 圆角矩形（UI 图标 / 盾兵底座）</summary>
        private static Sprite BuildRounded()
        {
            return Make("rounded", 64, 64, p =>
            {
                float sd = SdRoundBox(p, new Vector2(28f, 28f), 14f);
                Color c = BodyShade(sd, 3.5f, 0.8f);
                return AddHighlight(c, p, new Vector2(-11f, 12f), 9f, 0.35f);
            });
        }

        /// <summary>32×64 胶囊（长条按钮 / 炮台管）</summary>
        private static Sprite BuildCapsule()
        {
            return Make("capsule", 32, 64, p =>
            {
                float sd = SdRoundBox(p, new Vector2(12f, 28f), 12f);
                return BodyShade(sd, 3f, 0.8f);
            });
        }

        /// <summary>64×64 正三角形（飞行兵 / 箭头标记）</summary>
        private static Sprite BuildTriangle()
        {
            Vector2[] poly =
            {
                new Vector2(0f, 30f),
                new Vector2(27f, -24f),
                new Vector2(-27f, -24f)
            };
            return Make("triangle", 64, 64, p => BodyShade(SdPoly(p, poly), 3f, 0.76f));
        }

        /// <summary>64×64 菱形（快速兵）</summary>
        private static Sprite BuildDiamond()
        {
            Vector2[] poly =
            {
                new Vector2(0f, 30f),
                new Vector2(27f, 0f),
                new Vector2(0f, -30f),
                new Vector2(-27f, 0f)
            };
            return Make("diamond", 64, 64, p => BodyShade(SdPoly(p, poly), 3f, 0.76f));
        }

        /// <summary>64×64 五角星（奖励 / 连杀高光）</summary>
        private static Sprite BuildStar()
        {
            Vector2[] poly = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                // 从正上方开始顺时针交替外顶点 / 内顶点
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = (i % 2 == 0) ? 30f : 12.5f;
                poly[i] = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
            }

            return Make("star", 64, 64, p =>
            {
                Color c = BodyShade(SdPoly(p, poly), 2.5f, 0.8f);
                return AddHighlight(c, p, new Vector2(-6f, 10f), 6f, 0.4f);
            });
        }

        /// <summary>96×96 弧形盾片（盾兵护盾；正面朝 +Y，中间亮两端暗）</summary>
        private static Sprite BuildShield()
        {
            const int segments = 18;
            const float outer = 40f;
            const float thickness = 13f;
            const float halfAngle = 64f * Mathf.Deg2Rad;
            Vector2[] poly = new Vector2[segments * 2];
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.Lerp(-halfAngle, halfAngle, i / (float)(segments - 1));
                poly[i] = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * outer;
            }

            for (int i = segments - 1; i >= 0; i--)
            {
                float a = Mathf.Lerp(-halfAngle, halfAngle, i / (float)(segments - 1));
                poly[segments + (segments - 1 - i)] = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * (outer - thickness);
            }

            return Make("shield", 96, 96, p =>
            {
                float sd = SdPoly(p, poly);
                float alpha = Mathf.Clamp01(0.5f - sd);
                if (alpha <= 0.001f)
                {
                    return new Color(1f, 1f, 1f, 0f);
                }

                // 中间亮、两端暗，模拟金属盾片反光
                float angle = Mathf.Atan2(p.x, p.y);
                float k = Mathf.Clamp01(Mathf.Abs(angle) / halfAngle);
                float bright = Mathf.Lerp(1f, 0.6f, k * k);
                float rim = Mathf.Clamp01(-sd / 2.5f);
                bright *= Mathf.Lerp(1f, 0.82f, rim);
                return new Color(bright, bright, bright, alpha * 0.95f);
            });
        }

        /// <summary>32×48 尖头弹丸（直射子弹核心）</summary>
        private static Sprite BuildBullet()
        {
            Vector2[] tip =
            {
                new Vector2(0f, 23f),
                new Vector2(-7f, 6f),
                new Vector2(7f, 6f)
            };
            return Make("bullet", 32, 48, p =>
            {
                float body = SdRoundBox(new Vector2(p.x, p.y + 5f), new Vector2(6f, 14f), 5f);
                float sd = Mathf.Min(body, SdPoly(p, tip));
                return BodyShade(sd, 2.5f, 0.86f);
            });
        }

        /// <summary>48×48 手雷（抛物线弹药）</summary>
        private static Sprite BuildGrenade()
        {
            return Make("grenade", 48, 48, p =>
            {
                float body = SdCircle(p, new Vector2(0f, -3f), 15f);
                float cap = SdRoundBox(new Vector2(p.x, p.y - 12f), new Vector2(6f, 4.5f), 2f);
                float sd = Mathf.Min(body, cap);
                Color c = BodyShade(sd, 2.5f, 0.8f);
                // 网格纹路：压暗几条横竖线，强化「手雷」识别度
                if (sd < -2f && (Mathf.Abs(p.x) < 1.2f || Mathf.Abs(p.y + 3f) < 1.2f))
                {
                    c = new Color(c.r * 0.62f, c.g * 0.62f, c.b * 0.62f, c.a);
                }

                return AddHighlight(c, p, new Vector2(-6f, 1f), 4.5f, 0.5f);
            });
        }

        /// <summary>96×96 准星（瞄准点标记）</summary>
        private static Sprite BuildCrosshair()
        {
            return Make("crosshair", 96, 96, p =>
            {
                float ring = Mathf.Abs(p.magnitude - 27f) - 2.2f;
                float dot = SdCircle(p, Vector2.zero, 3f);
                float tick = float.MaxValue;
                for (int i = 0; i < 4; i++)
                {
                    float angle = i * Mathf.PI * 0.5f;
                    Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 perp = new Vector2(-dir.y, dir.x);
                    Vector2 local = new Vector2(Vector2.Dot(p, dir), Vector2.Dot(p, perp));
                    tick = Mathf.Min(tick, SdRoundBox(local - dir * 38f, new Vector2(8f, 1.8f), 1.5f));
                }

                float sd = Mathf.Min(Mathf.Min(ring, dot), tick);
                return BodyShade(sd, 1.6f, 0.9f);
            });
        }

        /// <summary>96×96 骷髅（Boss 施法者 / 危险标记）</summary>
        private static Sprite BuildSkull()
        {
            Vector2[] nose =
            {
                new Vector2(0f, 1f),
                new Vector2(-3.5f, -4f),
                new Vector2(3.5f, -4f)
            };
            return Make("skull", 96, 96, p =>
            {
                float cranium = SdCircle(p, new Vector2(0f, 8f), 25f);
                float jaw = SdRoundBox(new Vector2(p.x, p.y + 17f), new Vector2(16f, 12f), 6f);
                float sd = Mathf.Min(cranium, jaw);
                // 挖掉眼窝 / 鼻孔 / 牙齿缝
                sd = Mathf.Max(sd, -SdCircle(p, new Vector2(-10.5f, 8f), 7.5f));
                sd = Mathf.Max(sd, -SdCircle(p, new Vector2(10.5f, 8f), 7.5f));
                sd = Mathf.Max(sd, -SdPoly(p, nose));
                sd = Mathf.Max(sd, -SdRoundBox(new Vector2(p.x, p.y + 20f), new Vector2(13f, 1.2f), 1f));
                for (int i = -2; i <= 2; i++)
                {
                    sd = Mathf.Max(sd, -SdRoundBox(new Vector2(p.x - i * 6f, p.y + 17f), new Vector2(1f, 9f), 0.8f));
                }

                return BodyShade(sd, 3f, 0.92f);
            });
        }

        /// <summary>48×48 金币（击杀掉落提示）</summary>
        private static Sprite BuildCoin()
        {
            return Make("coin", 48, 48, p =>
            {
                float sd = SdCircle(p, Vector2.zero, 21f);
                float alpha = Mathf.Clamp01(0.5f - sd);
                if (alpha <= 0.001f)
                {
                    return new Color(1f, 1f, 1f, 0f);
                }

                float rim = Mathf.Clamp01(-sd / 2.5f);
                float bright = Mathf.Lerp(1f, 0.84f, rim);
                // 内圈凹槽
                if (Mathf.Abs(p.magnitude - 15f) < 1.1f)
                {
                    bright *= 0.76f;
                }

                // 中心「¥」符号
                bool symbol = (Mathf.Abs(p.x) < 1.6f && p.y > -10f && p.y < 4f) ||
                              (Mathf.Abs(p.y - 1f) < 1.3f && Mathf.Abs(p.x) < 7f) ||
                              (Mathf.Abs(p.y + 3f) < 1.3f && Mathf.Abs(p.x) < 5f);
                if (symbol)
                {
                    bright = 1f;
                }

                return new Color(bright, bright, bright, alpha);
            });
        }

        /// <summary>48×48 心形（血量 / 生命）</summary>
        private static Sprite BuildHeart()
        {
            Vector2[] bottom =
            {
                new Vector2(-19f, 3f),
                new Vector2(19f, 3f),
                new Vector2(0f, -21f)
            };
            return Make("heart", 48, 48, p =>
            {
                float left = SdCircle(p, new Vector2(-8.5f, 6f), 11f);
                float right = SdCircle(p, new Vector2(8.5f, 6f), 11f);
                float sd = Mathf.Min(Mathf.Min(left, right), SdPoly(p, bottom));
                Color c = BodyShade(sd, 2.5f, 0.82f);
                return AddHighlight(c, p, new Vector2(-8f, 10f), 5f, 0.5f);
            });
        }

        /// <summary>96×96 齿轮（设置按钮 / 机械感装饰）</summary>
        private static Sprite BuildGear()
        {
            return Make("gear", 96, 96, p =>
            {
                float ring = Mathf.Abs(p.magnitude - 29f) - 8f;
                float teeth = float.MaxValue;
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI * 0.25f;
                    Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 perp = new Vector2(-dir.y, dir.x);
                    Vector2 local = new Vector2(Vector2.Dot(p, dir), Vector2.Dot(p, perp));
                    teeth = Mathf.Min(teeth, SdRoundBox(local - dir * 36f, new Vector2(6f, 5f), 2f));
                }

                float sd = Mathf.Min(ring, teeth);
                sd = Mathf.Max(sd, -SdCircle(p, Vector2.zero, 11f));
                return BodyShade(sd, 2.5f, 0.85f);
            });
        }

        /// <summary>64×64 向上箭头（升级 / 提示）</summary>
        private static Sprite BuildArrowUp()
        {
            Vector2[] head =
            {
                new Vector2(0f, 30f),
                new Vector2(23f, 1f),
                new Vector2(-23f, 1f)
            };
            return Make("arrow_up", 64, 64, p =>
            {
                float shaft = SdRoundBox(new Vector2(p.x, p.y + 14f), new Vector2(8f, 14f), 2.5f);
                float sd = Mathf.Min(SdPoly(p, head), shaft);
                return BodyShade(sd, 2.5f, 0.88f);
            });
        }

        /// <summary>64×64 挂锁（未解锁 / 锁定标记）</summary>
        private static Sprite BuildLock()
        {
            return Make("lock", 64, 64, p =>
            {
                float body = SdRoundBox(new Vector2(p.x, p.y + 10f), new Vector2(17f, 13f), 4f);
                float shackle = Mathf.Max(Mathf.Abs(p.magnitude - 12f) - 3.5f, -(p.y - 3f));
                float sd = Mathf.Min(body, shackle);
                sd = Mathf.Max(sd, -SdCircle(p, new Vector2(0f, -8f), 3.5f));
                return BodyShade(sd, 2.5f, 0.85f);
            });
        }

        /// <summary>128×96 底部炮台（炮口基座，让玩家理解弹道来源）</summary>
        private static Sprite BuildCannon()
        {
            Vector2[] barrel =
            {
                new Vector2(-8f, 4f),
                new Vector2(8f, 4f),
                new Vector2(8f, 40f),
                new Vector2(-8f, 40f)
            };
            return Make("cannon", 128, 96, p =>
            {
                float basePlate = SdRoundBox(new Vector2(p.x, p.y + 36f), new Vector2(46f, 11f), 5f);
                float turret = SdRoundBox(new Vector2(p.x, p.y + 4f), new Vector2(26f, 16f), 12f);
                float sd = Mathf.Min(Mathf.Min(basePlate, turret), SdPoly(p, barrel));
                Color c = BodyShade(sd, 3f, 0.7f);
                return AddHighlight(c, p, new Vector2(-14f, 12f), 10f, 0.4f);
            });
        }

        /// <summary>256×64 砖石城墙（按城门耐久染色；顶部亮条 + 错缝砖块）</summary>
        private static Sprite BuildWall()
        {
            return Make("wall", 256, 64, p =>
            {
                float bright;
                if (p.y > 24f)
                {
                    // 城墙顶部亮条（城垛感）
                    bright = 1f;
                }
                else
                {
                    bright = 0.82f;
                    float rowF = (p.y + 32f) / 16f;
                    int row = Mathf.FloorToInt(rowF);
                    float yInRow = (p.y + 32f) - row * 16f;
                    float xOffset = (row % 2 == 0) ? 0f : 16f;
                    float xInBrick = Mathf.Repeat(p.x + 128f + xOffset, 32f);
                    bool mortar = yInRow < 2f || yInRow > 14f || xInBrick < 2f;
                    if (mortar)
                    {
                        bright = 0.52f;
                    }
                    else if (yInRow > 12f)
                    {
                        bright = 0.94f;
                    }
                }

                return new Color(bright, bright, bright, 1f);
            });
        }

        /// <summary>32×64 战场渐变背景（上亮下暗，中上方带一点暖色光晕）</summary>
        private static Sprite BuildBackground()
        {
            Color top = new Color(0.10f, 0.13f, 0.24f, 1f);
            Color bottom = new Color(0.035f, 0.045f, 0.09f, 1f);
            Color halo = new Color(0.16f, 0.20f, 0.34f, 1f);
            return Make("bg", 32, 64, p =>
            {
                float t = Mathf.InverseLerp(-32f, 32f, p.y);
                Color c = Color.Lerp(bottom, top, t * t);
                float glow = Mathf.Clamp01(1f - (p - new Vector2(0f, 18f)).magnitude / 44f);
                return Color.Lerp(c, halo, glow * 0.6f);
            });
        }

        // ============================ 数字 / 符号字形 ============================

        /// <summary>把 5×7 点阵绘制成 24×32 的字形 Sprite（每格 4×4 px，四周留 2 px 边距）</summary>
        private static Sprite BuildGlyph(char c, string[] rows)
        {
            const int cell = 4;
            const int cols = 5;
            const int rowCount = 7;
            int width = cols * cell + 4;
            int height = rowCount * cell + 4;
            Color32[] pixels = new Color32[width * height];
            Color32 clear = new Color32(255, 255, 255, 0);
            Color32 solid = new Color32(255, 255, 255, 255);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            for (int row = 0; row < rowCount && row < rows.Length; row++)
            {
                string line = rows[row];
                for (int col = 0; col < cols && col < line.Length; col++)
                {
                    if (line[col] != '1')
                    {
                        continue;
                    }

                    int x0 = 2 + col * cell;
                    int y0 = height - 2 - (row + 1) * cell;
                    for (int dy = 0; dy < cell; dy++)
                    {
                        for (int dx = 0; dx < cell; dx++)
                        {
                            pixels[(y0 + dy) * width + (x0 + dx)] = solid;
                        }
                    }
                }
            }

            return MakeFromPixels("glyph_" + c, width, height, pixels);
        }
    }
}
