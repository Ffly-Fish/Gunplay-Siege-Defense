using UnityEngine;

// MathUtil.cs —— 模拟层几何与数学工具（不依赖物理引擎）
namespace Gunplay.Core
{
    /// <summary>无物理引擎的几何判定工具</summary>
    public static class MathUtil
    {
        /// <summary>两圆是否相交</summary>
        public static bool CirclesOverlap(Vector2 a, float radiusA, Vector2 b, float radiusB)
        {
            float sum = radiusA + radiusB;
            return (a - b).sqrMagnitude <= sum * sum;
        }

        /// <summary>两向量夹角（度，0–180）</summary>
        public static float AngleDegrees(Vector2 from, Vector2 to)
        {
            if (from.sqrMagnitude < 0.000001f || to.sqrMagnitude < 0.000001f)
            {
                return 180f;
            }

            return Vector2.Angle(from, to);
        }

        /// <summary>由角度（弧度）得到单位向量</summary>
        public static Vector2 DirectionFromRadians(float radians)
        {
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        /// <summary>单位向量的角度（弧度）</summary>
        public static float RadiansFromDirection(Vector2 dir)
        {
            return Mathf.Atan2(dir.y, dir.x);
        }

        /// <summary>点到线段的最近距离（用于激光光束命中判定）</summary>
        public static float DistancePointToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared < 0.000001f)
            {
                return Vector2.Distance(point, start);
            }

            float t = Vector2.Dot(point - start, segment) / lengthSquared;
            t = Mathf.Clamp01(t);
            Vector2 closest = start + segment * t;
            return Vector2.Distance(point, closest);
        }

        /// <summary>把角度（度）归一到 [-180,180]</summary>
        public static float NormalizeDegrees(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f)
            {
                degrees -= 360f;
            }
            else if (degrees < -180f)
            {
                degrees += 360f;
            }

            return degrees;
        }

        /// <summary>血量安全值：过滤 NaN / 负数 / 上限溢出</summary>
        public static float SafeHp(float hp, float cap)
        {
            if (float.IsNaN(hp) || float.IsInfinity(hp))
            {
                return 0f;
            }

            return Mathf.Clamp(hp, 0f, cap);
        }
    }
}
