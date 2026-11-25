using System.Collections.Generic;
using UnityEngine;

// WorldNumbers.cs —— 世界空间飘字（伤害 / 金币 / 提示）：用 SpriteFactory 的数字点阵拼串，零字体依赖
namespace Gunplay.View
{
    /// <summary>
    /// 世界空间飘字系统：每个字符用一个池化 SpriteRenderer 显示，向上飘并淡出。
    /// 全部字形来自 SpriteFactory.Digit（5×7 点阵），因此不需要任何 TTF / TextMeshPro 资源。
    /// </summary>
    public sealed class WorldNumbers
    {
        /// <summary>单个字形的世界宽度（24 px = 0.24 unit）</summary>
        private const float GlyphWidth = 0.24f;

        /// <summary>字形对象池</summary>
        private readonly SpriteActorPool pool;
        /// <summary>正在播放的飘字</summary>
        private readonly List<NumberInstance> active = new List<NumberInstance>();
        /// <summary>飘字对象池（空闲复用）</summary>
        private readonly Stack<NumberInstance> free = new Stack<NumberInstance>();
        /// <summary>本帧已生成数量（性能护栏）</summary>
        private int spawnedThisFrame;
        /// <summary>本帧已生成数量所属帧号</summary>
        private int frameStamp = -1;

        /// <summary>创建飘字系统</summary>
        /// <param name="parent">挂载父节点</param>
        /// <param name="poolName">层级中显示的节点名</param>
        /// <param name="prewarmNumbers">预热飘字条数（每条最多按 5 个字符扩容）</param>
        public WorldNumbers(Transform parent, string poolName, int prewarmNumbers)
        {
            pool = new SpriteActorPool(parent, poolName, Mathf.Max(4, prewarmNumbers * 4));
        }

        /// <summary>尝试占用本帧的飘字名额（超过 maxPerFrame 时返回 false）</summary>
        public bool TryReserveSlot(int maxPerFrame)
        {
            int frame = Time.frameCount;
            if (frame != frameStamp)
            {
                frameStamp = frame;
                spawnedThisFrame = 0;
            }

            if (maxPerFrame > 0 && spawnedThisFrame >= maxPerFrame)
            {
                return false;
            }

            spawnedThisFrame++;
            return true;
        }

        /// <summary>生成一条飘字</summary>
        /// <param name="position">起始世界坐标</param>
        /// <param name="text">文本（仅支持 0-9 与 + - ! x）</param>
        /// <param name="color">颜色</param>
        /// <param name="scale">整体缩放（1 = 0.24 unit 宽的字）</param>
        /// <param name="life">存活时长（秒）</param>
        /// <param name="riseSpeed">上飘速度（世界单位/秒）</param>
        public void Spawn(Vector2 position, string text, Color color, float scale, float life, float riseSpeed)
        {
            if (string.IsNullOrEmpty(text) || life <= 0f)
            {
                return;
            }

            NumberInstance number = free.Count > 0 ? free.Pop() : new NumberInstance();
            number.Position = position;
            number.Color = color;
            number.Scale = Mathf.Max(0.1f, scale);
            number.Life = life;
            number.RiseSpeed = riseSpeed;
            number.Elapsed = 0f;
            number.Glyphs.Clear();

            float totalWidth = text.Length * GlyphWidth * number.Scale;
            float startX = -totalWidth * 0.5f + GlyphWidth * number.Scale * 0.5f;
            for (int i = 0; i < text.Length; i++)
            {
                SpriteActor glyph = pool.Rent();
                glyph.SetSprite(SpriteFactory.Digit(text[i]));
                glyph.SetOrder(ViewOrder.FloatingNumber);
                glyph.SetScale(number.Scale);
                glyph.SetPosition(new Vector2(position.x + startX + i * GlyphWidth * number.Scale, position.y));
                glyph.SetColor(color);
                number.Glyphs.Add(glyph);
            }

            active.Add(number);
        }

        /// <summary>每帧更新飘字位置与淡出</summary>
        public void Update(float deltaTime)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                NumberInstance number = active[i];
                number.Elapsed += deltaTime;
                float t = number.Life <= 0f ? 1f : Mathf.Clamp01(number.Elapsed / number.Life);
                if (t >= 1f)
                {
                    Release(number);
                    active.RemoveAt(i);
                    continue;
                }

                // 上飘速度随时间线性衰减，形成「抛出后减速」的手感
                float rise = number.RiseSpeed * number.Elapsed * (1f - t * 0.45f);
                float alpha = 1f - t * t;
                Color color = number.Color;
                color.a = number.Color.a * alpha;
                float scale = number.Scale * Mathf.Lerp(1f, 0.82f, t);

                float totalWidth = number.Glyphs.Count * GlyphWidth * scale;
                float startX = -totalWidth * 0.5f + GlyphWidth * scale * 0.5f;
                float baseY = number.Position.y + rise;
                for (int g = 0; g < number.Glyphs.Count; g++)
                {
                    SpriteActor glyph = number.Glyphs[g];
                    glyph.SetScale(scale);
                    glyph.SetColor(color);
                    glyph.SetPosition(new Vector2(number.Position.x + startX + g * GlyphWidth * scale, baseY));
                }
            }
        }

        /// <summary>清空全部飘字（切场景 / 重开一轮时调用）</summary>
        public void Clear()
        {
            for (int i = 0; i < active.Count; i++)
            {
                Release(active[i]);
            }

            active.Clear();
        }

        /// <summary>回收一条飘字的所有字形</summary>
        private void Release(NumberInstance number)
        {
            for (int i = 0; i < number.Glyphs.Count; i++)
            {
                pool.Return(number.Glyphs[i]);
            }

            number.Glyphs.Clear();
            free.Push(number);
        }

        /// <summary>一条飘字的运行时数据</summary>
        private sealed class NumberInstance
        {
            /// <summary>起始世界坐标</summary>
            public Vector2 Position;
            /// <summary>颜色（含 alpha）</summary>
            public Color Color;
            /// <summary>整体缩放</summary>
            public float Scale;
            /// <summary>存活时长（秒）</summary>
            public float Life;
            /// <summary>上飘速度（世界单位/秒）</summary>
            public float RiseSpeed;
            /// <summary>已播放时长（秒）</summary>
            public float Elapsed;
            /// <summary>当前占用的字形显示单元</summary>
            public readonly List<SpriteActor> Glyphs = new List<SpriteActor>(8);
        }
    }
}
