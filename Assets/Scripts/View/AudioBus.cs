using System;
using System.Collections.Generic;
using Gunplay.Core;
using UnityEngine;

// AudioBus.cs —— 程序化音效总线：运行时用 AudioClip.Create 合成全部短音效，零外部音频资源
namespace Gunplay.View
{
    /// <summary>
    /// 程序化音效总线：所有音效由代码合成（正弦 / 方波 / 噪声 + 包络），不依赖任何外部音频文件。
    /// 用法：AudioBus.Instance.Play("shoot_pistol")；内部用少量 AudioSource 轮转播放，支持同帧限流。
    /// </summary>
    public sealed class AudioBus
    {
        /// <summary>默认采样率（22.05 kHz 足够表现打击感，内存与 CPU 都更省）</summary>
        private const int DefaultSampleRate = 22050;
        /// <summary>同时发声的 AudioSource 数量（超过则抢占最久未使用的）</summary>
        private const int VoiceCount = 10;
        /// <summary>起音时长（秒）—— 强制 2ms 淡入，消除爆音</summary>
        private const float AttackSeconds = 0.002f;
        /// <summary>结尾淡出比例（占整段时长的比例）</summary>
        private const float TailFadeRatio = 0.06f;
        /// <summary>音量上限（防止多个音效叠加削波）</summary>
        private const float MaxVolume = 0.9f;

        /// <summary>单例（Unity 主线程访问）</summary>
        private static AudioBus instance;

        /// <summary>单例入口（首次访问自动创建，未 Initialize 时播放为静默安全操作）</summary>
        public static AudioBus Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new AudioBus();
                }

                return instance;
            }
        }

        /// <summary>已合成音效缓存（key → 音频片段）</summary>
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        /// <summary>播放用 AudioSource 数组</summary>
        private readonly List<AudioSource> sources = new List<AudioSource>();
        /// <summary>各 AudioSource 上一次开始播放的时间（用于抢占最久未使用的）</summary>
        private readonly List<float> lastPlayTime = new List<float>();
        /// <summary>音频宿主 GameObject</summary>
        private GameObject host;
        /// <summary>当前采样率</summary>
        private int sampleRate = DefaultSampleRate;
        /// <summary>是否已初始化</summary>
        private bool initialized;
        /// <summary>是否静音</summary>
        private bool muted;
        /// <summary>本帧已播放的开火音效数量</summary>
        private int fireSoundsThisFrame;
        /// <summary>本帧计数对应的帧号</summary>
        private int fireFrameStamp = -1;
        /// <summary>当前使用的播放源下标</summary>
        private int nextSource;

        /// <summary>是否已完成初始化</summary>
        public bool IsInitialized
        {
            get { return initialized; }
        }

        /// <summary>是否处于静音状态</summary>
        public bool IsMuted
        {
            get { return muted; }
        }

        /// <summary>
        /// 初始化音频总线：创建宿主对象、AudioSource 池，并合成全部音效。
        /// 重复调用会直接返回（只允许初始化一次，避免重复分配）。
        /// </summary>
        /// <param name="sampleRate">采样率，默认 22050</param>
        public void Initialize(int sampleRate = DefaultSampleRate)
        {
            if (initialized)
            {
                return;
            }

            this.sampleRate = Mathf.Clamp(sampleRate, 8000, 48000);

            // 宿主对象：不参与渲染，只承载 AudioSource
            host = new GameObject("AudioBus");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideInHierarchy;

            CreateSources();
            BuildClips();

            initialized = true;
            AudioBusLog("AudioBus 初始化完成：" + clips.Count + " 个程序化音效，采样率 " + this.sampleRate);
        }

        /// <summary>创建播放源池（每个源负责一段声音，轮转复用）</summary>
        private void CreateSources()
        {
            for (int i = 0; i < VoiceCount; i++)
            {
                AudioSource source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;   // 纯 2D 音效
                source.volume = 1f;
                source.priority = 128;
                source.mute = muted;
                sources.Add(source);
                lastPlayTime.Add(-999f);
            }
        }

        /// <summary>把所有音效一次性合成进缓存</summary>
        private void BuildClips()
        {
            // ---- 开火音效 ----
            AddClip("shoot_pistol", Synthesize(new SynthSpec
            {
                Duration = 0.12f,
                StartFreq = 720f,
                EndFreq = 180f,
                NoiseMix = 0.35f,
                HarmonicMix = 0.22f,
                DecayCurve = 2.4f,
                Gain = 0.85f,
            }));
            AddClip("shoot_smg", Synthesize(new SynthSpec
            {
                Duration = 0.08f,
                StartFreq = 880f,
                EndFreq = 260f,
                NoiseMix = 0.30f,
                HarmonicMix = 0.18f,
                DecayCurve = 2.8f,
                Gain = 0.70f,
            }));
            AddClip("shoot_shotgun", Synthesize(new SynthSpec
            {
                Duration = 0.20f,
                StartFreq = 420f,
                EndFreq = 90f,
                NoiseMix = 0.55f,
                HarmonicMix = 0.10f,
                DecayCurve = 2.0f,
                Gain = 0.95f,
            }));
            AddClip("shoot_grenade", Synthesize(new SynthSpec
            {
                Duration = 0.20f,
                StartFreq = 260f,
                EndFreq = 70f,
                NoiseMix = 0.40f,
                HarmonicMix = 0.30f,
                DecayCurve = 1.8f,
                Gain = 0.90f,
            }));
            AddClip("shoot_rifle", Synthesize(new SynthSpec
            {
                Duration = 0.14f,
                StartFreq = 640f,
                EndFreq = 160f,
                NoiseMix = 0.40f,
                HarmonicMix = 0.20f,
                DecayCurve = 2.6f,
                Gain = 0.85f,
            }));
            AddClip("shoot_sniper", Synthesize(new SynthSpec
            {
                Duration = 0.32f,
                StartFreq = 320f,
                EndFreq = 60f,
                NoiseMix = 0.45f,
                HarmonicMix = 0.15f,
                DecayCurve = 1.6f,
                Gain = 1.0f,
            }));
            AddClip("shoot_laser", Synthesize(new SynthSpec
            {
                Duration = 0.22f,
                StartFreq = 1400f,
                EndFreq = 300f,
                NoiseMix = 0.10f,
                Square = true,
                HarmonicMix = 0.12f,
                DecayCurve = 1.4f,
                Gain = 0.55f,
            }));

            // ---- 战斗反馈 ----
            AddClip("explosion", Synthesize(new SynthSpec
            {
                Duration = 0.45f,
                StartFreq = 160f,
                EndFreq = 40f,
                NoiseMix = 0.75f,
                HarmonicMix = 0.08f,
                DecayCurve = 1.5f,
                Gain = 1.0f,
            }));
            AddClip("hit", Synthesize(new SynthSpec
            {
                Duration = 0.07f,
                StartFreq = 900f,
                EndFreq = 420f,
                NoiseMix = 0.25f,
                HarmonicMix = 0.30f,
                DecayCurve = 3.0f,
                Gain = 0.55f,
            }));
            AddClip("shield_block", Synthesize(new SynthSpec
            {
                Duration = 0.13f,
                StartFreq = 1800f,
                EndFreq = 1200f,
                NoiseMix = 0.08f,
                Square = true,
                HarmonicMix = 0.35f,
                DecayCurve = 2.2f,
                Gain = 0.45f,
            }));
            AddClip("shield_break", Synthesize(new SynthSpec
            {
                Duration = 0.30f,
                StartFreq = 2200f,
                EndFreq = 300f,
                NoiseMix = 0.45f,
                Square = true,
                HarmonicMix = 0.25f,
                DecayCurve = 1.6f,
                Gain = 0.60f,
            }));
            AddClip("kill", Synthesize(new SynthSpec
            {
                Duration = 0.15f,
                StartFreq = 520f,
                EndFreq = 110f,
                NoiseMix = 0.35f,
                HarmonicMix = 0.18f,
                DecayCurve = 2.2f,
                Gain = 0.65f,
            }));
            AddClip("gate_hit", Synthesize(new SynthSpec
            {
                Duration = 0.28f,
                StartFreq = 120f,
                EndFreq = 45f,
                NoiseMix = 0.30f,
                HarmonicMix = 0.45f,
                DecayCurve = 1.8f,
                Gain = 0.95f,
            }));

            // ---- 界面与流程 ----
            AddClip("level_clear", Synthesize(new SynthSpec
            {
                Duration = 0.62f,
                Notes = new float[] { 523.25f, 659.25f, 783.99f, 1046.50f },
                NoiseMix = 0f,
                HarmonicMix = 0.15f,
                DecayCurve = 0.7f,
                Gain = 0.55f,
            }));
            AddClip("ui_click", Synthesize(new SynthSpec
            {
                Duration = 0.06f,
                StartFreq = 1200f,
                EndFreq = 1500f,
                NoiseMix = 0f,
                Square = true,
                HarmonicMix = 0f,
                DecayCurve = 2.5f,
                Gain = 0.35f,
            }));
            AddClip("ui_buy", Synthesize(new SynthSpec
            {
                Duration = 0.20f,
                Notes = new float[] { 880f, 1320f },
                NoiseMix = 0f,
                HarmonicMix = 0.10f,
                DecayCurve = 1.2f,
                Gain = 0.45f,
            }));
            AddClip("game_over", Synthesize(new SynthSpec
            {
                Duration = 0.80f,
                Notes = new float[] { 440f, 349.23f, 261.63f, 196f },
                NoiseMix = 0f,
                HarmonicMix = 0.20f,
                DecayCurve = 0.8f,
                Gain = 0.55f,
            }));
            AddClip("combo_up", Synthesize(new SynthSpec
            {
                Duration = 0.24f,
                Notes = new float[] { 660f, 880f, 1174.66f },
                NoiseMix = 0f,
                HarmonicMix = 0.18f,
                DecayCurve = 1.0f,
                Gain = 0.45f,
            }));
        }

        /// <summary>合成并缓存一个音效</summary>
        private void AddClip(string key, float[] samples)
        {
            AudioClip clip = AudioClip.Create(key, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            clips[key] = clip;
        }

        /// <summary>
        /// 播放音效。
        /// </summary>
        /// <param name="key">音效名（未注册的 key 静默忽略）</param>
        /// <param name="volume">音量 0~1</param>
        /// <param name="pitch">音高 0.5~2（1 为原调）</param>
        public void Play(string key, float volume = 1f, float pitch = 1f)
        {
            if (muted || string.IsNullOrEmpty(key))
            {
                return;
            }

            // 惰性初始化：菜单里的 UI 点击音可能早于 GameBootstrap 的初始化，这里兜底
            if (!initialized && !TryInitialize())
            {
                return;
            }

            // 外部音频优先：Resources/Art/Audio/<key>.wav 存在时直接播放，缺失才用合成音效
            AudioClip clip = ArtLibrary.GetAudioClip(key);
            if (clip == null)
            {
                clips.TryGetValue(key, out clip);
            }

            if (clip == null)
            {
                return;
            }

            AudioSource source = PickSource();
            if (source == null)
            {
                return;
            }

            source.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
            source.volume = Mathf.Clamp01(volume) * MaxVolume;
            source.clip = clip;
            source.Play();
        }

        /// <summary>播放开火音效（按 maxFireSoundsPerFrame 做同帧限流，避免密集弹幕糊成噪声）</summary>
        /// <param name="weaponId">武器 id（pistol/smg/shotgun/grenade/rifle/sniper/laser）</param>
        /// <param name="volume">音量</param>
        /// <returns>是否真的播放了</returns>
        public bool PlayWeaponFire(string weaponId, float volume = 0.8f)
        {
            if (!TryReserveFireSoundSlot())
            {
                return false;
            }

            string key = ShootKeyForWeapon(weaponId);
            float pitch = 0.94f + UnityEngine.Random.value * 0.12f;  // 轻微随机音高，避免机械重复感
            Play(key, volume, pitch);
            return true;
        }

        /// <summary>惰性初始化（首次播放时自动建立播放源与音效；失败则本次静默跳过）</summary>
        private bool TryInitialize()
        {
            try
            {
                Initialize();
            }
            catch (Exception e)
            {
                AudioBusLog("AudioBus 惰性初始化失败：" + e.Message);
            }

            return initialized;
        }

        /// <summary>播放界面点击音（静态便捷入口，等价于 Instance.Play("ui_click")）</summary>
        public static void PlayUiClick()
        {
            Instance.Play("ui_click", 0.7f, 1f);
        }

        /// <summary>播放购买成功音（静态便捷入口，等价于 Instance.Play("ui_buy")）</summary>
        public static void PlayUiBuy()
        {
            Instance.Play("ui_buy", 0.8f, 1f);
        }

        /// <summary>播放关卡通过音</summary>
        public static void PlayLevelClear()
        {
            Instance.Play("level_clear", 0.9f, 1f);
        }

        /// <summary>播放游戏结束音</summary>
        public static void PlayGameOver()
        {
            Instance.Play("game_over", 0.9f, 1f);
        }

        /// <summary>尝试占用一个开火音效名额（同帧超过上限返回 false）</summary>
        public bool TryReserveFireSoundSlot()
        {
            int frame = Time.frameCount;
            if (frame != fireFrameStamp)
            {
                fireFrameStamp = frame;
                fireSoundsThisFrame = 0;
            }

            int limit = MaxFireSoundsPerFrame();
            if (fireSoundsThisFrame >= limit)
            {
                return false;
            }

            fireSoundsThisFrame++;
            return true;
        }

        /// <summary>读取配置中的开火音效同帧上限（配置未就绪时回退到 2）</summary>
        private static int MaxFireSoundsPerFrame()
        {
            try
            {
                GameRuntime runtime = GameRuntime.Instance;
                if (runtime != null && runtime.Config != null && runtime.Config.Tuning != null && runtime.Config.Tuning.combat != null)
                {
                    return Mathf.Max(1, Mathf.RoundToInt(runtime.Config.Tuning.combat.maxFireSoundsPerFrame));
                }
            }
            catch (Exception)
            {
                // 配置尚未加载（例如菜单早期）时走默认值，绝不抛异常打断表现层
            }

            return 2;
        }

        /// <summary>武器 id → 开火音效 key</summary>
        public static string ShootKeyForWeapon(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId))
            {
                return "shoot_pistol";
            }

            switch (weaponId)
            {
                case "pistol":
                    return "shoot_pistol";
                case "smg":
                    return "shoot_smg";
                case "shotgun":
                    return "shoot_shotgun";
                case "grenade":
                    return "shoot_grenade";
                case "rifle":
                    return "shoot_rifle";
                case "sniper":
                    return "shoot_sniper";
                case "laser":
                    return "shoot_laser";
                default:
                    return "shoot_pistol";
            }
        }

        /// <summary>取一个可用的播放源：优先空闲的，其次抢占最久未播放的</summary>
        private AudioSource PickSource()
        {
            float now = Time.unscaledTime;
            AudioSource best = null;
            float oldest = float.MaxValue;
            int oldestIndex = 0;

            for (int i = 0; i < sources.Count; i++)
            {
                AudioSource source = sources[i];
                if (source == null)
                {
                    continue;
                }

                if (!source.isPlaying)
                {
                    nextSource = (i + 1) % sources.Count;
                    return source;
                }

                if (lastPlayTime[i] < oldest)
                {
                    oldest = lastPlayTime[i];
                    best = source;
                    oldestIndex = i;
                }
            }

            if (best != null)
            {
                lastPlayTime[oldestIndex] = now;
                nextSource = (oldestIndex + 1) % sources.Count;
            }

            return best;
        }

        /// <summary>设置静音（同时作用于已存在的播放源）</summary>
        public void SetMuted(bool value)
        {
            muted = value;
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i] != null)
                {
                    sources[i].mute = value;
                }
            }
        }

        /// <summary>停止所有播放（切场景 / 重开时调用）</summary>
        public void StopAll()
        {
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i] != null)
                {
                    sources[i].Stop();
                }
            }
        }

        /// <summary>合成一段音效采样数据</summary>
        private float[] Synthesize(SynthSpec spec)
        {
            int count = Mathf.Max(8, Mathf.RoundToInt(Mathf.Max(0.01f, spec.Duration) * sampleRate));
            float[] data = new float[count];
            System.Random random = new System.Random(spec.Seed);
            float phase = 0f;
            float tailStart = 1f - TailFadeRatio;

            for (int i = 0; i < count; i++)
            {
                float time = (float)i / sampleRate;
                float progress = (float)i / count;

                // 频率：默认线性扫频；若给了音符序列，则按时长均分切换音符
                float freq = Mathf.Lerp(spec.StartFreq, spec.EndFreq, progress);
                if (spec.Notes != null && spec.Notes.Length > 0)
                {
                    int noteIndex = Mathf.Clamp((int)(progress * spec.Notes.Length), 0, spec.Notes.Length - 1);
                    freq = spec.Notes[noteIndex];
                }

                phase += Mathf.Max(1f, freq) / sampleRate;
                if (phase >= 1f)
                {
                    phase -= Mathf.Floor(phase);
                }

                float tone = Mathf.Sin(phase * Mathf.PI * 2f);
                if (spec.Square)
                {
                    tone = tone >= 0f ? 0.75f : -0.75f;
                }

                float harmonic = Mathf.Sin(phase * Mathf.PI * 4f) * spec.HarmonicMix;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                float mixed = tone * (1f - spec.NoiseMix) + noise * spec.NoiseMix + harmonic;

                // 包络：2ms 淡入 → 指数衰减 → 结尾淡出（消除爆音）
                float attack = Mathf.Clamp01(time / AttackSeconds);
                float decay = Mathf.Pow(1f - progress, Mathf.Max(0.1f, spec.DecayCurve));
                float tail = progress <= tailStart ? 1f : Mathf.Clamp01((1f - progress) / TailFadeRatio);
                mixed *= attack * decay * tail * spec.Gain;

                data[i] = Mathf.Clamp(mixed, -1f, 1f);
            }

            return data;
        }

        /// <summary>仅在编辑器下打印日志（避免 WebGL 运行时产生日志开销）</summary>
        private static void AudioBusLog(string message)
        {
#if UNITY_EDITOR
            Debug.Log("[AudioBus] " + message);
#endif
        }

        /// <summary>合成参数：扫频 / 噪声混合 / 谐波 / 音符序列 / 包络</summary>
        private struct SynthSpec
        {
            /// <summary>时长（秒）</summary>
            public float Duration;
            /// <summary>起始频率（Hz）</summary>
            public float StartFreq;
            /// <summary>结束频率（Hz）</summary>
            public float EndFreq;
            /// <summary>噪声混合量 0~1（越高越像爆炸 / 摩擦音）</summary>
            public float NoiseMix;
            /// <summary>二次谐波混合量（增加金属 / 厚实感）</summary>
            public float HarmonicMix;
            /// <summary>衰减指数（越大衰减越快）</summary>
            public float DecayCurve;
            /// <summary>整体增益</summary>
            public float Gain;
            /// <summary>是否为方波（用于激光 / 界面提示音）</summary>
            public bool Square;
            /// <summary>音符序列（非空时代替扫频，用于过关 / 失败 / 升档音阶）</summary>
            public float[] Notes;
            /// <summary>噪声随机种子（保证每次构建音色一致）</summary>
            public int Seed;
        }
    }
}
