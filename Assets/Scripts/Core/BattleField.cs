using UnityEngine;

// BattleField.cs —— 战场几何常量（世界单位：1 unit = 100 参考像素；屏幕 10.8 × 19.2）
namespace Gunplay.Core
{
    /// <summary>战场固定坐标：炮口、城门线、敌人出生线、边界</summary>
    public static class BattleField
    {
        /// <summary>战场半宽（世界单位）</summary>
        public const float HalfWidth = 5.4f;
        /// <summary>战场半高（世界单位）</summary>
        public const float HalfHeight = 9.6f;
        /// <summary>炮口位置（屏幕底部中央，固定不随手指移动）</summary>
        public static readonly Vector2 Muzzle = new Vector2(0f, -8.2f);
        /// <summary>城门受击线：敌人 y 到达该线即攻击城门</summary>
        public const float GateLineY = -8.6f;
        /// <summary>敌人出生线（屏幕上方外沿）</summary>
        public const float SpawnLineY = 10.6f;
        /// <summary>子弹飞出该高度后回收</summary>
        public const float BulletDespawnY = 11.5f;
        /// <summary>子弹默认存活时间（秒）</summary>
        public const float BulletMaxLifetime = 2.5f;
        /// <summary>手雷爆炸半径的兜底值（配置为 0 时使用）</summary>
        public const float DefaultExplosionRadius = 1.6f;
        /// <summary>手雷最大投掷距离</summary>
        public const float MaxArcThrowDistance = 11f;
        /// <summary>手雷最小投掷距离</summary>
        public const float MinArcThrowDistance = 2.5f;
    }
}
