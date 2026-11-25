using UnityEngine;

// LaserShot.cs —— 激光束的当前帧表现数据（模拟层产生，表现层只读）
namespace Gunplay.Combat
{
    /// <summary>一帧激光束数据</summary>
    public struct LaserShot
    {
        /// <summary>武器 Id</summary>
        public string WeaponId;
        /// <summary>光束起点（炮口）</summary>
        public Vector2 From;
        /// <summary>光束终点（命中点或最大射程）</summary>
        public Vector2 To;
        /// <summary>光束宽度</summary>
        public float Width;
        /// <summary>能量余量（0–1，表现层用颜色/透明度反映）</summary>
        public float Energy01;
    }
}
