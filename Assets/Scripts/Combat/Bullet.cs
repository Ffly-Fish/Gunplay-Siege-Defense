using Gunplay.Core;
using UnityEngine;

// Bullet.cs —— 子弹运行时实体（普通直射弹 / 贯穿弹 / 抛物线手雷）
namespace Gunplay.Combat
{
    /// <summary>子弹飞行方式</summary>
    public enum BulletFlight
    {
        /// <summary>直线直射</summary>
        Straight,
        /// <summary>抛物线投掷（到达目标点后爆炸）</summary>
        Arc
    }

    /// <summary>单发子弹的运行时状态</summary>
    public sealed class Bullet
    {
        /// <summary>所属武器 Id</summary>
        public string WeaponId;
        /// <summary>当前世界坐标</summary>
        public Vector2 Position;
        /// <summary>速度向量（世界单位/秒）</summary>
        public Vector2 Velocity;
        /// <summary>单次命中伤害</summary>
        public float Damage;
        /// <summary>伤害类型</summary>
        public DamageType Type;
        /// <summary>碰撞半径</summary>
        public float Radius;
        /// <summary>是否暴击（开火瞬间判定）</summary>
        public bool Crit;
        /// <summary>发射原点（盾兵护盾朝向判定用）</summary>
        public Vector2 Origin;
        /// <summary>剩余可穿透敌人数</summary>
        public int PierceRemaining;
        /// <summary>爆炸半径（0 = 不爆炸）</summary>
        public float ExplosionRadius;
        /// <summary>飞行方式</summary>
        public BulletFlight Flight;
        /// <summary>手雷目标点（仅 Arc 使用）</summary>
        public Vector2 ArcTargetPos;
        /// <summary>剩余存活时间</summary>
        public float Lifetime;
        /// <summary>命中过的敌人 Id（穿透弹不重复伤害同一目标）</summary>
        public readonly System.Collections.Generic.List<int> HitIds = new System.Collections.Generic.List<int>(4);
        /// <summary>是否存活</summary>
        public bool Alive;
        /// <summary>渲染用的抛射高度偏移（表现层读取，模拟层不参与判定）</summary>
        public float VisualArcHeight;

        /// <summary>该子弹是否命中过指定敌人</summary>
        public bool HasHit(int enemyId) => HitIds.Contains(enemyId);

        /// <summary>复位为可复用状态（对象池回收时调用）</summary>
        public void Reset()
        {
            WeaponId = string.Empty;
            Position = Vector2.zero;
            Velocity = Vector2.zero;
            Damage = 0f;
            Type = DamageType.Normal;
            Radius = 0.08f;
            Crit = false;
            Origin = Vector2.zero;
            PierceRemaining = 0;
            ExplosionRadius = 0f;
            Flight = BulletFlight.Straight;
            ArcTargetPos = Vector2.zero;
            Lifetime = BattleField.BulletMaxLifetime;
            HitIds.Clear();
            Alive = false;
            VisualArcHeight = 0f;
        }
    }
}
