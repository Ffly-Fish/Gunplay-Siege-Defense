using System;

// IRandomSource.cs —— 随机数抽象，测试可注入固定种子以获得确定性
namespace Gunplay.Core
{
    /// <summary>随机数来源抽象</summary>
    public interface IRandomSource
    {
        /// <summary>[0,1) 随机浮点</summary>
        float NextFloat();
        /// <summary>[min,max) 随机整数</summary>
        int Range(int min, int maxExclusive);
        /// <summary>[min,max) 随机浮点</summary>
        float Range(float min, float max);
    }

    /// <summary>基于 System.Random 的实现（可复现，测试与正式共用）</summary>
    public sealed class SeededRandomSource : IRandomSource
    {
        // 内部随机数发生器
        private readonly Random random;

        /// <summary>以种子构造</summary>
        public SeededRandomSource(int seed)
        {
            random = new Random(seed);
        }

        /// <inheritdoc />
        public float NextFloat() => (float)random.NextDouble();

        /// <inheritdoc />
        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min)
            {
                return min;
            }

            return random.Next(min, maxExclusive);
        }

        /// <inheritdoc />
        public float Range(float min, float max)
        {
            if (max <= min)
            {
                return min;
            }

            return min + (float)random.NextDouble() * (max - min);
        }
    }
}
