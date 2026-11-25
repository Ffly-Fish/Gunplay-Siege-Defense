using System.Collections.Generic;
using Gunplay.Combat;
using Gunplay.Core;
using UnityEngine;

// BattleView.cs —— 战斗表现层：只读模拟层状态 + 订阅一次性事件，负责把敌人 / 子弹 / 激光 / 特效 / 音效 / 飘字画出来
namespace Gunplay.View
{
    /// <summary>
    /// 战斗表现层。
    /// 数据来源只有两个：<see cref="GameRuntime"/> 暴露的实时列表（Enemies / Bullets / Lasers）与 EventBus 的一次性事件。
    /// 本类绝不修改模拟层状态，所有可见元素都走对象池复用，运行期不产生 Instantiate / Destroy 抖动。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleView : MonoBehaviour
    {
        // ============================ 常量 ============================

        /// <summary>受击闪白从 1 衰减到 0 的时长（与模拟层 Enemy.HitFlash 初值 0.1 秒一致）</summary>
        private const float HitFlashDuration = 0.1f;

        /// <summary>城墙受击高光的持续时间（秒）</summary>
        private const float WallFlashDuration = 0.3f;

        /// <summary>命中音效的最小间隔（秒），避免霰弹多弹丸同帧叠音爆音</summary>
        private const float HitSoundInterval = 0.045f;

        /// <summary>敌人身体可见直径相对碰撞直径的放大倍率（略大于碰撞体，手感更清晰）</summary>
        private const float DefaultEnemySizeMultiplier = 1.15f;

        /// <summary>纯白方块精灵（8 px）在缩放 1 时的世界边长，用于把世界尺寸换算成 Transform.localScale</summary>
        private const float WhiteSpriteSize = 0.08f;

        /// <summary>实心圆精灵（circle）在缩放 1 时的可见直径（世界单位）</summary>
        private const float CircleVisibleDiameter = 0.52f;

        /// <summary>弧形盾片精灵（shield）在缩放 1 时的可见宽度（世界单位）</summary>
        private const float ShieldVisibleWidth = 0.72f;

        /// <summary>冲击环精灵（ring）在缩放 1 时的可见直径（世界单位）</summary>
        private const float RingVisibleDiameter = 0.52f;

        /// <summary>径向光晕精灵（glow）在缩放 1 时的可见直径（世界单位）</summary>
        private const float GlowVisibleDiameter = 1.24f;

        /// <summary>敌人血条的世界高度</summary>
        private const float HpBarHeight = 0.085f;

        /// <summary>战场背景覆盖宽度（世界单位，比屏幕略宽避免边缘露底）</summary>
        private const float BackgroundWidth = 11.2f;

        /// <summary>战场背景覆盖高度（世界单位）</summary>
        private const float BackgroundHeight = 19.6f;

        // ============================ Inspector 配置 ============================

        [Header("场景静态元素")]
        [Tooltip("是否在运行时自动生成背景、城墙与炮台（关闭后可由场景自行摆放静态美术）")]
        [SerializeField] private bool buildStaticArt = true;

        [Header("敌人表现")]
        [Tooltip("敌人身体可见直径相对碰撞直径的放大倍率，1 = 与碰撞体同宽")]
        [SerializeField, Range(0.8f, 2f)] private float enemySizeMultiplier = DefaultEnemySizeMultiplier;

        [Header("血条")]
        [Tooltip("敌人血条宽度相对身体可见直径的倍率")]
        [SerializeField, Range(0.8f, 2.5f)] private float hpBarWidthRatio = 1.15f;

        [Tooltip("满血时是否也显示血条（关闭则只有受伤或 Boss 才显示）")]
        [SerializeField] private bool alwaysShowHpBar = false;

        [Header("瞄准与炮口")]
        [Tooltip("是否显示瞄准辅助线（仅在按住瞄准时出现）")]
        [SerializeField] private bool showAimLine = true;

        [Tooltip("瞄准辅助线的最大长度（世界单位）")]
        [SerializeField, Range(2f, 20f)] private float aimLineMaxLength = 15f;

        [Tooltip("准星整体缩放倍率")]
        [SerializeField, Range(0.2f, 2f)] private float crosshairScale = 0.75f;

        [Header("特效")]
        [Tooltip("是否启用命中 / 击杀 / 爆炸等瞬时特效（低端机可整体关闭）")]
        [SerializeField] private bool enableFx = true;

        [Tooltip("每帧最多生成的命中火花数量（性能护栏）")]
        [SerializeField, Range(0, 40)] private int maxSparksPerFrame = 12;

        [Tooltip("每帧最多播放的击杀音效数量（防止同屏大量死亡时音效叠爆）")]
        [SerializeField, Range(1, 8)] private int maxKillSoundsPerFrame = 2;

        [Header("镜头反馈")]
        [Tooltip("是否启用受击镜头轻微抖动")]
        [SerializeField] private bool enableCameraShake = true;

        [Tooltip("城门受击时的镜头抖动强度（世界单位）")]
        [SerializeField, Range(0f, 0.5f)] private float gateShakeStrength = 0.16f;

        [Tooltip("城门受击镜头抖动的持续时间（秒）")]
        [SerializeField, Range(0.05f, 1f)] private float gateShakeDuration = 0.22f;

        [Header("音效")]
        [Tooltip("是否播放战斗音效（关闭后仅保留静音，不影响其他界面音效）")]
        [SerializeField] private bool enableAudio = true;

        [Tooltip("战斗音效整体音量倍率")]
        [SerializeField, Range(0f, 1f)] private float audioVolume = 0.8f;

        [Header("对象池预热")]
        [Tooltip("敌人视图预热数量（每个视图含身体 / 护盾 / 血条底 / 血条填充）")]
        [SerializeField, Range(0, 120)] private int enemyViewPrewarm = 32;

        [Tooltip("子弹视图预热数量")]
        [SerializeField, Range(0, 200)] private int bulletViewPrewarm = 64;

        [Tooltip("激光线段预热数量（每段含核心与外发光）")]
        [SerializeField, Range(0, 12)] private int laserPrewarm = 4;

        [Tooltip("瞬时特效预热数量")]
        [SerializeField, Range(0, 80)] private int fxPrewarm = 32;

        [Tooltip("飘字预热条数（每条按 4 个字形扩容）")]
        [SerializeField, Range(0, 40)] private int numberPrewarm = 16;

        // ============================ 运行期状态 ============================

        /// <summary>当前连接的运行期实例（GameBootstrap 在 Awake 创建，可能晚于本组件 Start）</summary>
        private GameRuntime runtime;

        /// <summary>是否已完成事件订阅</summary>
        private bool subscribed;

        /// <summary>是否已生成静态表现元素与对象池</summary>
        private bool staticsBuilt;

        /// <summary>音效总线是否已初始化</summary>
        private bool audioInitialized;

        /// <summary>所有动态显示单元的父节点（本组件的子物体）</summary>
        private Transform root;

        /// <summary>敌人身体对象池</summary>
        private SpriteActorPool enemyBodyPool;

        /// <summary>盾兵护盾对象池</summary>
        private SpriteActorPool enemyShieldPool;

        /// <summary>敌人血条底对象池</summary>
        private SpriteActorPool enemyHpBackPool;

        /// <summary>敌人血条填充对象池</summary>
        private SpriteActorPool enemyHpFillPool;

        /// <summary>Boss 光环对象池</summary>
        private SpriteActorPool bossAuraPool;

        /// <summary>子弹主体对象池</summary>
        private SpriteActorPool bulletPool;

        /// <summary>子弹拖尾对象池（每颗子弹 2 段）</summary>
        private SpriteActorPool bulletTrailPool;

        /// <summary>激光核心对象池</summary>
        private SpriteActorPool laserCorePool;

        /// <summary>激光外发光对象池</summary>
        private SpriteActorPool laserGlowPool;

        /// <summary>瞬时特效对象池</summary>
        private SpriteActorPool fxPool;

        /// <summary>世界空间飘字系统（点阵数字，零字体依赖）</summary>
        private WorldNumbers numbers;

        /// <summary>背景（静态）</summary>
        private SpriteActor backgroundActor;

        /// <summary>城墙（静态，按城门耐久染色）</summary>
        private SpriteActor wallActor;

        /// <summary>城墙受击高光（静态，平时隐藏）</summary>
        private SpriteActor wallFlashActor;

        /// <summary>炮台基座（静态，跟随瞄准方向旋转）</summary>
        private SpriteActor cannonActor;

        /// <summary>瞄准辅助线（静态，按住瞄准时显示）</summary>
        private SpriteActor aimLineActor;

        /// <summary>准星（静态，按住瞄准时显示）</summary>
        private SpriteActor crosshairActor;

        /// <summary>城墙受击高光剩余时间（秒）</summary>
        private float wallFlashTimer;

        /// <summary>上一次播放命中音效的时间</summary>
        private float lastHitSoundTime;

        /// <summary>击杀音效限额所属帧号</summary>
        private int killSoundFrame = -1;

        /// <summary>本帧已播放的击杀音效数量</summary>
        private int killSoundCount;

        /// <summary>火花特效限额所属帧号</summary>
        private int sparkFrame = -1;

        /// <summary>本帧已生成的火花数量</summary>
        private int sparkCount;

        /// <summary>镜头抖动使用的主相机（缓存）</summary>
        private Camera shakeCamera;

        /// <summary>镜头抖动开始前的相机位置</summary>
        private Vector3 shakeBasePosition;

        /// <summary>镜头抖动剩余时间（秒）</summary>
        private float shakeTimer;

        /// <summary>镜头抖动总时长（秒）</summary>
        private float shakeDuration;

        /// <summary>镜头抖动当前强度（世界单位）</summary>
        private float shakeStrength;

        /// <summary>敌人实例 → 敌人视图（按对象引用配对，模拟层对象池复用时自动沿用视图）</summary>
        private readonly Dictionary<Enemy, EnemyView> enemyViews = new Dictionary<Enemy, EnemyView>();

        /// <summary>正在使用的敌人视图列表（用于剔除已消失的敌人）</summary>
        private readonly List<EnemyView> enemyViewList = new List<EnemyView>();

        /// <summary>空闲敌人视图栈</summary>
        private readonly Stack<EnemyView> enemyViewFree = new Stack<EnemyView>();

        /// <summary>子弹实例 → 子弹视图</summary>
        private readonly Dictionary<Bullet, BulletView> bulletViews = new Dictionary<Bullet, BulletView>();

        /// <summary>正在使用的子弹视图列表</summary>
        private readonly List<BulletView> bulletViewList = new List<BulletView>();

        /// <summary>空闲子弹视图栈</summary>
        private readonly Stack<BulletView> bulletViewFree = new Stack<BulletView>();

        /// <summary>激光线段视图（按索引与 Lasers 列表一一对应，数量只增不减）</summary>
        private readonly List<LaserSegment> laserSegments = new List<LaserSegment>();

        /// <summary>正在播放的瞬时特效</summary>
        private readonly List<Fx> fxList = new List<Fx>();

        /// <summary>空闲特效对象栈</summary>
        private readonly Stack<Fx> fxFree = new Stack<Fx>();

        // ============================ 生命周期 ============================

        /// <summary>首次进入时尝试连接运行期实例（GameRuntime 可能尚未创建，Update 中会持续重试）</summary>
        private void Start()
        {
            EnsureBound();
        }

        /// <summary>每帧同步渲染：先保证连接，再按「敌人 → 子弹 → 激光 → 静态元素 → 特效 → 飘字」顺序刷新</summary>
        private void Update()
        {
            EnsureBound();
            if (runtime == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            SyncEnemies();
            SyncBullets();
            SyncLasers();
            SyncStatics(deltaTime);
            UpdateFx(deltaTime);
            UpdateShake(deltaTime);
            numbers.Update(deltaTime);
        }

        /// <summary>禁用时解除事件订阅，避免继续收到事件驱动特效</summary>
        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        /// <summary>销毁时解除订阅并回收全部动态显示单元</summary>
        private void OnDestroy()
        {
            UnsubscribeEvents();
            ClearAllViews();
        }

        // ============================ 连接与订阅 ============================

        /// <summary>连接 GameRuntime（含实例替换）、生成静态元素与对象池、初始化音效并订阅事件</summary>
        private void EnsureBound()
        {
            GameRuntime current = GameRuntime.Instance;
            if (runtime != current)
            {
                UnsubscribeEvents();
                runtime = current;
                ClearAllViews();
            }

            if (runtime == null)
            {
                return;
            }

            if (!staticsBuilt)
            {
                BuildStatics();
            }

            if (!audioInitialized)
            {
                AudioBus.Instance.Initialize();
                audioInitialized = true;
            }

            if (!subscribed)
            {
                SubscribeEvents();
            }
        }

        /// <summary>订阅战斗相关的一次性事件（重复调用安全）</summary>
        private void SubscribeEvents()
        {
            EventBus bus = runtime.Events;
            bus.Subscribe<EnemySpawnedEvent>(HandleEnemySpawned);
            bus.Subscribe<EnemyKilledEvent>(HandleEnemyKilled);
            bus.Subscribe<EnemyDamagedEvent>(HandleEnemyDamaged);
            bus.Subscribe<ShieldBlockedEvent>(HandleShieldBlocked);
            bus.Subscribe<ShieldBrokenEvent>(HandleShieldBroken);
            bus.Subscribe<ExplosionEvent>(HandleExplosion);
            bus.Subscribe<GateDamagedEvent>(HandleGateDamaged);
            bus.Subscribe<WeaponFiredEvent>(HandleWeaponFired);
            bus.Subscribe<ComboChangedEvent>(HandleComboChanged);
            bus.Subscribe<GameStateChangedEvent>(HandleStateChanged);
            subscribed = true;
        }

        /// <summary>解除全部事件订阅（重复调用安全）</summary>
        private void UnsubscribeEvents()
        {
            if (!subscribed || runtime == null)
            {
                subscribed = false;
                return;
            }

            EventBus bus = runtime.Events;
            bus.Unsubscribe<EnemySpawnedEvent>(HandleEnemySpawned);
            bus.Unsubscribe<EnemyKilledEvent>(HandleEnemyKilled);
            bus.Unsubscribe<EnemyDamagedEvent>(HandleEnemyDamaged);
            bus.Unsubscribe<ShieldBlockedEvent>(HandleShieldBlocked);
            bus.Unsubscribe<ShieldBrokenEvent>(HandleShieldBroken);
            bus.Unsubscribe<ExplosionEvent>(HandleExplosion);
            bus.Unsubscribe<GateDamagedEvent>(HandleGateDamaged);
            bus.Unsubscribe<WeaponFiredEvent>(HandleWeaponFired);
            bus.Unsubscribe<ComboChangedEvent>(HandleComboChanged);
            bus.Unsubscribe<GameStateChangedEvent>(HandleStateChanged);
            subscribed = false;
        }

        // ============================ 静态元素与对象池 ============================

        /// <summary>创建子节点根、全部对象池、飘字系统与背景 / 城墙 / 炮台 / 瞄准线等静态元素</summary>
        private void BuildStatics()
        {
            GameObject rootObject = new GameObject("BattleViewRuntime");
            rootObject.transform.SetParent(transform, false);
            root = rootObject.transform;

            enemyBodyPool = new SpriteActorPool(root, "EnemyBody", enemyViewPrewarm);
            enemyShieldPool = new SpriteActorPool(root, "EnemyShield", Mathf.Max(2, enemyViewPrewarm / 4));
            enemyHpBackPool = new SpriteActorPool(root, "EnemyHpBarBack", enemyViewPrewarm);
            enemyHpFillPool = new SpriteActorPool(root, "EnemyHpBarFill", enemyViewPrewarm);
            bossAuraPool = new SpriteActorPool(root, "BossAura", 2);
            bulletPool = new SpriteActorPool(root, "BulletMain", bulletViewPrewarm);
            bulletTrailPool = new SpriteActorPool(root, "BulletTrail", bulletViewPrewarm * 2);
            laserCorePool = new SpriteActorPool(root, "LaserCore", laserPrewarm);
            laserGlowPool = new SpriteActorPool(root, "LaserGlow", laserPrewarm);
            fxPool = new SpriteActorPool(root, "Fx", fxPrewarm);
            numbers = new WorldNumbers(root, "FloatingNumbers", numberPrewarm);

            if (buildStaticArt)
            {
                BuildBackground();
                BuildWall();
                BuildCannon();
            }

            BuildAimGuide();

            staticsBuilt = true;
        }

        /// <summary>生成背景（上亮下暗的战场底色）</summary>
        private void BuildBackground()
        {
            backgroundActor = new SpriteActor(root, "BattleBackground");
            backgroundActor.SetSprite(SpriteFactory.Get("bg"));
            backgroundActor.SetOrder(ViewOrder.Background);
            backgroundActor.SetPosition(Vector2.zero);
            float bgWidth = 0.32f;
            float bgHeight = 0.64f;
            backgroundActor.SetScale(BackgroundWidth / bgWidth, BackgroundHeight / bgHeight);
            backgroundActor.SetColor(Color.white);
        }

        /// <summary>生成城墙（顶边正好落在城门受击线 GateLineY 上）</summary>
        private void BuildWall()
        {
            wallActor = new SpriteActor(root, "GateWall");
            wallActor.SetSprite(SpriteFactory.Get("wall"));
            wallActor.SetOrder(ViewOrder.Wall);
            wallActor.SetPosition(new Vector2(0f, BattleField.GateLineY - 0.85f));
            wallActor.SetScale(BackgroundWidth / 2.56f, 1.7f / 0.64f);
            wallActor.SetColor(new Color(0.62f, 0.66f, 0.76f, 1f));

            wallFlashActor = new SpriteActor(root, "GateWallFlash");
            wallFlashActor.SetSprite(SpriteFactory.Get("glow"));
            wallFlashActor.SetOrder(ViewOrder.WallFlash);
            wallFlashActor.SetPosition(new Vector2(0f, BattleField.GateLineY - 0.5f));
            wallFlashActor.SetScale(9.5f / GlowVisibleDiameter, 1.6f / GlowVisibleDiameter);
            wallFlashActor.SetColor(new Color(1f, 0.35f, 0.25f, 0f));
            wallFlashActor.SetActive(false);
        }

        /// <summary>生成炮台基座（炮管顶端与炮口 BattleField.Muzzle 对齐）</summary>
        private void BuildCannon()
        {
            cannonActor = new SpriteActor(root, "MuzzleCannon");
            cannonActor.SetSprite(SpriteFactory.Get("cannon"));
            cannonActor.SetOrder(ViewOrder.Cannon);
            // cannon 精灵 128×96 px：炮管顶端在中心上方 40 px，按 1.5 倍缩放即 0.6 unit
            cannonActor.SetPosition(BattleField.Muzzle + new Vector2(0f, -0.6f));
            cannonActor.SetScale(1.5f);
            cannonActor.SetColor(new Color(0.85f, 0.87f, 0.95f, 1f));
        }

        /// <summary>生成瞄准辅助线与准星（平时隐藏）</summary>
        private void BuildAimGuide()
        {
            aimLineActor = new SpriteActor(root, "AimLine");
            aimLineActor.SetSprite(SpriteFactory.White);
            aimLineActor.SetOrder(ViewOrder.AimLine);
            aimLineActor.SetActive(false);

            crosshairActor = new SpriteActor(root, "AimCrosshair");
            crosshairActor.SetSprite(SpriteFactory.Get("crosshair"));
            crosshairActor.SetOrder(ViewOrder.Crosshair);
            crosshairActor.SetActive(false);
        }

        // ============================ 敌人同步 ============================

        /// <summary>把当前所有存活敌人同步到视图：新敌人取视图、老敌人刷新、消失的敌人回收</summary>
        private void SyncEnemies()
        {
            IReadOnlyList<Enemy> enemies = runtime.Enemies;
            int frame = Time.frameCount;
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy == null || !enemy.Alive || enemy.Config == null)
                {
                    continue;
                }

                EnemyView view;
                if (!enemyViews.TryGetValue(enemy, out view))
                {
                    view = CreateEnemyView(enemy);
                    enemyViews[enemy] = view;
                }

                view.LastFrame = frame;
                UpdateEnemyView(view, enemy);
            }

            for (int i = enemyViewList.Count - 1; i >= 0; i--)
            {
                EnemyView view = enemyViewList[i];
                if (view.LastFrame == frame)
                {
                    continue;
                }

                ReleaseEnemyView(view);
                enemyViewList.RemoveAt(i);
            }
        }

        /// <summary>从对象池取出一组显示单元并组装成新的敌人视图</summary>
        private EnemyView CreateEnemyView(Enemy enemy)
        {
            EnemyView view = enemyViewFree.Count > 0 ? enemyViewFree.Pop() : new EnemyView();
            view.Entity = enemy;
            view.Body = enemyBodyPool.Rent();
            view.Shield = enemyShieldPool.Rent();
            view.HpBack = enemyHpBackPool.Rent();
            view.HpFill = enemyHpFillPool.Rent();
            view.Aura = null;
            view.LastFrame = -1;
            view.Body.SetOrder(ViewOrder.EnemyBody);
            view.Shield.SetOrder(ViewOrder.EnemyShield);
            view.HpBack.SetOrder(ViewOrder.EnemyHpBar);
            view.HpFill.SetOrder(ViewOrder.EnemyHpBar + 1);
            enemyViewList.Add(view);
            return view;
        }

        /// <summary>回收敌人视图的全部显示单元</summary>
        private void ReleaseEnemyView(EnemyView view)
        {
            if (view.Entity != null)
            {
                enemyViews.Remove(view.Entity);
            }

            enemyBodyPool.Return(view.Body);
            enemyShieldPool.Return(view.Shield);
            enemyHpBackPool.Return(view.HpBack);
            enemyHpFillPool.Return(view.HpFill);
            if (view.Aura != null)
            {
                bossAuraPool.Return(view.Aura);
                view.Aura = null;
            }

            view.Entity = null;
            enemyViewFree.Push(view);
        }

        /// <summary>按敌人当前状态刷新身体、护盾、血条与 Boss 光环</summary>
        private void UpdateEnemyView(EnemyView view, Enemy enemy)
        {
            EnemyKind kind = enemy.Kind;
            Vector2 position = enemy.Position;
            float diameter = Mathf.Max(0.1f, enemy.Radius * 2f * enemySizeMultiplier);
            float flash = Mathf.Clamp01(enemy.HitFlash / HitFlashDuration);

            // 身体：不同兵种用不同图形与配色，受击时向白色插值
            Sprite bodySprite = EnemyBodySprite(kind);
            view.Body.SetSprite(bodySprite);
            view.Body.SetPosition(position);
            view.Body.SetScale(diameter / EnemyVisibleExtent(kind));
            // 飞行兵三角形尖端朝下（朝城门），重甲兵胶囊横放（显得更敦实）
            float bodyRotation = 0f;
            if (kind == EnemyKind.Flyer)
            {
                bodyRotation = 180f;
            }
            else if (kind == EnemyKind.Heavy)
            {
                bodyRotation = 90f;
            }

            view.Body.SetRotation(bodyRotation);
            Color bodyColor = ViewPalette.EnemyBody(kind);
            if (kind == EnemyKind.Boss && enemy.BossPhase > 1)
            {
                // Boss 进入后续阶段时逐渐升温（暗红 → 亮橙红），给玩家阶段变化的视觉提示
                bodyColor = Color.Lerp(bodyColor, new Color(1f, 0.42f, 0.20f, 1f), Mathf.Clamp01((enemy.BossPhase - 1) * 0.28f));
            }

            view.Body.SetColor(Color.Lerp(bodyColor, Color.white, flash * 0.85f));

            // 护盾：按 ShieldAngle 旋转（精灵正面朝 +Y，因此角度 - 90 度）
            if (enemy.HasShield)
            {
                view.Shield.SetActive(true);
                view.Shield.SetSprite(SpriteFactory.Get("shield"));
                view.Shield.SetPosition(position);
                view.Shield.SetRotation(enemy.ShieldAngle - 90f);
                view.Shield.SetScale(diameter * 1.04f / ShieldVisibleWidth);
                Color shieldColor = enemy.IsShieldFacing(BattleField.Muzzle) ? ViewPalette.ShieldFacing : ViewPalette.ShieldIdle;
                view.Shield.SetColor(Color.Lerp(shieldColor, Color.white, flash * 0.5f));
            }
            else if (view.Shield.IsActive)
            {
                view.Shield.SetActive(false);
            }

            // 血条：底 + 填充，填充按 Hp / MaxHp 缩放并从左侧对齐
            float hpRatio = enemy.MaxHp > 0.001f ? Mathf.Clamp01(enemy.Hp / enemy.MaxHp) : 1f;
            bool showBar = alwaysShowHpBar || hpRatio < 0.999f || enemy.IsBoss;
            if (showBar)
            {
                float barWidth = diameter * hpBarWidthRatio;
                float barY = position.y + enemy.Radius * enemySizeMultiplier + 0.18f;
                float innerWidth = Mathf.Max(0.02f, barWidth - 0.02f);
                float fillWidth = innerWidth * hpRatio;

                view.HpBack.SetActive(true);
                view.HpBack.SetSprite(SpriteFactory.White);
                view.HpBack.SetPosition(new Vector2(position.x, barY));
                view.HpBack.SetScale(barWidth / WhiteSpriteSize, HpBarHeight / WhiteSpriteSize);
                view.HpBack.SetColor(ViewPalette.HpBarBack);

                view.HpFill.SetActive(true);
                view.HpFill.SetSprite(SpriteFactory.White);
                view.HpFill.SetScale(Mathf.Max(0.001f, fillWidth) / WhiteSpriteSize, (HpBarHeight - 0.02f) / WhiteSpriteSize);
                view.HpFill.SetPosition(new Vector2(position.x - innerWidth * 0.5f + fillWidth * 0.5f, barY));
                view.HpFill.SetColor(Color.Lerp(ViewPalette.HpBarLow, ViewPalette.HpBarFill, Mathf.Clamp01(hpRatio * 1.6f)));
            }
            else
            {
                if (view.HpBack.IsActive)
                {
                    view.HpBack.SetActive(false);
                }

                if (view.HpFill.IsActive)
                {
                    view.HpFill.SetActive(false);
                }
            }

            // Boss 光环：脉动的暗红光晕，强化 Boss 的体量感（Boss 大血条由 BattleHud 负责）
            if (enemy.IsBoss)
            {
                if (view.Aura == null)
                {
                    view.Aura = bossAuraPool.Rent();
                    view.Aura.SetOrder(ViewOrder.BossAura);
                }

                float pulse = 0.75f + Mathf.Sin(Time.time * 3.4f) * 0.14f;
                view.Aura.SetActive(true);
                view.Aura.SetSprite(SpriteFactory.Get("glow"));
                view.Aura.SetPosition(position);
                view.Aura.SetScale(diameter * 1.35f / GlowVisibleDiameter);
                Color auraColor = ViewPalette.Boss;
                auraColor.a = 0.22f * pulse;
                view.Aura.SetColor(auraColor);
            }
            else if (view.Aura != null)
            {
                bossAuraPool.Return(view.Aura);
                view.Aura = null;
            }
        }

        /// <summary>按兵种取身体精灵（图形差异化，颜色在 ViewPalette 中统一）</summary>
        private static Sprite EnemyBodySprite(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Runner:
                    return SpriteFactory.Get("diamond");
                case EnemyKind.Shield:
                    return SpriteFactory.Get("rounded");
                case EnemyKind.Flyer:
                    return SpriteFactory.Get("triangle");
                case EnemyKind.Heavy:
                    return SpriteFactory.Get("capsule");
                case EnemyKind.Boss:
                    return SpriteFactory.Get("skull");
                default:
                    return SpriteFactory.Get("circle");
            }
        }

        /// <summary>各兵种精灵在缩放 1 时的可见尺寸（单位），用于把碰撞直径换算成缩放倍率</summary>
        private static float EnemyVisibleExtent(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Runner:
                    return 0.54f;
                case EnemyKind.Shield:
                    return 0.56f;
                case EnemyKind.Flyer:
                    return 0.54f;
                case EnemyKind.Heavy:
                    return 0.56f;
                case EnemyKind.Boss:
                    return 0.50f;
                default:
                    return CircleVisibleDiameter;
            }
        }

        // ============================ 子弹同步 ============================

        /// <summary>把当前所有子弹同步到视图（直射弹带 2 段拖尾、手雷按 VisualArcHeight 抬升）</summary>
        private void SyncBullets()
        {
            IReadOnlyList<Bullet> bullets = runtime.Bullets;
            int frame = Time.frameCount;
            for (int i = 0; i < bullets.Count; i++)
            {
                Bullet bullet = bullets[i];
                if (bullet == null || !bullet.Alive)
                {
                    continue;
                }

                BulletView view;
                if (!bulletViews.TryGetValue(bullet, out view))
                {
                    view = CreateBulletView(bullet);
                    bulletViews[bullet] = view;
                }

                view.LastFrame = frame;
                UpdateBulletView(view, bullet);
            }

            for (int i = bulletViewList.Count - 1; i >= 0; i--)
            {
                BulletView view = bulletViewList[i];
                if (view.LastFrame == frame)
                {
                    continue;
                }

                ReleaseBulletView(view);
                bulletViewList.RemoveAt(i);
            }
        }

        /// <summary>从对象池取出子弹主体与两段拖尾</summary>
        private BulletView CreateBulletView(Bullet bullet)
        {
            BulletView view = bulletViewFree.Count > 0 ? bulletViewFree.Pop() : new BulletView();
            view.Entity = bullet;
            view.Main = bulletPool.Rent();
            view.TrailA = bulletTrailPool.Rent();
            view.TrailB = bulletTrailPool.Rent();
            view.LastFrame = -1;
            view.Main.SetOrder(ViewOrder.Bullet);
            view.TrailA.SetOrder(ViewOrder.Bullet - 1);
            view.TrailB.SetOrder(ViewOrder.Bullet - 2);
            bulletViewList.Add(view);
            return view;
        }

        /// <summary>回收子弹视图的全部显示单元</summary>
        private void ReleaseBulletView(BulletView view)
        {
            if (view.Entity != null)
            {
                bulletViews.Remove(view.Entity);
            }

            bulletPool.Return(view.Main);
            bulletTrailPool.Return(view.TrailA);
            bulletTrailPool.Return(view.TrailB);
            view.Entity = null;
            bulletViewFree.Push(view);
        }

        /// <summary>刷新单颗子弹的显示：直射弹用圆点 / 箭头 + 两段拖尾，手雷用抛物线精灵 + 地面影子</summary>
        private void UpdateBulletView(BulletView view, Bullet bullet)
        {
            Vector2 position = bullet.Position;
            if (bullet.Flight == BulletFlight.Arc)
            {
                // 手雷：模拟层的 Position 是地面投影点，视觉上叠加抛射高度
                Vector2 airborne = position + new Vector2(0f, bullet.VisualArcHeight);
                view.Main.SetSprite(SpriteFactory.Get("grenade"));
                view.Main.SetPosition(airborne);
                view.Main.SetScale(0.5f);
                view.Main.SetRotation(Time.time * 540f);
                view.Main.SetColor(Color.white);

                view.TrailA.SetActive(true);
                view.TrailA.SetSprite(SpriteFactory.Get("dot"));
                view.TrailA.SetPosition(position);
                view.TrailA.SetScale(0.55f, 0.28f);
                view.TrailA.SetColor(new Color(0f, 0f, 0f, 0.3f));

                if (view.TrailB.IsActive)
                {
                    view.TrailB.SetActive(false);
                }

                return;
            }

            Color color = ViewPalette.Bullet(bullet.Type);
            Vector2 direction = bullet.Velocity.sqrMagnitude > 0.0001f ? bullet.Velocity.normalized : Vector2.up;

            if (bullet.Type == DamageType.Piercing)
            {
                // 贯穿弹：细长尖头弹丸，朝速度方向
                view.Main.SetSprite(SpriteFactory.Get("bullet"));
                view.Main.SetPosition(position);
                view.Main.SetScale(0.85f);
                view.Main.SetRotation(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
                view.Main.SetColor(color);
            }
            else
            {
                // 普通弹：实心圆点（尺寸跟随碰撞半径）
                view.Main.SetSprite(SpriteFactory.Get("circle"));
                view.Main.SetPosition(position);
                view.Main.SetScale(Mathf.Max(0.08f, bullet.Radius * 2f) / CircleVisibleDiameter);
                view.Main.SetRotation(0f);
                view.Main.SetColor(color);
            }

            // 两段拖尾：越靠后越小越淡
            UpdateTrail(view.TrailA, position - direction * 0.16f, 0.5f, color, 0.5f);
            UpdateTrail(view.TrailB, position - direction * 0.34f, 0.34f, color, 0.24f);
        }

        /// <summary>刷新一段拖尾圆点</summary>
        private static void UpdateTrail(SpriteActor trail, Vector2 position, float scale, Color color, float alpha)
        {
            trail.SetActive(true);
            trail.SetSprite(SpriteFactory.Get("dot"));
            trail.SetPosition(position);
            trail.SetScale(scale);
            Color trailColor = color;
            trailColor.a = alpha;
            trail.SetColor(trailColor);
        }

        // ============================ 激光同步 ============================

        /// <summary>按 WeaponSystem.Lasers 绘制激光束：核心（白方块拉伸）+ 外发光（柔光拉伸）</summary>
        private void SyncLasers()
        {
            IReadOnlyList<LaserShot> lasers = runtime.Lasers;
            int count = lasers.Count;
            for (int i = 0; i < count; i++)
            {
                if (i >= laserSegments.Count)
                {
                    laserSegments.Add(CreateLaserSegment());
                }

                LaserSegment segment = laserSegments[i];
                LaserShot shot = lasers[i];
                Vector2 delta = shot.To - shot.From;
                float length = delta.magnitude;
                if (length < 0.01f)
                {
                    segment.Core.SetActive(false);
                    segment.Glow.SetActive(false);
                    continue;
                }

                Vector2 center = shot.From + delta * 0.5f;
                float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                float width = Mathf.Max(0.04f, shot.Width);
                float energy = Mathf.Clamp01(shot.Energy01);
                float energyAlpha = Mathf.Lerp(0.55f, 1f, energy);

                segment.Core.SetActive(true);
                segment.Core.SetSprite(SpriteFactory.White);
                segment.Core.SetPosition(center);
                segment.Core.SetRotation(angle);
                segment.Core.SetScale(length / WhiteSpriteSize, width / WhiteSpriteSize);
                Color coreColor = ViewPalette.Laser;
                coreColor.a = energyAlpha;
                segment.Core.SetColor(coreColor);

                segment.Glow.SetActive(true);
                segment.Glow.SetSprite(SpriteFactory.Get("glow"));
                segment.Glow.SetPosition(center);
                segment.Glow.SetRotation(angle);
                segment.Glow.SetScale(length / GlowVisibleDiameter, width * 3.4f / GlowVisibleDiameter);
                Color glowColor = ViewPalette.LaserGlow;
                glowColor.a *= energyAlpha;
                segment.Glow.SetColor(glowColor);
            }

            for (int i = count; i < laserSegments.Count; i++)
            {
                LaserSegment segment = laserSegments[i];
                if (segment.Core.IsActive)
                {
                    segment.Core.SetActive(false);
                }

                if (segment.Glow.IsActive)
                {
                    segment.Glow.SetActive(false);
                }
            }
        }

        /// <summary>创建一段激光视图（核心 + 外发光）</summary>
        private LaserSegment CreateLaserSegment()
        {
            LaserSegment segment = new LaserSegment();
            segment.Core = laserCorePool.Rent();
            segment.Glow = laserGlowPool.Rent();
            segment.Core.SetOrder(ViewOrder.LaserCore);
            segment.Glow.SetOrder(ViewOrder.LaserGlow);
            segment.Core.SetActive(false);
            segment.Glow.SetActive(false);
            return segment;
        }

        // ============================ 静态元素同步（炮台 / 瞄准 / 城墙） ============================

        /// <summary>刷新炮台朝向、瞄准辅助线与城墙染色</summary>
        private void SyncStatics(float deltaTime)
        {
            UpdateCannon();
            UpdateAimGuide();
            UpdateWall(deltaTime);
        }

        /// <summary>炮台随瞄准方向旋转（按住瞄准时才更新，松手保持上次朝向）</summary>
        private void UpdateCannon()
        {
            if (cannonActor == null || !runtime.IsAiming)
            {
                return;
            }

            Vector2 direction = runtime.AimWorldPos - BattleField.Muzzle;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            // cannon 精灵炮管朝 +Y，因此旋转角 = 方向角 - 90 度
            cannonActor.SetRotation(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
        }

        /// <summary>按住瞄准时显示「炮口 → 瞄准点」的淡色辅助线与脉动准星</summary>
        private void UpdateAimGuide()
        {
            if (aimLineActor == null || crosshairActor == null)
            {
                return;
            }

            if (!runtime.IsAiming || !showAimLine)
            {
                if (aimLineActor.IsActive)
                {
                    aimLineActor.SetActive(false);
                }

                if (crosshairActor.IsActive)
                {
                    crosshairActor.SetActive(false);
                }

                return;
            }

            Vector2 muzzle = BattleField.Muzzle;
            Vector2 target = runtime.AimWorldPos;
            Vector2 delta = target - muzzle;
            if (delta.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector2 direction = delta.normalized;
            float length = Mathf.Min(delta.magnitude, aimLineMaxLength);
            aimLineActor.SetActive(true);
            aimLineActor.SetSprite(SpriteFactory.White);
            aimLineActor.SetPosition(muzzle + direction * (length * 0.5f));
            aimLineActor.SetRotation(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            aimLineActor.SetScale(length / WhiteSpriteSize, 0.05f / WhiteSpriteSize);
            Color lineColor = ViewPalette.Laser;
            lineColor.a = 0.16f;
            aimLineActor.SetColor(lineColor);

            crosshairActor.SetActive(true);
            crosshairActor.SetSprite(SpriteFactory.Get("crosshair"));
            crosshairActor.SetPosition(target);
            crosshairActor.SetRotation(Time.time * 45f);
            float pulse = 0.95f + Mathf.Sin(Time.time * 8f) * 0.06f;
            crosshairActor.SetScale(crosshairScale * pulse);
            crosshairActor.SetColor(new Color(1f, 1f, 1f, 0.85f));
        }

        /// <summary>按城门耐久给城墙染色，并推进受击高光的淡出</summary>
        private void UpdateWall(float deltaTime)
        {
            if (wallActor == null)
            {
                return;
            }

            float hpRatio = 1f;
            if (runtime.Run != null && runtime.Run.gateMaxHp > 0.001f)
            {
                hpRatio = Mathf.Clamp01(runtime.Run.gateHp / runtime.Run.gateMaxHp);
            }

            Color stone = new Color(0.62f, 0.66f, 0.76f, 1f);
            Color danger = new Color(0.95f, 0.38f, 0.30f, 1f);
            float lowHp = 1f - hpRatio;
            wallActor.SetColor(Color.Lerp(stone, danger, lowHp * lowHp));

            if (wallFlashActor == null)
            {
                return;
            }

            if (wallFlashTimer > 0f)
            {
                wallFlashTimer = Mathf.Max(0f, wallFlashTimer - deltaTime);
                float t = wallFlashTimer / WallFlashDuration;
                wallFlashActor.SetActive(true);
                wallFlashActor.SetColor(new Color(1f, 0.35f, 0.25f, 0.55f * t));
            }
            else if (wallFlashActor.IsActive)
            {
                wallFlashActor.SetActive(false);
            }
        }

        // ============================ 瞬时特效 ============================

        /// <summary>每帧推进特效的位置、缩放与淡出</summary>
        private void UpdateFx(float deltaTime)
        {
            for (int i = fxList.Count - 1; i >= 0; i--)
            {
                Fx fx = fxList[i];
                fx.Elapsed += deltaTime;
                float t = Mathf.Clamp01(fx.Elapsed / fx.Life);
                if (t >= 1f)
                {
                    fxPool.Return(fx.Actor);
                    fx.Actor = null;
                    fxList.RemoveAt(i);
                    fxFree.Push(fx);
                    continue;
                }

                fx.Position += fx.Velocity * deltaTime;
                fx.Actor.SetPosition(fx.Position);
                fx.Actor.SetScale(Mathf.Max(0.01f, Mathf.Lerp(fx.StartScale, fx.EndScale, t)));
                Color color = Color.Lerp(fx.StartColor, fx.EndColor, t);
                color.a *= 1f - t;
                fx.Actor.SetColor(color);
                if (fx.RotationSpeed != 0f)
                {
                    fx.Actor.SetRotation(fx.StartRotation + fx.Elapsed * fx.RotationSpeed);
                }
            }
        }

        /// <summary>生成一个瞬时特效并返回其运行期数据（可继续设置速度 / 自旋）</summary>
        private Fx SpawnFx(string spriteKey, Vector2 position, Color startColor, Color endColor, float startScale, float endScale, float life, int sortingOrder)
        {
            if (!enableFx)
            {
                return null;
            }

            Fx fx = fxFree.Count > 0 ? fxFree.Pop() : new Fx();
            fx.Actor = fxPool.Rent();
            fx.Actor.SetSprite(SpriteFactory.Get(spriteKey));
            fx.Actor.SetPosition(position);
            fx.Actor.SetScale(Mathf.Max(0.01f, startScale));
            fx.Actor.SetColor(startColor);
            fx.Actor.SetOrder(sortingOrder);
            fx.Actor.SetRotation(0f);
            fx.Position = position;
            fx.Velocity = Vector2.zero;
            fx.Elapsed = 0f;
            fx.Life = Mathf.Max(0.02f, life);
            fx.StartScale = startScale;
            fx.EndScale = endScale;
            fx.StartColor = startColor;
            fx.EndColor = endColor;
            fx.StartRotation = 0f;
            fx.RotationSpeed = 0f;
            fxList.Add(fx);
            return fx;
        }

        /// <summary>尝试占用本帧的火花特效名额（性能护栏）</summary>
        private bool TryReserveSpark()
        {
            int frame = Time.frameCount;
            if (frame != sparkFrame)
            {
                sparkFrame = frame;
                sparkCount = 0;
            }

            if (sparkCount >= maxSparksPerFrame)
            {
                return false;
            }

            sparkCount++;
            return true;
        }

        /// <summary>尝试占用本帧的击杀音效名额</summary>
        private bool TryReserveKillSound()
        {
            int frame = Time.frameCount;
            if (frame != killSoundFrame)
            {
                killSoundFrame = frame;
                killSoundCount = 0;
            }

            if (killSoundCount >= maxKillSoundsPerFrame)
            {
                return false;
            }

            killSoundCount++;
            return true;
        }

        // ============================ 飘字 ============================

        /// <summary>生成一条世界空间飘字（受 Tuning.combat.maxFloatingTexts 每帧上限约束）</summary>
        private void SpawnNumber(Vector2 position, string text, Color color, float scale, float life, float riseSpeed)
        {
            int maxPerFrame = 40;
            if (runtime != null && runtime.Config != null && runtime.Config.Tuning != null && runtime.Config.Tuning.combat != null)
            {
                maxPerFrame = runtime.Config.Tuning.combat.maxFloatingTexts;
            }

            if (!numbers.TryReserveSlot(maxPerFrame))
            {
                return;
            }

            numbers.Spawn(position, text, color, scale, life, riseSpeed);
        }

        // ============================ 音效 ============================

        /// <summary>播放一条音效（内部统一乘整体音量倍率）</summary>
        private void PlaySound(string key, float volume, float pitch)
        {
            if (!enableAudio || string.IsNullOrEmpty(key))
            {
                return;
            }

            AudioBus.Instance.Play(key, volume * audioVolume, pitch);
        }

        // ============================ 事件处理 ============================

        /// <summary>敌人出生：生成一个扩散环做入场提示，Boss 出生附带轻微镜头震动</summary>
        private void HandleEnemySpawned(EnemySpawnedEvent evt)
        {
            Color color = ViewPalette.EnemyBody(evt.Kind);
            color.a = 0.85f;
            Fx ring = SpawnFx("ring", evt.Pos, color, new Color(color.r, color.g, color.b, 0f), 0.15f, evt.IsBoss ? 2.4f : 0.7f, 0.35f, ViewOrder.Fx);
            if (ring != null)
            {
                ring.RotationSpeed = 90f;
            }

            if (evt.IsBoss)
            {
                TriggerShake(0.12f, 0.3f);
            }
        }

        /// <summary>敌人被击杀：爆散碎片 + 金币飘字 + 击杀音效（每帧限流）</summary>
        private void HandleEnemyKilled(EnemyKilledEvent evt)
        {
            Color bodyColor = ViewPalette.EnemyBody(evt.Kind);
            bool boss = evt.Kind == EnemyKind.Boss;
            Color burstColor = bodyColor;
            burstColor.a = 0.9f;
            SpawnFx("ring", evt.Pos, burstColor, new Color(burstColor.r, burstColor.g, burstColor.b, 0f), boss ? 0.4f : 0.2f, boss ? 2.8f : 0.95f, boss ? 0.5f : 0.26f, ViewOrder.Fx);

            int pieces = boss ? 8 : 3;
            for (int i = 0; i < pieces; i++)
            {
                Color pieceColor = bodyColor;
                pieceColor.a = 1f;
                Fx piece = SpawnFx("dot", evt.Pos, pieceColor, new Color(bodyColor.r, bodyColor.g, bodyColor.b, 0f), boss ? 0.5f : 0.3f, 0.03f, 0.32f + Random.Range(0f, 0.18f), ViewOrder.Fx);
                if (piece != null)
                {
                    piece.Velocity = Random.insideUnitCircle * (boss ? 4.2f : 2.4f);
                }
            }

            SpawnNumber(evt.Pos + new Vector2(0f, 0.55f), "+" + evt.Gold, ViewPalette.GoldText, 0.85f, 0.7f, 1.8f);

            if (boss)
            {
                TriggerShake(0.22f, 0.45f);
            }

            if (TryReserveKillSound())
            {
                PlaySound("kill", 0.45f, Random.Range(0.92f, 1.16f));
            }
        }

        /// <summary>敌人受伤：伤害飘字（暴击更大更金）+ 命中火花 + 命中音效（节流）</summary>
        private void HandleEnemyDamaged(EnemyDamagedEvent evt)
        {
            int damage = Mathf.Max(1, Mathf.RoundToInt(evt.Damage));
            Color numberColor = evt.Crit ? ViewPalette.CritText : ViewPalette.DamageText;
            Vector2 offset = new Vector2(Random.Range(-0.18f, 0.18f), 0.35f);
            SpawnNumber(evt.Pos + offset, damage.ToString(), numberColor, evt.Crit ? 1.05f : 0.8f, evt.Crit ? 0.75f : 0.55f, 2.1f);

            if (TryReserveSpark())
            {
                Color sparkColor = evt.Crit ? ViewPalette.CritText : Color.white;
                Fx spark = SpawnFx("star", evt.Pos, sparkColor, new Color(sparkColor.r, sparkColor.g, sparkColor.b, 0f), evt.Crit ? 0.38f : 0.24f, 0.02f, 0.15f, ViewOrder.Fx);
                if (spark != null)
                {
                    spark.Velocity = Random.insideUnitCircle * 0.8f;
                    spark.RotationSpeed = Random.Range(-240f, 240f);
                }
            }

            if (Time.time >= lastHitSoundTime)
            {
                lastHitSoundTime = Time.time + HitSoundInterval;
                PlaySound("hit", evt.Crit ? 0.35f : 0.2f, Random.Range(0.9f, 1.28f));
            }
        }

        /// <summary>护盾挡下普通弹：盾蓝色「0」飘字 + 小冲击环 + 挡格音效</summary>
        private void HandleShieldBlocked(ShieldBlockedEvent evt)
        {
            SpawnNumber(evt.Pos + new Vector2(0f, 0.3f), "0", ViewPalette.BlockText, 0.75f, 0.5f, 1.8f);
            Fx ring = SpawnFx("ring", evt.Pos, ViewPalette.ShieldFacing, new Color(0.6f, 0.85f, 1f, 0f), 0.2f, 0.55f, 0.22f, ViewOrder.Fx);
            if (ring != null)
            {
                ring.RotationSpeed = -120f;
            }

            PlaySound("shield_block", 0.5f, Random.Range(0.95f, 1.12f));
        }

        /// <summary>护盾被爆炸 / 贯穿克制：洋红「!」飘字 + 星形迸裂 + 破盾音效</summary>
        private void HandleShieldBroken(ShieldBrokenEvent evt)
        {
            SpawnNumber(evt.Pos + new Vector2(0f, 0.34f), "!", ViewPalette.BreakText, 1.05f, 0.6f, 2f);
            Color breakColor = ViewPalette.BreakText;
            breakColor.a = 1f;
            Fx star = SpawnFx("star", evt.Pos, breakColor, new Color(breakColor.r, breakColor.g, breakColor.b, 0f), 0.3f, 0.85f, 0.24f, ViewOrder.Fx);
            if (star != null)
            {
                star.RotationSpeed = 320f;
            }

            Fx ring = SpawnFx("ring", evt.Pos, breakColor, new Color(breakColor.r, breakColor.g, breakColor.b, 0f), 0.18f, 1.1f, 0.3f, ViewOrder.Fx);
            if (ring != null)
            {
                ring.RotationSpeed = -200f;
            }

            PlaySound("shield_break", 0.6f, 1f);
        }

        /// <summary>爆炸：内核光晕 + 扩散冲击环 + 飞散火花 + 镜头轻震 + 爆炸音效</summary>
        private void HandleExplosion(ExplosionEvent evt)
        {
            float radius = evt.Radius > 0.01f ? evt.Radius : 1f;
            Fx glow = SpawnFx("glow", evt.Pos, ViewPalette.ExplosionCore, ViewPalette.Explosion, radius * 0.6f / GlowVisibleDiameter, radius * 2.2f / GlowVisibleDiameter, 0.35f, ViewOrder.Fx);
            if (glow != null)
            {
                glow.RotationSpeed = 0f;
            }

            SpawnFx("ring", evt.Pos, ViewPalette.Explosion, new Color(ViewPalette.Explosion.r, ViewPalette.Explosion.g, ViewPalette.Explosion.b, 0f), radius * 0.6f / RingVisibleDiameter, radius * 2.2f / RingVisibleDiameter, 0.3f, ViewOrder.Fx);

            for (int i = 0; i < 4; i++)
            {
                Color sparkColor = ViewPalette.ExplosionCore;
                Fx spark = SpawnFx("dot", evt.Pos, sparkColor, new Color(sparkColor.r, sparkColor.g, sparkColor.b, 0f), 0.4f, 0.03f, 0.4f, ViewOrder.Fx);
                if (spark != null)
                {
                    spark.Velocity = Random.insideUnitCircle * (radius * 2.6f);
                }
            }

            TriggerShake(0.06f, 0.14f);
            PlaySound("explosion", 0.8f, Random.Range(0.92f, 1.06f));
        }

        /// <summary>城门受击：城墙红色高光 + 伤害飘字 + 镜头震动 + 受击音效</summary>
        private void HandleGateDamaged(GateDamagedEvent evt)
        {
            wallFlashTimer = WallFlashDuration;
            SpawnNumber(new Vector2(0f, BattleField.GateLineY + 0.7f), "-" + Mathf.RoundToInt(evt.Damage), new Color(1f, 0.42f, 0.36f, 1f), 1.05f, 0.8f, 2.2f);
            TriggerShake(gateShakeStrength, gateShakeDuration);
            PlaySound("gate_hit", 0.7f, Random.Range(0.9f, 1.06f));
        }

        /// <summary>武器发射：枪口火光 + 合成枪声（同帧数量由 AudioBus 内部限流）</summary>
        private void HandleWeaponFired(WeaponFiredEvent evt)
        {
            if (enableFx)
            {
                SpawnFx("glow", BattleField.Muzzle, ViewPalette.MuzzleFlash, new Color(ViewPalette.MuzzleFlash.r, ViewPalette.MuzzleFlash.g, ViewPalette.MuzzleFlash.b, 0f), 0.32f, 0.85f, 0.09f, ViewOrder.Fx);
            }

            if (enableAudio)
            {
                AudioBus.Instance.PlayWeaponFire(evt.WeaponId, 0.5f * audioVolume);
            }
        }

        /// <summary>连杀等级提升：播放上扬的提示音（等级越高音调越高）</summary>
        private void HandleComboChanged(ComboChangedEvent evt)
        {
            if (evt.Combo <= 0 || evt.FeedbackLevel <= 0)
            {
                return;
            }

            PlaySound("combo_up", 0.28f, 1f + evt.FeedbackLevel * 0.07f);
        }

        /// <summary>游戏状态切换：进入结算 / 失败时播放对应音效，离开战斗时隐藏瞄准元素</summary>
        private void HandleStateChanged(GameStateChangedEvent evt)
        {
            if (evt.Current == GameState.Result)
            {
                PlaySound("level_clear", 0.65f, 1f);
            }
            else if (evt.Current == GameState.GameOver)
            {
                PlaySound("game_over", 0.65f, 1f);
            }

            if (evt.Current != GameState.Battle)
            {
                if (aimLineActor != null && aimLineActor.IsActive)
                {
                    aimLineActor.SetActive(false);
                }

                if (crosshairActor != null && crosshairActor.IsActive)
                {
                    crosshairActor.SetActive(false);
                }
            }
        }

        // ============================ 镜头抖动 ============================

        /// <summary>触发（或叠加）一次镜头抖动；仅在主相机存在且开启时生效</summary>
        private void TriggerShake(float strength, float duration)
        {
            if (!enableCameraShake)
            {
                return;
            }

            if (shakeCamera == null)
            {
                shakeCamera = Camera.main;
            }

            if (shakeCamera == null)
            {
                return;
            }

            if (shakeTimer <= 0f)
            {
                shakeBasePosition = shakeCamera.transform.position;
            }

            shakeStrength = Mathf.Max(shakeStrength, strength);
            shakeDuration = Mathf.Max(shakeDuration, duration);
            shakeTimer = Mathf.Max(shakeTimer, duration);
        }

        /// <summary>推进镜头抖动并在结束时复位到抖动前的位置</summary>
        private void UpdateShake(float deltaTime)
        {
            if (shakeCamera == null || shakeTimer <= 0f)
            {
                return;
            }

            shakeTimer = Mathf.Max(0f, shakeTimer - deltaTime);
            if (shakeTimer <= 0f)
            {
                shakeCamera.transform.position = shakeBasePosition;
                shakeStrength = 0f;
                shakeDuration = 0f;
                return;
            }

            float t = shakeDuration > 0.0001f ? shakeTimer / shakeDuration : 0f;
            Vector2 offset = Random.insideUnitCircle * (shakeStrength * t);
            Vector3 position = shakeBasePosition;
            position.x += offset.x;
            position.y += offset.y;
            shakeCamera.transform.position = position;
        }

        // ============================ 清理 ============================

        /// <summary>回收全部动态显示单元（切换运行期实例 / 销毁时调用），避免残留旧画面</summary>
        private void ClearAllViews()
        {
            if (!staticsBuilt)
            {
                return;
            }

            for (int i = 0; i < enemyViewList.Count; i++)
            {
                EnemyView view = enemyViewList[i];
                enemyBodyPool.Return(view.Body);
                enemyShieldPool.Return(view.Shield);
                enemyHpBackPool.Return(view.HpBack);
                enemyHpFillPool.Return(view.HpFill);
                if (view.Aura != null)
                {
                    bossAuraPool.Return(view.Aura);
                    view.Aura = null;
                }

                view.Entity = null;
                enemyViewFree.Push(view);
            }

            enemyViewList.Clear();
            enemyViews.Clear();

            for (int i = 0; i < bulletViewList.Count; i++)
            {
                BulletView view = bulletViewList[i];
                bulletPool.Return(view.Main);
                bulletTrailPool.Return(view.TrailA);
                bulletTrailPool.Return(view.TrailB);
                view.Entity = null;
                bulletViewFree.Push(view);
            }

            bulletViewList.Clear();
            bulletViews.Clear();

            for (int i = 0; i < laserSegments.Count; i++)
            {
                laserSegments[i].Core.SetActive(false);
                laserSegments[i].Glow.SetActive(false);
            }

            for (int i = fxList.Count - 1; i >= 0; i--)
            {
                Fx fx = fxList[i];
                fxPool.Return(fx.Actor);
                fx.Actor = null;
                fxFree.Push(fx);
            }

            fxList.Clear();

            if (numbers != null)
            {
                numbers.Clear();
            }

            if (aimLineActor != null)
            {
                aimLineActor.SetActive(false);
            }

            if (crosshairActor != null)
            {
                crosshairActor.SetActive(false);
            }

            if (wallFlashActor != null)
            {
                wallFlashActor.SetActive(false);
            }

            wallFlashTimer = 0f;
        }

        // ============================ 视图数据 ============================

        /// <summary>单个敌人的视图：身体 / 护盾 / 血条底 / 血条填充 / Boss 光环（均为池化显示单元）</summary>
        private sealed class EnemyView
        {
            /// <summary>绑定的模拟层敌人（对象池复用时随实际敌人替换）</summary>
            public Enemy Entity;

            /// <summary>身体</summary>
            public SpriteActor Body;

            /// <summary>护盾（仅盾兵显示）</summary>
            public SpriteActor Shield;

            /// <summary>血条底</summary>
            public SpriteActor HpBack;

            /// <summary>血条填充</summary>
            public SpriteActor HpFill;

            /// <summary>Boss 光环（仅 Boss 使用，懒创建）</summary>
            public SpriteActor Aura;

            /// <summary>最近一次同步的帧号（用于剔除已消失的敌人）</summary>
            public int LastFrame;
        }

        /// <summary>单颗子弹的视图：主体 + 两段拖尾（手雷时拖尾 A 兼作地面影子）</summary>
        private sealed class BulletView
        {
            /// <summary>绑定的模拟层子弹</summary>
            public Bullet Entity;

            /// <summary>子弹主体</summary>
            public SpriteActor Main;

            /// <summary>第一段拖尾</summary>
            public SpriteActor TrailA;

            /// <summary>第二段拖尾</summary>
            public SpriteActor TrailB;

            /// <summary>最近一次同步的帧号</summary>
            public int LastFrame;
        }

        /// <summary>一段激光的视图：核心 + 外发光</summary>
        private sealed class LaserSegment
        {
            /// <summary>激光核心（白色方块拉伸）</summary>
            public SpriteActor Core;

            /// <summary>激光外发光（柔光拉伸）</summary>
            public SpriteActor Glow;
        }

        /// <summary>一个瞬时特效的运行时数据</summary>
        private sealed class Fx
        {
            /// <summary>承载渲染的显示单元</summary>
            public SpriteActor Actor;

            /// <summary>当前位置（世界坐标）</summary>
            public Vector2 Position;

            /// <summary>运动速度（世界单位 / 秒）</summary>
            public Vector2 Velocity;

            /// <summary>已播放时长（秒）</summary>
            public float Elapsed;

            /// <summary>总时长（秒）</summary>
            public float Life;

            /// <summary>起始缩放</summary>
            public float StartScale;

            /// <summary>结束缩放</summary>
            public float EndScale;

            /// <summary>起始颜色</summary>
            public Color StartColor;

            /// <summary>结束颜色</summary>
            public Color EndColor;

            /// <summary>初始旋转角（度）</summary>
            public float StartRotation;

            /// <summary>自旋速度（度 / 秒）</summary>
            public float RotationSpeed;
        }
    }
}
