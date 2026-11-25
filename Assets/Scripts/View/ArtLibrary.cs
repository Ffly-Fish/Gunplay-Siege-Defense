using System.Collections.Generic;
using UnityEngine;

// ArtLibrary.cs —— 外部美术资源库：从 Resources/Art 装载 PNG 精灵与音频，缺失时由调用方回退到程序化美术
namespace Gunplay.View
{
    /// <summary>
    /// 外部资源装载器（美术 / 音频）。
    /// 目录约定（全部相对 Assets/Resources）：
    /// - Art/Sprites/&lt;key&gt;.png            单帧精灵，例如 Art/Sprites/enemy_grunt.png
    /// - Art/Sprites/&lt;key&gt;_0.png、_1.png  同一动作的帧序列（按序号连续，缺号即结束），例如 enemy_grunt_0.png
    /// - Art/Audio/&lt;key&gt;.wav|.ogg|.mp3     音效，例如 Art/Audio/shoot_pistol.wav
    /// 所有资源缺失时返回 null，调用方自动回退到程序化图形 / 合成音效；因此少放文件不会报错，只是保持占位美术。
    /// </summary>
    public static class ArtLibrary
    {
        /// <summary>精灵资源根目录（相对 Resources，不加扩展名）</summary>
        public const string SpriteRoot = "Art/Sprites";

        /// <summary>音频资源根目录（相对 Resources，不加扩展名）</summary>
        public const string AudioRoot = "Art/Audio";

        /// <summary>一个动作最多读取的帧数（防止无限探测）</summary>
        public const int MaxFrames = 12;

        /// <summary>已装载精灵缓存：key → Sprite（null 也缓存，避免每帧重复 Resources.Load）</summary>
        private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>(System.StringComparer.Ordinal);

        /// <summary>已装载帧序列缓存：key → Sprite[]（空数组表示没有外部帧）</summary>
        private static readonly Dictionary<string, Sprite[]> frameCache = new Dictionary<string, Sprite[]>(System.StringComparer.Ordinal);

        /// <summary>已装载音频缓存：key → AudioClip（null 也缓存）</summary>
        private static readonly Dictionary<string, AudioClip> audioCache = new Dictionary<string, AudioClip>(System.StringComparer.Ordinal);

        /// <summary>是否输出缺失日志（默认关闭，避免每帧刷屏；校验工具会显式读取缺失清单）</summary>
        public static bool VerboseLogging { get; set; }

        /// <summary>是否已经存在外部精灵（任一张图片都算）</summary>
        public static bool HasAnySprite
        {
            get { return Resources.Load<Texture2D>(SpriteRoot + "/enemy_grunt") != null; }
        }

        /// <summary>
        /// 取外部精灵；不存在返回 null。
        /// </summary>
        /// <param name="key">资源 key（不带扩展名，见素材清单）</param>
        public static Sprite GetSprite(string key)
        {
            TryGetSprite(key, out Sprite sprite);
            return sprite;
        }

        /// <summary>
        /// 尝试取外部精灵；不存在返回 false 且 sprite 为 null。
        /// </summary>
        /// <param name="key">资源 key（不带扩展名）</param>
        /// <param name="sprite">命中的精灵</param>
        public static bool TryGetSprite(string key, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (spriteCache.TryGetValue(key, out Sprite cached))
            {
                sprite = cached;
                return sprite != null;
            }

            Sprite loaded = Resources.Load<Sprite>(SpriteRoot + "/" + key);
            spriteCache[key] = loaded;
            if (loaded == null && VerboseLogging)
            {
                Debug.Log("[ArtLibrary] 未找到外部精灵：" + SpriteRoot + "/" + key + "（将使用程序化图形）");
            }

            sprite = loaded;
            return loaded != null;
        }

        /// <summary>
        /// 取外部帧序列（&lt;key&gt;、&lt;key&gt;_1、&lt;key&gt;_2…）；没有则返回空数组。
        /// 单帧图也会返回长度为 1 的数组，便于调用方统一处理。
        /// </summary>
        /// <param name="key">资源 key（不带扩展名）</param>
        public static Sprite[] GetFrames(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return System.Array.Empty<Sprite>();
            }

            if (frameCache.TryGetValue(key, out Sprite[] cached))
            {
                return cached;
            }

            List<Sprite> frames = new List<Sprite>(MaxFrames);
            Sprite first = GetSprite(key);
            if (first != null)
            {
                frames.Add(first);
            }

            for (int i = 1; i < MaxFrames; i++)
            {
                Sprite frame = Resources.Load<Sprite>(SpriteRoot + "/" + key + "_" + i);
                if (frame == null)
                {
                    break;
                }

                frames.Add(frame);
            }

            Sprite[] result = frames.ToArray();
            frameCache[key] = result;
            return result;
        }

        /// <summary>
        /// 取外部音频片段；不存在返回 null（调用方回退到合成音效）。
        /// </summary>
        /// <param name="key">资源 key（不带扩展名）</param>
        public static AudioClip GetAudioClip(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            if (audioCache.TryGetValue(key, out AudioClip cached))
            {
                return cached;
            }

            AudioClip clip = Resources.Load<AudioClip>(AudioRoot + "/" + key);
            audioCache[key] = clip;
            if (clip == null && VerboseLogging)
            {
                Debug.Log("[ArtLibrary] 未找到外部音效：" + AudioRoot + "/" + key + "（将使用合成音效）");
            }

            return clip;
        }

        /// <summary>
        /// 清空缓存（编辑器热重载或运行期替换资源后调用）。
        /// </summary>
        public static void ClearCache()
        {
            spriteCache.Clear();
            frameCache.Clear();
            audioCache.Clear();
        }
    }
}
