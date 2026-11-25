using System.Collections.Generic;
using UnityEngine;

// GameEvents.cs —— 模拟层向表现层广播的一次性事件（class 便于 EventBus 按类型分发）
namespace Gunplay.Core
{
    /// <summary>敌人被击杀</summary>
    public sealed class EnemyKilledEvent
    {
        /// <summary>敌人实例 Id</summary>
        public int Id;
        /// <summary>敌人种类</summary>
        public EnemyKind Kind;
        /// <summary>死亡位置</summary>
        public Vector2 Pos;
        /// <summary>获得金币（已含连杀倍率）</summary>
        public int Gold;
        /// <summary>击杀后的连杀数</summary>
        public int Combo;
    }

    /// <summary>敌人出生</summary>
    public sealed class EnemySpawnedEvent
    {
        /// <summary>敌人实例 Id</summary>
        public int Id;
        /// <summary>敌人种类</summary>
        public EnemyKind Kind;
        /// <summary>出生位置</summary>
        public Vector2 Pos;
        /// <summary>最大血量</summary>
        public float MaxHp;
        /// <summary>是否 Boss</summary>
        public bool IsBoss;
    }

    /// <summary>敌人受到伤害（用于飘字与受击反馈）</summary>
    public sealed class EnemyDamagedEvent
    {
        /// <summary>敌人实例 Id</summary>
        public int Id;
        /// <summary>最终伤害</summary>
        public float Damage;
        /// <summary>是否暴击</summary>
        public bool Crit;
        /// <summary>命中位置</summary>
        public Vector2 Pos;
    }

    /// <summary>普通弹被盾兵护盾挡下</summary>
    public sealed class ShieldBlockedEvent
    {
        /// <summary>敌人实例 Id</summary>
        public int Id;
        /// <summary>弹开位置</summary>
        public Vector2 Pos;
    }

    /// <summary>爆炸 / 贯穿伤害命中盾兵（克制成功）</summary>
    public sealed class ShieldBrokenEvent
    {
        /// <summary>敌人实例 Id</summary>
        public int Id;
        /// <summary>命中位置</summary>
        public Vector2 Pos;
    }

    /// <summary>发生一次爆炸</summary>
    public sealed class ExplosionEvent
    {
        /// <summary>爆炸中心</summary>
        public Vector2 Pos;
        /// <summary>爆炸半径</summary>
        public float Radius;
    }

    /// <summary>城门受到伤害</summary>
    public sealed class GateDamagedEvent
    {
        /// <summary>本次伤害</summary>
        public float Damage;
        /// <summary>剩余耐久</summary>
        public float GateHp;
        /// <summary>城门耐久上限</summary>
        public float GateMaxHp;
    }

    /// <summary>金币变化</summary>
    public sealed class GoldChangedEvent
    {
        /// <summary>当前金币</summary>
        public int Gold;
        /// <summary>本次变化量</summary>
        public int Delta;
    }

    /// <summary>连杀数 / 倍率变化</summary>
    public sealed class ComboChangedEvent
    {
        /// <summary>当前连杀</summary>
        public int Combo;
        /// <summary>当前倍率</summary>
        public float Multiplier;
        /// <summary>反馈等级（0–4）</summary>
        public int FeedbackLevel;
    }

    /// <summary>一波开始</summary>
    public sealed class WaveStartedEvent
    {
        /// <summary>关卡</summary>
        public int Level;
        /// <summary>波序号（1 起）</summary>
        public int Wave;
        /// <summary>本关总波数</summary>
        public int WaveCount;
        /// <summary>是否 Boss 波</summary>
        public bool BossWave;
    }

    /// <summary>一波清空</summary>
    public sealed class WaveClearedEvent
    {
        /// <summary>关卡</summary>
        public int Level;
        /// <summary>波序号</summary>
        public int Wave;
        /// <summary>清波奖励金币</summary>
        public int Reward;
    }

    /// <summary>本关全部波次清空（进入结算页前触发）</summary>
    public sealed class LevelClearedEvent
    {
        /// <summary>通关关卡</summary>
        public int Level;
        /// <summary>通关奖励金币</summary>
        public int Reward;
        /// <summary>本关统计</summary>
        public LevelStats Stats;
    }

    /// <summary>游戏状态切换</summary>
    public sealed class GameStateChangedEvent
    {
        /// <summary>切换前状态</summary>
        public GameState Previous;
        /// <summary>切换后状态</summary>
        public GameState Current;
    }

    /// <summary>商店刷新</summary>
    public sealed class ShopRolledEvent
    {
        /// <summary>本次刷出的全部奖励</summary>
        public IReadOnlyList<ShopOffer> Offers;
        /// <summary>本关已重刷次数</summary>
        public int RerollCount;
        /// <summary>下一次重刷价格</summary>
        public int RerollCost;
    }

    /// <summary>商店购买成功</summary>
    public sealed class ShopPurchasedEvent
    {
        /// <summary>购买的奖励</summary>
        public ShopOffer Offer;
    }

    /// <summary>武器发射（表现层据此播放枪口特效；同帧内由表现层合并音效）</summary>
    public sealed class WeaponFiredEvent
    {
        /// <summary>武器 Id</summary>
        public string WeaponId;
        /// <summary>发射位置</summary>
        public Vector2 Pos;
        /// <summary>发射方向</summary>
        public Vector2 Direction;
    }

    /// <summary>激励视频广告结束（只有看完才发奖励；UI 可据此提示「广告未看完」）</summary>
    public sealed class AdRewardFinishedEvent
    {
        /// <summary>广告位 Id（与 DouyinBridge 的 Placement 常量对应）</summary>
        public string PlacementId;
        /// <summary>是否看完（false = 中途关闭 / 加载失败 / 未就绪）</summary>
        public bool Success;
    }
}
