using Gunplay.Core;
using UnityEngine;

// Enemy.cs —— 敌人运行时实体（纯数据 + 轻量方法，不依赖 MonoBehaviour）
namespace Gunplay.Combat
{
    /// <summary>一次伤害请求（由武器/爆炸生成）</summary>
    public struct DamageInfo
    {
        /// <summary>伤害数值（已含武器与词条加成）</summary>
        public float Amount;
        /// <summary>伤害类型（决定盾兵护盾是否挡下）</summary>
        public DamageType Type;
        /// <summary>伤害来源位置（用于护盾朝向判定）</summary>
        public Vector2 SourcePos;
        /// <summary>是否暴击</summary>
        public bool Crit;
        /// <summary>来源武器 Id（用于聚焦火力判定）</summary>
        public string WeaponId;
    }

    /// <summary>伤害结算结果</summary>
    public enum HitOutcome
    {
        /// <summary>正常造成伤害</summary>
        Hit,
        /// <summary>被盾兵护盾挡下</summary>
        Blocked,
        /// <summary>本次伤害击杀敌人</summary>
        Killed
    }

    /// <summary>单个敌人实例的运行时状态</summary>
    public sealed class Enemy
    {
        /// <summary>实例 Id（表现层用它配对视图）</summary>
        public int Id;
        /// <summary>配置数据</summary>
        public EnemyConfigData Config;
        /// <summary>世界坐标</summary>
        public Vector2 Position;
        /// <summary>当前血量</summary>
        public float Hp;
        /// <summary>血量上限（已含难度系数）</summary>
        public float MaxHp;
        /// <summary>移动速度（已含难度系数）</summary>
        public float Speed;
        /// <summary>碰撞半径</summary>
        public float Radius;
        /// <summary>护甲减伤比例（0.2 = 减伤 20%）</summary>
        public float Armor;
        /// <summary>击杀金币（已含连杀前的基数）</summary>
        public int Gold;
        /// <summary>到达城门时造成的伤害</summary>
        public float GateDamage;
        /// <summary>是否存活</summary>
        public bool Alive;
        /// <summary>是否 Boss</summary>
        public bool IsBoss;
        /// <summary>Boss 当前阶段（1 起，非 Boss 为 0）</summary>
        public int BossPhase;
        /// <summary>护盾当前朝向（度）</summary>
        public float ShieldAngle;
        /// <summary>受击闪白剩余时间（表现层使用）</summary>
        public float HitFlash;
        /// <summary>最近一次受击时间（聚焦火力判定用）</summary>
        public float LastHitTime;
        /// <summary>最近一次命中它的武器 Id（聚焦火力判定用）</summary>
        public string LastHitWeaponId;
        /// <summary>出生时间（表现层做入场动画用）</summary>
        public float SpawnTime;

        /// <summary>该敌人种类</summary>
        public EnemyKind Kind => Config.kind;

        /// <summary>是否有生效中的环绕护盾</summary>
        public bool HasShield => Config.hasOrbitShield && Alive;

        /// <summary>护盾是否正对伤害来源（正面扇区内）</summary>
        public bool IsShieldFacing(Vector2 sourcePos)
        {
            if (!HasShield)
            {
                return false;
            }

            Vector2 toSource = sourcePos - Position;
            if (toSource.sqrMagnitude < 0.000001f)
            {
                return true;
            }

            Vector2 shieldDir = MathUtil.DirectionFromRadians(ShieldAngle * Mathf.Deg2Rad);
            return Vector2.Angle(shieldDir, toSource) <= Config.shieldFrontArc;
        }

        /// <summary>复位为可复用状态（对象池回收时调用）</summary>
        public void Reset()
        {
            Id = 0;
            Config = null;
            Position = Vector2.zero;
            Hp = 0f;
            MaxHp = 1f;
            Speed = 0f;
            Radius = 0.3f;
            Armor = 0f;
            Gold = 0;
            GateDamage = 0f;
            Alive = false;
            IsBoss = false;
            BossPhase = 0;
            ShieldAngle = 0f;
            HitFlash = 0f;
            LastHitTime = -999f;
            LastHitWeaponId = string.Empty;
            SpawnTime = 0f;
        }
    }
}
