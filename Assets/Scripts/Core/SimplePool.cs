using System.Collections.Generic;

// SimplePool.cs —— 模拟层对象池（子弹 / 敌人 / 飘字数据复用，避免 GC）
namespace Gunplay.Core
{
    /// <summary>泛型简单对象池；池空时自动扩容</summary>
    public sealed class SimplePool<T> where T : class
    {
        // 空闲对象栈
        private readonly Stack<T> free = new Stack<T>();
        // 对象工厂
        private readonly System.Func<T> factory;
        // 回收时的重置回调（可选）
        private readonly System.Action<T> onRelease;

        /// <summary>构造对象池</summary>
        /// <param name="factory">对象工厂</param>
        /// <param name="onRelease">回收时重置，可为 null</param>
        /// <param name="prewarm">预热数量</param>
        public SimplePool(System.Func<T> factory, System.Action<T> onRelease = null, int prewarm = 0)
        {
            this.factory = factory;
            this.onRelease = onRelease;
            for (int i = 0; i < prewarm; i++)
            {
                free.Push(factory());
            }
        }

        /// <summary>取出一个对象</summary>
        public T Get()
        {
            T item = free.Count > 0 ? free.Pop() : factory();
            return item;
        }

        /// <summary>回收对象</summary>
        public void Release(T item)
        {
            if (item == null)
            {
                return;
            }

            onRelease?.Invoke(item);
            free.Push(item);
        }

        /// <summary>清空池</summary>
        public void Clear()
        {
            free.Clear();
        }

        /// <summary>空闲数量（调试用）</summary>
        public int FreeCount => free.Count;
    }
}
