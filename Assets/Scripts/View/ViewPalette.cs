using Gunplay.Core;
using UnityEngine;

// ViewPalette.cs —— 表现层配色与排序常量（集中管理，方便整体调色）
namespace Gunplay.View
{
    /// <summary>表现层统一配色：敌人种类色、子弹类型色、城门与 UI 提示色</summary>
    public static class ViewPalette
    {
        /// <summary>小兵：红</summary>
        public static readonly Color Grunt = new Color(0.93f, 0.30f, 0.28f, 1f);
        /// <summary>快速兵：橙</summary>
        public static readonly Color Runner = new Color(1.00f, 0.62f, 0.18f, 1f);
        /// <summary>盾兵：蓝</summary>
        public static readonly Color Shield = new Color(0.32f, 0.62f, 0.96f, 1f);
        /// <summary>飞行兵：紫</summary>
        public static readonly Color Flyer = new Color(0.72f, 0.44f, 0.96f, 1f);
        /// <summary>重甲兵：灰</summary>
        public static readonly Color Heavy = new Color(0.64f, 0.68f, 0.75f, 1f);
        /// <summary>Boss：暗红</summary>
        public static readonly Color Boss = new Color(0.66f, 0.12f, 0.16f, 1f);

        /// <summary>护盾正对炮口时的高亮色</summary>
        public static readonly Color ShieldFacing = new Color(0.72f, 0.92f, 1f, 1f);
        /// <summary>护盾背对炮口时的暗色</summary>
        public static readonly Color ShieldIdle = new Color(0.30f, 0.52f, 0.86f, 1f);

        /// <summary>普通弹：暖黄</summary>
        public static readonly Color BulletNormal = new Color(1f, 0.92f, 0.55f, 1f);
        /// <summary>贯穿弹：青蓝</summary>
        public static readonly Color BulletPiercing = new Color(0.45f, 0.95f, 1f, 1f);
        /// <summary>爆炸弹：橙红</summary>
        public static readonly Color BulletExplosive = new Color(1f, 0.55f, 0.22f, 1f);

        /// <summary>伤害数字：白</summary>
        public static readonly Color DamageText = new Color(1f, 0.98f, 0.92f, 1f);
        /// <summary>暴击伤害数字：金</summary>
        public static readonly Color CritText = new Color(1f, 0.82f, 0.25f, 1f);
        /// <summary>金币飘字：金</summary>
        public static readonly Color GoldText = new Color(1f, 0.86f, 0.30f, 1f);
        /// <summary>护盾被挡下：盾蓝</summary>
        public static readonly Color BlockText = new Color(0.60f, 0.85f, 1f, 1f);
        /// <summary>破盾提示：洋红</summary>
        public static readonly Color BreakText = new Color(1f, 0.45f, 0.85f, 1f);

        /// <summary>血条底色</summary>
        public static readonly Color HpBarBack = new Color(0.09f, 0.10f, 0.14f, 0.85f);
        /// <summary>血条填充色（满血）</summary>
        public static readonly Color HpBarFill = new Color(0.42f, 0.95f, 0.55f, 1f);
        /// <summary>血条填充色（低血）</summary>
        public static readonly Color HpBarLow = new Color(1f, 0.35f, 0.28f, 1f);

        /// <summary>枪口火焰</summary>
        public static readonly Color MuzzleFlash = new Color(1f, 0.90f, 0.55f, 1f);
        /// <summary>爆炸主色</summary>
        public static readonly Color Explosion = new Color(1f, 0.62f, 0.22f, 1f);
        /// <summary>爆炸内核</summary>
        public static readonly Color ExplosionCore = new Color(1f, 0.96f, 0.75f, 1f);
        /// <summary>激光主色</summary>
        public static readonly Color Laser = new Color(1f, 0.35f, 0.55f, 1f);
        /// <summary>激光外发光</summary>
        public static readonly Color LaserGlow = new Color(1f, 0.55f, 0.85f, 0.45f);

        /// <summary>取敌人身体颜色</summary>
        public static Color EnemyBody(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Runner:
                    return Runner;
                case EnemyKind.Shield:
                    return Shield;
                case EnemyKind.Flyer:
                    return Flyer;
                case EnemyKind.Heavy:
                    return Heavy;
                case EnemyKind.Boss:
                    return Boss;
                default:
                    return Grunt;
            }
        }

        /// <summary>取子弹颜色</summary>
        public static Color Bullet(DamageType type)
        {
            switch (type)
            {
                case DamageType.Piercing:
                    return BulletPiercing;
                case DamageType.Explosive:
                    return BulletExplosive;
                default:
                    return BulletNormal;
            }
        }
    }

    /// <summary>表现层排序层级（数值越大越靠前；世界内 SpriteRenderer 共用 Default 排序层）</summary>
    public static class ViewOrder
    {
        /// <summary>战场背景</summary>
        public const int Background = -1000;
        /// <summary>城墙</summary>
        public const int Wall = -100;
        /// <summary>城墙受击高光</summary>
        public const int WallFlash = -99;
        /// <summary>炮台底座</summary>
        public const int Cannon = -50;
        /// <summary>瞄准虚线</summary>
        public const int AimLine = -20;
        /// <summary>敌人身体</summary>
        public const int EnemyBody = 0;
        /// <summary>盾兵护盾</summary>
        public const int EnemyShield = 5;
        /// <summary>敌人血条</summary>
        public const int EnemyHpBar = 20;
        /// <summary>Boss 光环</summary>
        public const int BossAura = 18;
        /// <summary>激光外发光</summary>
        public const int LaserGlow = 24;
        /// <summary>激光核心</summary>
        public const int LaserCore = 25;
        /// <summary>子弹与拖尾</summary>
        public const int Bullet = 30;
        /// <summary>受击 / 爆炸等瞬时特效</summary>
        public const int Fx = 40;
        /// <summary>准星</summary>
        public const int Crosshair = 60;
        /// <summary>飘字</summary>
        public const int FloatingNumber = 70;
    }
}
