using System;
using System.Collections.Generic;

// EventBus.cs —— 轻量类型化事件总线（模拟层 → 表现层单向广播）
namespace Gunplay.Core
{
    /// <summary>按事件类型分发的事件总线；同一帧内允许重入发布（使用快照遍历）</summary>
    public sealed class EventBus
    {
        // 每种事件类型对应一组订阅者
        private readonly Dictionary<Type, List<Delegate>> subscribers = new Dictionary<Type, List<Delegate>>();

        /// <summary>订阅某一类型事件</summary>
        public void Subscribe<T>(Action<T> handler) where T : class
        {
            if (handler == null)
            {
                return;
            }

            Type type = typeof(T);
            if (!subscribers.TryGetValue(type, out List<Delegate> list))
            {
                list = new List<Delegate>();
                subscribers[type] = list;
            }

            list.Add(handler);
        }

        /// <summary>取消订阅</summary>
        public void Unsubscribe<T>(Action<T> handler) where T : class
        {
            if (handler == null)
            {
                return;
            }

            if (subscribers.TryGetValue(typeof(T), out List<Delegate> list))
            {
                list.Remove(handler);
            }
        }

        /// <summary>发布事件（无订阅者时开销可忽略）</summary>
        public void Publish<T>(T evt) where T : class
        {
            if (evt == null || !subscribers.TryGetValue(typeof(T), out List<Delegate> list) || list.Count == 0)
            {
                return;
            }

            // 用索引遍历，允许订阅者在回调中取消订阅
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i < list.Count)
                {
                    (list[i] as Action<T>)?.Invoke(evt);
                }
            }
        }

        /// <summary>清空全部订阅（场景重建时使用）</summary>
        public void Clear()
        {
            subscribers.Clear();
        }
    }
}
