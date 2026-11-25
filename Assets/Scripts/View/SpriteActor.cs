using System.Collections.Generic;
using UnityEngine;

// SpriteActor.cs —— 池化的 SpriteRenderer 显示单元（表现层所有可见元素都由它承载，避免每帧 Instantiate/Destroy）
namespace Gunplay.View
{
    /// <summary>一个可复用的 SpriteRenderer 显示单元：只负责设置精灵、颜色、缩放、旋转与排序层级</summary>
    public sealed class SpriteActor
    {
        /// <summary>承载渲染的 GameObject</summary>
        public readonly GameObject GameObject;
        /// <summary>Transform 缓存（避免每帧 GetComponent）</summary>
        public readonly Transform Transform;
        /// <summary>SpriteRenderer 缓存</summary>
        public readonly SpriteRenderer Renderer;

        /// <summary>创建显示单元</summary>
        public SpriteActor(Transform parent, string name)
        {
            GameObject = new GameObject(name);
            Transform = GameObject.transform;
            Transform.SetParent(parent, false);
            Renderer = GameObject.AddComponent<SpriteRenderer>();
            Renderer.sprite = SpriteFactory.White;
            Renderer.color = Color.white;
            GameObject.SetActive(false);
        }

        /// <summary>设置精灵</summary>
        public void SetSprite(Sprite sprite)
        {
            if (Renderer.sprite != sprite)
            {
                Renderer.sprite = sprite;
            }
        }

        /// <summary>设置颜色（含透明度）</summary>
        public void SetColor(Color color)
        {
            Renderer.color = color;
        }

        /// <summary>设置世界坐标（父节点在原点，因此直接用 localPosition）</summary>
        public void SetPosition(Vector2 position)
        {
            Transform.localPosition = new Vector3(position.x, position.y, 0f);
        }

        /// <summary>等比缩放</summary>
        public void SetScale(float scale)
        {
            Transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>非等比缩放（血条 / 城墙 / 直线用）</summary>
        public void SetScale(float scaleX, float scaleY)
        {
            Transform.localScale = new Vector3(scaleX, scaleY, 1f);
        }

        /// <summary>设置绕 Z 轴旋转角度（度）</summary>
        public void SetRotation(float degrees)
        {
            Transform.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }

        /// <summary>设置排序层级</summary>
        public void SetOrder(int sortingOrder)
        {
            Renderer.sortingOrder = sortingOrder;
        }

        /// <summary>显示 / 隐藏（隐藏时不参与渲染，但仍保留在层级中）</summary>
        public void SetActive(bool active)
        {
            if (GameObject.activeSelf != active)
            {
                GameObject.SetActive(active);
            }
        }

        /// <summary>当前是否可见</summary>
        public bool IsActive
        {
            get { return GameObject.activeSelf; }
        }
    }

    /// <summary>SpriteActor 的对象池：只创建不销毁，隐藏即复用</summary>
    public sealed class SpriteActorPool
    {
        /// <summary>池内所有单元的父节点</summary>
        private readonly Transform root;
        /// <summary>空闲单元栈</summary>
        private readonly Stack<SpriteActor> free = new Stack<SpriteActor>();
        /// <summary>已创建单元总数（调试 / 性能观察用）</summary>
        private readonly List<SpriteActor> created = new List<SpriteActor>();
        /// <summary>单元名字前缀</summary>
        private readonly string namePrefix;

        /// <summary>创建对象池并预热指定数量</summary>
        public SpriteActorPool(Transform parent, string namePrefix, int prewarm)
        {
            this.namePrefix = namePrefix;
            GameObject rootObject = new GameObject(namePrefix);
            root = rootObject.transform;
            root.SetParent(parent, false);
            for (int i = 0; i < prewarm; i++)
            {
                SpriteActor actor = Create();
                free.Push(actor);
            }
        }

        /// <summary>已创建的显示单元总数</summary>
        public int CreatedCount
        {
            get { return created.Count; }
        }

        /// <summary>取一个显示单元（池空时自动扩容）并激活</summary>
        public SpriteActor Rent()
        {
            SpriteActor actor = free.Count > 0 ? free.Pop() : Create();
            actor.SetActive(true);
            return actor;
        }

        /// <summary>归还显示单元（隐藏）</summary>
        public void Return(SpriteActor actor)
        {
            if (actor == null)
            {
                return;
            }

            actor.SetActive(false);
            free.Push(actor);
        }

        /// <summary>创建新的显示单元</summary>
        private SpriteActor Create()
        {
            SpriteActor actor = new SpriteActor(root, namePrefix);
            created.Add(actor);
            return actor;
        }
    }
}
