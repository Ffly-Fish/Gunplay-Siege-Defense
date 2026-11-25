using UnityEditor;
using UnityEngine;

// ArtImportPostprocess.cs —— 外部美术资源导入规则：像素风（点采样、关闭 mipmap、无压缩）
namespace Gunplay.EditorTools
{
    /// <summary>
    /// 美术资源导入后处理。
    /// 只作用于 Assets/Resources/Art/ 下的贴图：统一按像素风精灵导入（Point 采样、关闭 mipmap、无压缩、PPU 50）。
    /// 同时把 Art/Audio 下的音频按手机小游戏口径导入（单声道、Vorbis 压缩），控制包体。
    /// 这样用户把网上下载的 PNG 直接丢进该目录即可获得正确效果，无需手工改 Inspector。
    /// </summary>
    public sealed class ArtImportPostprocess : AssetPostprocessor
    {
        /// <summary>受管的资源目录前缀</summary>
        private const string ArtRoot = "Assets/Resources/Art/";

        /// <summary>像素与世界单位换算：50 像素 = 1 世界单位（敌人 30px 约 0.6 单位，Boss 64px 约 1.3 单位）</summary>
        private const float PixelsPerUnit = 50f;

        /// <summary>受管的音频资源目录前缀</summary>
        private const string ArtAudioRoot = "Assets/Resources/Art/Audio/";

        /// <summary>音效压缩质量（0-1，手机小游戏取 0.6 平衡包体与听感）</summary>
        private const float AudioQuality = 0.6f;

        /// <summary>贴图最长边上限（超过会被缩放，抖音包体友好）</summary>
        private const int MaxTextureSize = 2048;

        /// <summary>
        /// 贴图导入前设置。
        /// </summary>
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot, System.StringComparison.Ordinal))
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = MaxTextureSize;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            // UI 与 2D 精灵都不需要 sRGB 差异；保持默认 sRGB 即可
        }

        /// <summary>
        /// 音频导入前设置：单声道 + Vorbis 压缩，避免 96kHz 立体声素材拖大包体。
        /// </summary>
        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(ArtAudioRoot, System.StringComparison.Ordinal))
            {
                return;
            }

            AudioImporter importer = (AudioImporter)assetImporter;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;

            // 单声道：守城玩法音效不需要立体声定位，省一半体积
            importer.forceToMono = true;

            // Vorbis 压缩：短音效解码开销可接受，包体比 PCM 小一个数量级
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = AudioQuality;
            settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            settings.sampleRateOverride = 44100;

            importer.defaultSampleSettings = settings;
        }
    }
}
